/*
    The page half of the playground.

    Monaco lives here and the compiler lives in .NET, so every question the editor asks becomes a
    call into the Interop class. Two conventions keep that boundary honest:

      - Monaco counts lines and columns from one; the language service counts from zero. The
        conversion happens in toService and fromService, and nowhere else.
      - Every language provider first pushes the current text across, because the service answers
        about the document it was last told about, not about the one Monaco is holding.
*/

window.lensPlayground = (function () {
    "use strict";

    const ASSEMBLY = "Lens.Playground";
    const STORAGE_KEY = "lens-playground-source";
    const DIAGNOSTIC_DELAY = 350;

    let editor = null;
    let ready = false;
    let running = false;
    let diagnosticTimer = null;
    let semanticLegend = ["keyword", "variable", "type", "function", "parameter", "property", "number", "string", "regexp", "operator"];

    /* ---------------------------------------------------------------- calls */

    function call(method, ...args) {
        return DotNet.invokeMethodAsync(ASSEMBLY, method, ...args);
    }

    /**
     * Tells the compiler what the editor currently holds. Every query is preceded by this, so that
     * an answer can never describe an older version of the text than the user is looking at.
     */
    function sync() {
        return call("Update", editor.getValue());
    }

    /**
     * Whether the compiler can be called at all.
     *
     * Monaco starts asking for tokens and diagnostics the moment a model exists, which is well
     * before the runtime has finished downloading. Calling across at that point throws out of
     * Blazor rather than returning an error, so every provider checks first.
     */
    function available() {
        return ready;
    }

    /* ----------------------------------------------------------- coordinates */

    function toService(position) {
        return [position.lineNumber - 1, position.column - 1];
    }

    function fromService(range) {
        return {
            startLineNumber: range.startLine + 1,
            startColumn: range.startColumn + 1,
            endLineNumber: range.endLine + 1,
            endColumn: range.endColumn + 1
        };
    }

    /* -------------------------------------------------------------- language */

    /**
     * A tokenizer good enough for the first paint.
     *
     * Real colouring comes from the compiler, which knows a type from a variable; this only has to
     * keep the text from flashing white while the first analysis runs, and to colour a file the
     * analyser could not parse at all.
     */
    const monarch = {
        defaultToken: "",
        keywords: [
            "declare", "use", "record", "type", "fun", "pure", "let", "var",
            "if", "then", "else", "while", "do", "for", "in", "try", "catch", "finally",
            "throw", "match", "with", "case", "when", "yield", "await", "using",
            "new", "not", "is", "as", "of", "ref", "typeof", "default"
        ],
        constants: ["true", "false", "null"],
        operators: [
            "->", "=>", "|>", "<|", "**", "<:", ":>", "==", "<=", ">=", "<>",
            "??", "?.", "?[", "&&", "||", "^^", "::", "...", ".."
        ],
        symbols: /[+\-*/%&|^<>=~?:!]+/,
        tokenizer: {
            root: [
                [/\/\/.*$/, "comment"],
                [/#([^#]|##)*#[a-zA-Z]*/, "regexp"],
                [/\$"/, {token: "string.quote", next: "@interpolated"}],
                [/"/, {token: "string.quote", next: "@string"}],
                [/\b\d+(\.\d+)?[FfMm]\b/, "number.float"],
                [/\b\d+\.\d+\b/, "number.float"],
                [/\b\d+L?\b/, "number"],
                [/[A-Za-z_][A-Za-z0-9_]*/, {
                    cases: {
                        "@keywords": "keyword",
                        "@constants": "constant",
                        "@default": "identifier"
                    }
                }],
                [/@symbols/, {
                    cases: {
                        "@operators": "operator",
                        "@default": "operator"
                    }
                }]
            ],
            string: [
                [/[^\\"]+/, "string"],
                [/\\./, "string.escape"],
                [/"/, {token: "string.quote", next: "@pop"}]
            ],
            interpolated: [
                [/\{\{|\}\}/, "string"],
                [/\{/, {token: "delimiter.bracket", next: "@interpolation"}],
                [/[^\\"{}]+/, "string"],
                [/\\./, "string.escape"],
                [/"/, {token: "string.quote", next: "@pop"}]
            ],
            interpolation: [
                [/\}/, {token: "delimiter.bracket", next: "@pop"}],
                {include: "root"}
            ]
        }
    };

    // Built lazily: it names monaco.languages.IndentAction, which does not exist until the
    // editor module has loaded.
    function languageConfiguration() {
        return {
            comments: {lineComment: "//"},
            brackets: [["{", "}"], ["[", "]"], ["(", ")"]],
            autoClosingPairs: [
                {open: "{", close: "}"},
                {open: "[", close: "]"},
                {open: "(", close: ")"},
                {open: "\"", close: "\"", notIn: ["string"]}
            ],
            surroundingPairs: [
                {open: "{", close: "}"},
                {open: "[", close: "]"},
                {open: "(", close: ")"},
                {open: "\"", close: "\""}
            ],
            wordPattern: /[A-Za-z_][A-Za-z0-9_]*/,
            indentationRules: {
                increaseIndentPattern: /(^\s*(declare|record|type)\b.*$)|((->|\b(then|else|do|try|finally)|\bwith)\s*$)|(^\s*catch\b.*$)/,
                decreaseIndentPattern: /^\s*(else|catch|finally)\b.*$/
            },
            onEnterRules: [
                {beforeText: /^\s*(declare|record|type)\b.*$/, action: {indentAction: monaco.languages.IndentAction.Indent}},
                {beforeText: /(->|\b(then|else|do|try|finally|with))\s*$/, action: {indentAction: monaco.languages.IndentAction.Indent}}
            ]
        };
    }

    /**
     * The editor's icon for a kind of symbol. The names on the left are the compiler's.
     */
    function completionKind(kind) {
        const kinds = monaco.languages.CompletionItemKind;

        switch (kind) {
            case "Local": return kinds.Variable;
            case "GlobalVariable": return kinds.Variable;
            case "Parameter": return kinds.Variable;
            case "Function": return kinds.Function;
            case "Record": return kinds.Struct;
            case "RecordField": return kinds.Field;
            case "AlgebraicType": return kinds.Enum;
            case "TypeLabel": return kinds.EnumMember;
            case "HostType": return kinds.Class;
            case "Member": return kinds.Property;
            case "Keyword": return kinds.Keyword;
            case "Namespace": return kinds.Module;
            default: return kinds.Text;
        }
    }

    function symbolKind(kind) {
        const kinds = monaco.languages.SymbolKind;

        switch (kind) {
            case "Function": return kinds.Function;
            case "Record": return kinds.Struct;
            case "RecordField": return kinds.Field;
            case "AlgebraicType": return kinds.Enum;
            case "TypeLabel": return kinds.EnumMember;
            case "Local": return kinds.Variable;
            case "GlobalVariable": return kinds.Variable;
            default: return kinds.Object;
        }
    }

    function registerLanguage() {
        monaco.languages.register({id: "lens", extensions: [".lns"], aliases: ["LENS", "lens"]});
        monaco.languages.setMonarchTokensProvider("lens", monarch);
        monaco.languages.setLanguageConfiguration("lens", languageConfiguration());

        monaco.languages.registerCompletionItemProvider("lens", {
            triggerCharacters: [".", ":"],
            provideCompletionItems: async function (model, position) {
                if (!available()) {
                    return {suggestions: []};
                }

                await sync();

                const suggestions = await call("Suggest", ...toService(position));
                const word = model.getWordUntilPosition(position);
                const range = {
                    startLineNumber: position.lineNumber,
                    endLineNumber: position.lineNumber,
                    startColumn: word.startColumn,
                    endColumn: word.endColumn
                };

                return {
                    suggestions: suggestions.map(function (x) {
                        return {
                            label: x.label,
                            kind: completionKind(x.kind),
                            detail: x.detail,
                            insertText: x.label,
                            range: range
                        };
                    })
                };
            }
        });

        monaco.languages.registerHoverProvider("lens", {
            provideHover: async function (model, position) {
                if (!available()) {
                    return null;
                }

                await sync();

                const explanation = await call("Explain", ...toService(position));
                if (!explanation) {
                    return null;
                }

                return {
                    range: fromService(explanation.range),
                    contents: [{value: "```lens\n" + explanation.text + "\n```"}]
                };
            }
        });

        monaco.languages.registerDefinitionProvider("lens", {
            provideDefinition: async function (model, position) {
                if (!available()) {
                    return null;
                }

                await sync();

                const range = await call("Define", ...toService(position));

                return range ? [{uri: model.uri, range: fromService(range)}] : null;
            }
        });

        monaco.languages.registerReferenceProvider("lens", {
            provideReferences: async function (model, position) {
                if (!available()) {
                    return [];
                }

                await sync();

                const ranges = await call("FindReferences", ...toService(position));

                return ranges.map(function (x) {
                    return {uri: model.uri, range: fromService(x)};
                });
            }
        });

        monaco.languages.registerRenameProvider("lens", {
            provideRenameEdits: async function (model, position, newName) {
                if (!available()) {
                    return {edits: [], rejectReason: "The compiler is still loading."};
                }

                await sync();

                const outcome = await call("Rename", ...toService(position), newName);
                if (!outcome.isAllowed) {
                    return {edits: [], rejectReason: outcome.refusal};
                }

                return {
                    edits: outcome.edits.map(function (edit) {
                        return {
                            resource: model.uri,
                            versionId: model.getVersionId(),
                            textEdit: {range: fromService(edit.range), text: edit.text}
                        };
                    })
                };
            }
        });

        monaco.languages.registerDocumentSymbolProvider("lens", {
            provideDocumentSymbols: async function () {
                if (!available()) {
                    return [];
                }

                await sync();

                const outline = await call("Outline");

                const convert = function (entry) {
                    return {
                        name: entry.name,
                        detail: entry.detail || "",
                        kind: symbolKind(entry.kind),
                        tags: [],
                        range: fromService(entry.range),
                        selectionRange: fromService(entry.selection),
                        children: (entry.children || []).map(convert)
                    };
                };

                return outline.map(convert);
            }
        });

        monaco.languages.registerDocumentSemanticTokensProvider("lens", {
            getLegend: function () {
                return {tokenTypes: semanticLegend, tokenModifiers: []};
            },
            provideDocumentSemanticTokens: async function () {
                if (!available()) {
                    return {data: new Uint32Array(0), resultId: null};
                }

                await sync();

                const data = await call("Colour");

                return {data: new Uint32Array(data), resultId: null};
            },
            releaseDocumentSemanticTokens: function () {
            }
        });
    }

    /* ------------------------------------------------------------ diagnostics */

    function scheduleDiagnostics() {
        if (!ready) {
            return;
        }

        window.clearTimeout(diagnosticTimer);
        diagnosticTimer = window.setTimeout(refreshDiagnostics, DIAGNOSTIC_DELAY);
    }

    async function refreshDiagnostics() {
        if (!available()) {
            return;
        }

        await sync();

        const problems = await call("Diagnose");
        const model = editor.getModel();

        monaco.editor.setModelMarkers(model, "lens", problems.map(function (problem) {
            const range = fromService(problem.range);

            return {
                message: problem.message,
                severity: problem.severity === "warning"
                    ? monaco.MarkerSeverity.Warning
                    : monaco.MarkerSeverity.Error,
                startLineNumber: range.startLineNumber,
                startColumn: range.startColumn,
                endLineNumber: range.endLineNumber,
                // a zero-width marker is invisible, so an empty range is widened by one character
                endColumn: range.endColumn > range.startColumn || range.endLineNumber > range.startLineNumber
                    ? range.endColumn
                    : range.endColumn + 1
            };
        }));
    }

    /* ----------------------------------------------------------------- output */

    const output = {
        element: null,

        clear: function () {
            this.element.textContent = "";
        },

        placeholder: function (text) {
            this.clear();
            this.append(text, "placeholder");
        },

        append: function (text, className) {
            const node = document.createElement("span");
            node.className = className;
            node.textContent = text;
            this.element.appendChild(node);
            this.element.scrollTop = this.element.scrollHeight;
        },

        rule: function () {
            const node = document.createElement("div");
            node.className = "rule";
            this.element.appendChild(node);
        }
    };

    /* -------------------------------------------------------------- running */

    async function run() {
        if (!ready || running) {
            return;
        }

        running = true;
        setStatus("Running...");

        const button = document.getElementById("run");
        button.classList.add("running");
        button.querySelector(".button-label").textContent = "Running";

        output.clear();

        try {
            const result = await call("Run", editor.getValue(), document.getElementById("input").value);
            render(result);
        } catch (error) {
            output.append("The compiler itself failed: " + error + "\n", "error");
            setStatus("Failed", true);
        } finally {
            running = false;
            button.classList.remove("running");
            button.querySelector(".button-label").textContent = "Run";
        }
    }

    function render(result) {
        if (result.output) {
            output.append(result.output, "console");
        }

        if (result.error) {
            output.rule();
            output.append(result.error + "\n", "error");

            if (result.errorRange) {
                showErrorRange(result.errorRange);
            }

            setStatus(result.isCompileError ? "Did not compile" : "Threw", true);
            return;
        }

        output.rule();
        output.append("Result: ", "result-label");
        output.append(result.result + "\n", "result");

        if (result.resultType) {
            output.append(result.resultType + "\n", "meta");
        }

        setStatus("Finished in " + Math.round(result.elapsedMs) + " ms");
    }

    /**
     * Puts the cursor on the failure and marks it, so that an error in the output pane and the
     * place it happened are not two separate things to find.
     */
    function showErrorRange(range) {
        const converted = fromService(range);

        editor.setPosition({lineNumber: converted.startLineNumber, column: converted.startColumn});
        editor.revealLineInCenterIfOutsideViewport(converted.startLineNumber);
        editor.focus();
    }

    function setStatus(text, isError) {
        const status = document.getElementById("status");

        status.textContent = text;
        status.classList.toggle("error", !!isError);
    }

    /* ---------------------------------------------------------------- samples */

    async function loadSamples() {
        const samples = await call("Samples");
        const select = document.getElementById("samples");

        samples.forEach(function (sample) {
            const option = document.createElement("option");
            option.value = sample.name;
            option.textContent = sample.title;
            select.appendChild(option);
        });

        select.disabled = false;
        select.addEventListener("change", function () {
            const chosen = samples.find(function (x) { return x.name === select.value; });
            if (chosen) {
                editor.setValue(chosen.source);
                output.placeholder("Press F8 to run.");
                setStatus("");
            }
        });

        return samples;
    }

    /* ------------------------------------------------------------------ setup */

    function createEditor(initialText) {
        monaco.editor.defineTheme("lens-dark", {
            base: "vs-dark",
            inherit: true,
            rules: [],
            colors: {"editor.background": "#1e1e1e"}
        });

        editor = monaco.editor.create(document.getElementById("editor"), {
            value: initialText,
            language: "lens",
            theme: "lens-dark",
            automaticLayout: true,
            fontFamily: "\"Cascadia Mono\", \"Fira Code\", Consolas, monospace",
            fontSize: 14,
            minimap: {enabled: false},
            scrollBeyondLastLine: false,
            renderWhitespace: "selection",
            tabSize: 4,
            insertSpaces: true,
            "semanticHighlighting.enabled": true
        });

        editor.onDidChangeModelContent(function () {
            scheduleDiagnostics();
            save();
        });

        // F8 rather than F5: F5 is how a browser reloads a page, and a playground that throws
        // away what you typed when you reach for the run key is not one anybody would keep using.
        editor.addCommand(monaco.KeyCode.F8, run);
        editor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.Enter, run);
    }

    function save() {
        try {
            window.localStorage.setItem(STORAGE_KEY, editor.getValue());
        } catch (error) {
            // a browser that refuses storage costs the draft and nothing else
        }
    }

    function restore() {
        try {
            return window.localStorage.getItem(STORAGE_KEY);
        } catch (error) {
            return null;
        }
    }

    function wireChrome() {
        document.getElementById("run").addEventListener("click", run);

        document.getElementById("clear").addEventListener("click", function () {
            output.placeholder("Press F8 to run.");
            setStatus("");
        });

        const toggle = document.getElementById("toggle-input");
        toggle.addEventListener("click", function () {
            const pane = document.getElementById("input-pane");
            const shown = pane.hasAttribute("hidden");

            pane.toggleAttribute("hidden", !shown);
            toggle.setAttribute("aria-pressed", String(shown));

            if (shown) {
                document.getElementById("input").focus();
            }
        });

        // the same keys outside the editor, so they work wherever the focus happens to be -
        // the input pane included
        window.addEventListener("keydown", function (event) {
            if (event.key === "F8" || (event.key === "Enter" && (event.ctrlKey || event.metaKey))) {
                event.preventDefault();
                run();
            }
        });
    }

    /* ----------------------------------------------------------------- public */

    return {
        /**
         * Called from .NET once the compiler is warm. Everything before this point is the page
         * loading a runtime; everything after it is a working editor.
         */
        onReady: async function () {
            ready = true;

            document.getElementById("run").disabled = false;

            try {
                semanticLegend = await call("TokenLegend");
            } catch (error) {
                // the built-in legend above is the same list, so this is not worth failing over
            }

            const samples = await loadSamples();
            const saved = restore();

            if (saved && saved.trim().length > 0) {
                editor.setValue(saved);
            } else if (samples.length > 0) {
                editor.setValue(samples[0].source);
            }

            document.getElementById("loader").classList.add("done");
            setStatus("Ready");
            output.placeholder("Press F8 to run.");

            refreshDiagnostics();
            editor.focus();
        },

        /**
         * Called from .NET while a script is running, with whatever it has printed since the last
         * time. See the runner for why this arrives in batches rather than line by line.
         */
        appendOutput: function (text) {
            output.append(text, "console");
        },

        /**
         * Builds the editor. Runs as soon as Monaco is loaded, without waiting for .NET, so that
         * there is something to look at while the runtime downloads.
         */
        boot: function () {
            output.element = document.getElementById("output");
            output.placeholder("Waiting for the compiler...");

            registerLanguage();
            createEditor("// Loading...\n");
            wireChrome();
        }
    };
})();

require.config({paths: {vs: "lib/monaco"}});
require(["vs/editor/editor.main"], function () {
    lensPlayground.boot();
});
