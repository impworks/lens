import * as fs from 'fs';
import * as path from 'path';
import * as vscode from 'vscode';
import {
    Executable,
    LanguageClient,
    LanguageClientOptions,
    ServerOptions
} from 'vscode-languageclient/node';

let client: LanguageClient | undefined;

/**
 * Starts the LENS language server and connects VS Code to it.
 *
 * Everything the extension does beyond this is declarative - the grammar, the language
 * configuration, the semantic token scopes - so this file only has to find the server, launch it,
 * and hand over.
 */
export async function activate(context: vscode.ExtensionContext): Promise<void> {
    const server = resolveServer(context);

    if (!server) {
        vscode.window.showErrorMessage(
            'The LENS language server was not found. Build it with "dotnet build Lens.LanguageServer", ' +
            'or set "lens.server.path" to the built lens-language-server.dll.'
        );
        return;
    }

    const clientOptions: LanguageClientOptions = {
        documentSelector: [{ scheme: 'file', language: 'lens' }],
        synchronize: {
            fileEvents: vscode.workspace.createFileSystemWatcher('**/*.lns')
        }
    };

    client = new LanguageClient('lens', 'LENS Language Server', server, clientOptions);
    await client.start();

    context.subscriptions.push({ dispose: () => void client?.stop() });
}

export function deactivate(): Thenable<void> | undefined {
    return client?.stop();
}

/**
 * Works out how to launch the server: a self-contained executable is run directly, a .dll through
 * the dotnet host.
 */
function resolveServer(context: vscode.ExtensionContext): ServerOptions | undefined {
    const configured = vscode.workspace.getConfiguration('lens').get<string>('server.path')?.trim();
    const dotnet = vscode.workspace.getConfiguration('lens').get<string>('server.dotnetPath')?.trim() || 'dotnet';

    const candidate = configured || bundledServer(context);
    if (!candidate || !fs.existsSync(candidate)) {
        return undefined;
    }

    const executable: Executable = candidate.endsWith('.dll')
        ? { command: dotnet, args: [candidate] }
        : { command: candidate, args: [] };

    return { run: executable, debug: executable };
}

/**
 * The server shipped inside the extension, if it was packaged with one.
 */
function bundledServer(context: vscode.ExtensionContext): string | undefined {
    const candidates = [
        path.join(context.extensionPath, 'server', 'lens-language-server.dll'),
        path.join(context.extensionPath, 'server', 'lens-language-server.exe'),
        path.join(context.extensionPath, 'server', 'lens-language-server')
    ];

    return candidates.find(x => fs.existsSync(x));
}
