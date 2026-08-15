#!/usr/bin/env node
'use strict';

// Drives the language server over stdio the way an editor does, and checks that the answers are
// the ones the features promise. This is the only place the wire format is exercised end to end -
// everything below it is covered by the .NET test suite.

const { spawn } = require('child_process');
const path = require('path');

const serverDll = process.argv[2];
if (!serverDll) {
    console.error('usage: node smoke-test.js <path to lens-language-server.dll>');
    process.exit(2);
}

const server = spawn('dotnet', [serverDll], { stdio: ['pipe', 'pipe', 'pipe'] });
server.stderr.on('data', d => process.stderr.write('[server] ' + d));

let buffer = Buffer.alloc(0);
const pending = new Map();
const notifications = [];
let nextId = 1;

server.stdout.on('data', chunk => {
    buffer = Buffer.concat([buffer, chunk]);

    for (;;) {
        const headerEnd = buffer.indexOf('\r\n\r\n');
        if (headerEnd < 0) return;

        const header = buffer.slice(0, headerEnd).toString('ascii');
        const match = /Content-Length: (\d+)/i.exec(header);
        if (!match) return;

        const length = parseInt(match[1], 10);
        const start = headerEnd + 4;
        if (buffer.length < start + length) return;

        const message = JSON.parse(buffer.slice(start, start + length).toString('utf8'));
        buffer = buffer.slice(start + length);

        if (message.id !== undefined && pending.has(message.id)) {
            const resolve = pending.get(message.id);
            pending.delete(message.id);
            resolve(message);
        } else if (message.method) {
            notifications.push(message);
        }
    }
});

function send(message) {
    const body = Buffer.from(JSON.stringify(message), 'utf8');
    server.stdin.write(`Content-Length: ${body.length}\r\n\r\n`);
    server.stdin.write(body);
}

function request(method, params) {
    const id = nextId++;
    return new Promise(resolve => {
        pending.set(id, resolve);
        send({ jsonrpc: '2.0', id, method, params });
    });
}

function notify(method, params) {
    send({ jsonrpc: '2.0', method, params });
}

function wait(ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
}

// the server normalises the uri it was given - the drive letter above all - so comparisons have to
// be case-insensitive
function sameUri(left, right) {
    return String(left).toLowerCase() === String(right).toLowerCase();
}

async function waitForDiagnostics(uri, timeoutMs = 8000) {
    const deadline = Date.now() + timeoutMs;

    while (Date.now() < deadline) {
        const found = notifications.filter(x => x.method === 'textDocument/publishDiagnostics' && sameUri(x.params.uri, uri));
        if (found.length > 0) return found[found.length - 1].params.diagnostics;
        await wait(50);
    }

    throw new Error('no diagnostics arrived for ' + uri + '; saw ' + notifications.map(x => x.method).join(','));
}

function before(left, right) {
    return left.line < right.line || (left.line === right.line && left.character <= right.character);
}

/**
 * The first outline entry whose range is inverted or does not contain its selection - which is what
 * VS Code checks before it will show any of them.
 */
function findBadRange(entries) {
    for (const entry of entries) {
        if (!entry.range || !entry.selectionRange) continue;

        const ok = before(entry.range.start, entry.range.end)
            && before(entry.range.start, entry.selectionRange.start)
            && before(entry.selectionRange.end, entry.range.end);

        if (!ok) return entry;

        const child = findBadRange(entry.children || []);
        if (child) return child;
    }

    return undefined;
}

const failures = [];

function check(name, condition, detail) {
    if (condition) {
        console.log('  ok   ' + name);
    } else {
        console.log('  FAIL ' + name + (detail ? ' -- ' + detail : ''));
        failures.push(name);
    }
}

const uri = 'file:///' + path.resolve('smoke.lns').replace(/\\/g, '/').replace(/^\//, '');

const source = [
    'record Point',
    '    X : int',
    '    Y : int',
    '',
    'fun lengthOf:int (p:Point) -> p.X + p.Y',
    '',
    'var origin = new Point 1 2',
    'var text = "hello"',
    'var size = text.Length',
    'lengthOf origin'
].join('\n');

(async () => {
    const init = await request('initialize', {
        processId: process.pid,
        rootUri: null,
        capabilities: {
            textDocument: {
                semanticTokens: { formats: ['relative'], requests: { full: true }, tokenTypes: [], tokenModifiers: [] },
                documentSymbol: { hierarchicalDocumentSymbolSupport: true }
            }
        }
    });

    const caps = init.result.capabilities;
    check('initialize answers', !!caps);
    check('advertises completion', !!caps.completionProvider);
    check('advertises hover', !!caps.hoverProvider);
    check('advertises definition', !!caps.definitionProvider);
    check('advertises references', !!caps.referencesProvider);
    check('advertises rename', !!caps.renameProvider);
    check('advertises document symbols', !!caps.documentSymbolProvider);
    check('advertises semantic tokens', !!caps.semanticTokensProvider);

    notify('initialized', {});
    notify('textDocument/didOpen', {
        textDocument: { uri, languageId: 'lens', version: 1, text: source }
    });

    const clean = await waitForDiagnostics(uri);
    check('a valid script reports nothing', clean.length === 0, JSON.stringify(clean));

    // completion after a dot
    const members = await request('textDocument/completion', {
        textDocument: { uri },
        position: { line: 4, character: 32 }
    });
    const memberLabels = (members.result.items || members.result || []).map(x => x.label);
    check('member completion offers record fields', memberLabels.includes('X') && memberLabels.includes('Y'), memberLabels.slice(0, 10).join(','));

    // completion of visible names
    const names = await request('textDocument/completion', {
        textDocument: { uri },
        position: { line: 9, character: 0 }
    });
    const nameLabels = (names.result.items || names.result || []).map(x => x.label);
    check('name completion offers locals and functions', nameLabels.includes('origin') && nameLabels.includes('lengthOf'), nameLabels.slice(0, 10).join(','));

    // hover over the record name in the function signature
    const hover = await request('textDocument/hover', {
        textDocument: { uri },
        position: { line: 4, character: 20 }
    });
    check('hover explains a name', !!(hover.result && hover.result.contents), JSON.stringify(hover.result));

    // go to definition of the function from its call site
    const definition = await request('textDocument/definition', {
        textDocument: { uri },
        position: { line: 9, character: 2 }
    });
    const target = Array.isArray(definition.result) ? definition.result[0] : definition.result;
    check('definition points at the declaration', !!target && target.range.start.line === 4, JSON.stringify(definition.result));

    // references of a local
    const references = await request('textDocument/references', {
        textDocument: { uri },
        position: { line: 6, character: 5 },
        context: { includeDeclaration: true }
    });
    check('references finds both mentions of a local', (references.result || []).length === 2, JSON.stringify(references.result));

    // document symbols
    const symbols = await request('textDocument/documentSymbol', { textDocument: { uri } });
    const outline = (symbols.result || []).map(x => x.name);
    check('outline lists the declarations', outline.includes('Point') && outline.includes('lengthOf'), outline.join(','));

    // VS Code validates this itself and rejects the whole batch when it fails, so an outline with
    // one bad entry is an outline with no entries
    const badRange = findBadRange(symbols.result || []);
    check('outline ranges contain their selections', !badRange, badRange && JSON.stringify(badRange));

    // semantic tokens
    const tokens = await request('textDocument/semanticTokens/full', { textDocument: { uri } });
    check('semantic tokens are produced', !!(tokens.result && tokens.result.data && tokens.result.data.length > 0));

    // rename a local
    const rename = await request('textDocument/rename', {
        textDocument: { uri },
        position: { line: 6, character: 5 },
        newName: 'start'
    });
    const changes = (rename.result && rename.result.changes) || {};
    const edits = changes[Object.keys(changes)[0]];
    check('rename edits every mention', !!edits && edits.length === 2, JSON.stringify(rename.result));

    // a record field is renamed at its declaration and at every access through a receiver of that
    // record's type
    const field = await request('textDocument/rename', {
        textDocument: { uri },
        position: { line: 4, character: 32 },
        newName: 'Across'
    });
    const fieldChanges = (field.result && field.result.changes) || {};
    const fieldEdits = fieldChanges[Object.keys(fieldChanges)[0]] || [];
    check('rename of a record field reaches its uses', fieldEdits.length === 2, JSON.stringify(field.result || field.error));

    // renaming something the script does not own is refused rather than silently doing nothing
    const refused = await request('textDocument/rename', {
        textDocument: { uri },
        position: { line: 8, character: 18 },
        newName: 'Whatever'
    });
    check('rename of a .NET member is refused', !!refused.error, JSON.stringify(refused));

    // static members, which is what the caret is on halfway through typing 'string::'
    notify('textDocument/didChange', {
        textDocument: { uri, version: 2 },
        contentChanges: [{ text: source + '\nstring::' }]
    });

    await wait(300);

    const statics = await request('textDocument/completion', {
        textDocument: { uri },
        position: { line: 10, character: 8 }
    });
    const staticLabels = (statics.result.items || statics.result || []).map(x => x.label);
    check('static completion offers static members', staticLabels.includes('IsNullOrEmpty') && staticLabels.includes('Join'), staticLabels.slice(0, 10).join(','));
    check('static completion leaves out instance members', !staticLabels.includes('Substring'), staticLabels.slice(0, 10).join(','));

    // a broken line does not take the rest of the file with it
    notify('textDocument/didChange', {
        textDocument: { uri, version: 2 },
        contentChanges: [{ text: source + '\nvar = = =\n' }]
    });

    await wait(500);
    const broken = await waitForDiagnostics(uri);
    check('a syntax error is reported', broken.length > 0);

    const stillThere = await request('textDocument/documentSymbol', { textDocument: { uri } });
    const stillOutline = (stillThere.result || []).map(x => x.name);
    check('the rest of the file survives a syntax error', stillOutline.includes('Point') && stillOutline.includes('lengthOf'), stillOutline.join(','));

    await request('shutdown', {});
    notify('exit', {});

    await wait(300);
    server.kill();

    console.log('');
    if (failures.length > 0) {
        console.log(`${failures.length} check(s) failed`);
        process.exit(1);
    }

    console.log('all checks passed');
    process.exit(0);
})().catch(err => {
    console.error(err);
    server.kill();
    process.exit(1);
});
