# Phase 6 — Language server

## Status

**Done**, in the shape described below. What shipped:

| Project | What it is |
|---|---|
| `Lens/Analysis` | The editor-facing view of a bound script: diagnostics, classified tokens, symbols, completion, outline. Public API inside the compiler assembly, so it can reach the bound model without making the AST public. |
| `Lens.LanguageServer.Core` | Documents, position conversion, and the features as plain objects. No protocol dependency, `netstandard2.0` and `net8.0` — an in-process plugin for Visual Studio or Rider references this and skips the protocol. |
| `Lens.LanguageServer` | The protocol layer, on `OmniSharp.Extensions.LanguageServer`, over stdio. Reusable by any LSP client — Neovim, Zed, Sublime, Visual Studio, Rider. |
| `editors/vscode` | The VS Code extension: TextMate grammar, language configuration, client, and the server bundled into the `.vsix`. |

Covered by 27 analysis tests in `Lens.Test/Internals/ScriptAnalysisTest.cs` and a 21-check
end-to-end run over the real wire protocol (`editors/vscode/scripts/smoke-test.js`, `npm run
smoke-test`), which is the only place the protocol itself is exercised.

Five compiler bugs fell out of building it, none of them reachable from the compiler itself. They
are worth listing because they say something about what an editor asks that a compiler never does.

**Two were in the "analysis allocates no `AssemblyBuilder`" claim from Phase 3.5**, and were
invisible until something actually analysed a real script:

- `match` reserved its jump labels on the `ILGenerator` while *binding*, so a script containing one
  could not be analysed at all. Labels are an emission artefact and are now placeholders when there
  is nothing to emit into.
- The pattern rules materialized the type they test against, which forces a declared type's
  `TypeBuilder` into being. They take the entry now.

**Three were in the lexer**, and had been there since the beginning. A compiler reads each file once,
complete and usually valid; an editor reads every intermediate state of it, hundreds of times, and
most of those are neither.

- `AddLexem` gave every structural lexem (`NEWLINE`, `INDENT`, `DEDENT`, `EOF`) an `EndLocation` of
  nothing at all. The parser takes a node's end from the last lexem it consumed, so **every
  construct with an indented body** — a record, an algebraic type, a function with a block — ended
  nowhere. Nothing noticed, because a compiler reads a node's location only when reporting an error
  there and `DiagnosticBag` skips an unset end. An outline reads it for every declaration.
- `CurrChar` indexed the source without a bounds check, where its sibling `NextChar` has always been
  guarded. A file ending in a line of nothing but indentation — which is exactly what an editor
  holds from the moment you press Enter after `->` until you type the first character of the body —
  walked off the end of it.
- The dedent loop popped the outermost indentation level and then read the empty stack, so dedenting
  to a column no enclosing block sits at raised an `InvalidOperationException` instead of the
  "inconsistent indentation" error that was clearly intended.

Tolerance was hardened alongside: the tolerant lexer and parser now record *any* exception rather
than only a `LensCompilerException`, and `ScriptAnalyzer` fences each stage separately so that a
failure in one keeps what the stages before it produced. The compiler's own path catches neither, so
nothing is being hidden — but an editor that stops answering is worse than one that says less.

`AnalysisWithoutEmissionTest` covers the first two and `ScriptAnalysisTest` / `LanguageServiceTest`
the rest, the last of them by asserting the invariant an editor actually checks: an outline entry's
name must lie inside the declaration it names, or the editor rejects the whole outline.

## Goal

A language server providing syntax highlighting, type-aware completion, hover, go-to-definition,
diagnostics, and rename — usable from VS Code and any other LSP client.

## Why this is last

Every phase above changes the grammar and the semantic model. An LSP built first would be rewritten
after each one. The single exception is lexer-driven syntax highlighting, which is grammar-stable
enough to ship at any point and is broken out as step 1 for exactly that reason.

## Honest scoping

This is not one project, it is three, and they have very different costs:

| Step | What | Cost |
|---|---|---|
| 1 | Syntax highlighting + server skeleton | days |
| 2 | Tolerant parser, trivia, public AST | weeks |
| 3 | Editor-facing semantic API | weeks, mostly inherited from Phase 3.5 |
| 4 | Language features | weeks |

Step 1 delivers visible value immediately and is worth doing early regardless of the rest.

Steps 3 and 4 are what "type-aware completion and rename" actually cost, and they are gated on
[Phase 3.5](Phase3.5.md). Without that refactor, rename degenerates to text search and completion
cannot see through the destructive `Expand()` rewrites.

## Step 1 — Syntax highlighting and server skeleton

Ship this whenever. It has no dependency on any other phase.

- A TextMate grammar for LENS, for baseline colouring in VS Code. This is a `.tmLanguage.json` file
  and a regex exercise, not compiler work.
- A minimal LSP server on `OmniSharp.Extensions.LanguageServer`, in a new `Lens.LanguageServer`
  project targeting `net8.0` (the server is a tool, not an embedded library — it does not need the
  net47 leg).
- **Semantic tokens driven by the existing lexer.** `LensLexer` already classifies every lexem, so
  mapping `LexemType` to LSP semantic token types is nearly free and produces better colouring than
  the TextMate grammar can — correct keyword/identifier/string/number distinctions with no regex
  guessing.
- A VS Code extension shell to host it.

One catch: the lexer is indentation-sensitive and currently throws on malformed input. For
highlighting, wrap it so a lex failure degrades to "colour what we got, leave the rest plain" rather
than producing nothing. Mid-edit files are malformed by definition — this is the normal case, not the
exceptional one.

## Step 2 — Tolerant parser

**What shipped is the first half of this, and deliberately so.** Recovery and partial lexing are in;
trivia and a public AST are not, and turned out not to be needed for any feature in step 4.

- `new LensLexer(src, tolerant: true)` keeps the lexems it read before the failure and reports it in
  `Failure`, instead of producing nothing. Closing the open blocks moved out of the parse loop so
  that a failed run still hands the parser a well-formed stream.
- `new LensParser(lexems, tolerant: true)` records each failure in `Failures`, skips to the next
  statement at the same nesting level, and carries on. Recovery points are the top level and the
  inside of a block, which is where the language puts its boundaries anyway. A skipped statement
  inside a block leaves an `ErrorNode` behind, so the block still has a body and everything around
  it still binds.
- The safety argument for grafting this onto a backtracking parser: `Attempt` does not catch
  exceptions, so no speculative decision the parser makes has ever depended on one propagating.
  Turning a throw into a recovery cannot change which alternative gets picked — only how much of
  the file survives.
- The AST stayed internal. `Lens/Analysis` is the deliberate read-only view this section asked for,
  and it lives inside the compiler assembly so it can reach the bound model without freezing the
  node types as API.

Positions were the one thing that needed fixing rather than adding: a string literal's span started
after its opening quote and ended after its closing one, so colouring by lexem left the quotes bare.

The rest of this section is what was not done:

The current parser is unusable for an editor:

- It **throws on the first error** (`LensParser.Utils.cs:22`, `Ensure`), so a file with a typo yields
  no tree at all. An editor needs a tree for the 95% of the file that is fine.
- It **discards trivia**. Comments and whitespace are not in the AST, so formatting, comment-aware
  rename, and doc hover are impossible.
- The AST is **`internal`**. `NodeBase` and every node type are internal to the `Lens` assembly, so
  nothing outside can inspect a tree.

Work:

- **Error recovery.** Add recovery points at statement and block boundaries — natural here, since the
  language is indentation-delimited: on an error, skip to the next `NL` at the same or lower indent
  level and resume. Emit a placeholder/error node so the tree stays well-formed and positions stay
  meaningful. The existing `Attempt`/`Bind` backtracking machinery is a reasonable base.
- **Trivia.** Attach leading/trailing trivia to lexems, and thence to nodes.
- **Public AST surface.** Either make the node types public, or expose a separate read-only view.
  Making them public freezes them as API for embedders, which is a real cost given how much later
  phases change them. Prefer a deliberate read-only view designed for consumers, with the internal
  tree free to churn.
- **Full-fidelity positions.** `LocationEntity` already carries start and end. Verify every node
  actually sets both — nodes constructed by `Expr.*` factories during expansion generally do not, and
  once Phase 3.5 stops rewriting the tree that matters less, but parse-time nodes must be complete.

## Step 3 — Editor-facing semantic API

Mostly delivered by [Phase 3.5](Phase3.5.md). What that phase provides and this step consumes:

- binding without an `AssemblyBuilder` (3.5 stage 5) — an editor cannot emit an assembly per keystroke;
- a non-destructive bound model (3.5 stage 2) — positions in the source still map to constructs after
  analysis;
- **symbols with identity and reference lists** (3.5 stage 3) — the thing rename and
  go-to-definition are actually made of;
- multiple diagnostics (3.5 stage 1) — an editor shows all errors, not the first.

This step adds the query layer on top: given a document and an offset, find the enclosing node, its
bound type, its symbol, and the set of names visible at that point.

If Phase 3.5 has not happened, this step is where its entire cost reappears, plus rework.

## Step 4 — Language features

- **Diagnostics** — publish the binder's diagnostic bag on change, debounced.
- **Hover** — bound type plus symbol declaration, formatted.
- **Go-to-definition** — symbol declaration location. Trivial once symbols exist.
- **Completion** — two flavours, differing in difficulty:
  - *member completion* after `.` or `::`. The easy and most valuable one:
    `Context.Lookup.ResolveMethodGroup` / `ResolveField` / `ResolveProperty` already enumerate members
    of a `Type`. Given the receiver's bound type, this is close to free.
  - *identifier completion* in expression position — needs the visible-names query from step 3:
    locals in scope, declared functions, imported types, open namespaces.
- **Rename** — symbol reference list, rewritten as text edits. Correct by construction if symbols are;
  a nightmare otherwise. Must reject renaming into a name that already exists in scope.
- **Signature help** — LENS's whitespace-separated application makes "which argument am I on" harder
  than in a comma-and-parens language, and partial application means the arity is not fixed. Lower
  priority; do it last or not at all.

## Sharp edges

**Incrementality.** Full re-lex, re-parse, re-bind per keystroke is fine for scripts of the size LENS
targets — debounce at ~200ms and measure. Do not build incremental parsing speculatively. Revisit only
if measurement says so.

**Host-provided context — decided and implemented.** A LENS script's meaning depends on what the
*host* registered — `RegisterType`, `RegisterFunction`, `RegisterProperty`, `RegisterFunctionOverloads`.
A standalone editor knows none of it, so completion would miss exactly the API the script exists to
call.

The answer is a `declare` block at the top of the script, in the language rather than in a side file:

```
declare
    reference "FooBar.dll"
    let screen : ScreenManager
    var counter : int
    fun addNumbers:int (a:int b:int)
    type Baz = MyNamespace.Foo.Baz.SomeType
```

It has two readers and two meanings, and that is the whole point:

- **In the compiler** there is a host, so the block is an *assertion*. Every entry is checked against
  what was actually registered, and a mismatch is a compile error. See
  `Context.Declarations.cs`.
- **In the language server** there is no host, so the block *is* the environment. It is the only
  thing an editor has to go on, and because the compiler checks it, it cannot silently drift from
  what the host really provides.

Rules worth keeping in mind when building steps 3 and 4 on top:

- The check runs one way only. A host may serve many scripts and register far more than any one of
  them uses, so a registration that nothing declares is left alone rather than reported.
- Types are matched exactly, not by assignability: a property declared `object` would make the
  editor offer the wrong members.
- `let` *narrows*. Declaring `let` over a property the host registered with a setter makes it
  readonly for the rest of the script, so that the editor and the compiler agree on whether an
  assignment is legal.
- `declare type` is a definition rather than an assertion — given the referenced assemblies the
  compiler resolves the type itself, so the alias works whether or not the host also registered it.
- `reference` is inert in the compiler: the host has already chosen its assemblies via
  `RegisterAssembly`, so a path that does not resolve is a tooling problem. The server reports it as
  a warning; the compiler says nothing.
- Not supported: `pure`, and generic parameters on a declared function. Neither has anything in a
  registered `MethodInfo` that could be checked against.

Still to build on the server side: emitting the declaration header from a configured `LensCompiler`,
so embedders can keep script headers in sync mechanically. That needs the compiler to tell host
registrations apart from the standard library, which it currently does not — `MethodEntity.IsImported`
is true for both.

**Loading a declared assembly is not safe by default.** `Assembly.Load`/`LoadFrom` runs module
initializers and can run static constructors, in-process and fully trusted; there is no sandbox to
fall back on. A server that loads whatever DLL a workspace file names, on file open, is a drive-by
execution vector. Inspect references through **`System.Reflection.MetadataLoadContext`** instead — it
reads metadata only, never executes IL, and is disposable. Resolve paths relative to the workspace
root and refuse anything that escapes it.

The cost to plan for: `MetadataLoadContext` yields types from a separate reflection universe, which
are unusable for `Reflection.Emit` and never compare equal to runtime types. Fine for a server that
never emits, but the semantic layer has to tolerate metadata-only types — the same requirement as
"binding without an `AssemblyBuilder`", so fold it into step 3 rather than discovering it in step 4.

**Indentation sensitivity while typing.** A half-typed block is not just malformed, its indent
structure is ambiguous. `LensLexer` generates `INDENT`/`DEDENT` lexems; on a partial line these will
be wrong in ways that cascade. Step 2's recovery strategy has to be designed around this specifically,
not bolted on.

**Two consumers of the parser, one grammar.** The compiler wants to fail fast; the editor wants to
limp on. Resist forking. One tolerant parser with a strict mode (promote first diagnostic to an
exception) is maintainable; two parsers will diverge within a release.

## Acceptance criteria

- Step 1 ships independently and is useful on its own.
- Editing a file with a syntax error in the middle still yields correct highlighting, completion, and
  hover in the rest of the file.
- Member completion after `.` on a host-registered type lists the right members, filtered by safe mode
  when active.
- The environment the server builds from a `declare` block is the same one the compiler checks that
  block against: a script that completes and hovers cleanly in the editor compiles against the host.
- No assembly named by `declare reference` ever has its code executed by the server.
- Rename of a local, a function, and a record field updates exactly the right occurrences — verified
  against a script containing shadowing and a same-named member on an unrelated type.
- No `AssemblyBuilder` is created during any editor operation (assert it).
- Analysis of a ~1000-line script stays under a debounce budget; measure, do not assume.

## Limitations of what shipped

Stated rather than discovered later:

- **Rename of a global is matched by name.** Locals, arguments and record fields have exact
  reference lists — locals from the symbols binding produces, fields from the receiver's bound type,
  so two records with a field of the same name are never confused. Functions, records and algebraic
  types have no such list, and are matched instead by every identifier spelling the name that is not
  reached through a `.` and not claimed by a local. LENS has one global namespace and no methods on
  user types, which makes that exact in practice, but it is a rule about the language rather than a
  fact about the tree.
- **Renaming is refused while the file does not parse.** The set of names a rename must avoid comes
  out of binding, and binding a tree with a hole in it does not produce all of them. A rename that
  silently captures a name is the one failure mode of this feature that corrupts working code.
- **Rename does not check scope, only the file.** Renaming into a name used anywhere in the script
  is refused, which turns some legal renames away. The cost of being wrong the other way is worse.
- **Completion after a `.` costs a second analysis.** `foo.` does not parse, so the rest of that one
  line is blanked and the file is analysed again to ask what `foo` is. Every position before the dot
  is unchanged, so the answer is about the real code. Scripts are small and this is debounced;
  measure before optimising.
- **Comments are not semantic tokens.** The lexer discards them, so the TextMate grammar colours
  them. This is the right split — nothing about a comment needs the compiler.
- **`declare reference` does not load anything.** The server checks that the file exists and warns
  when it does not; it never opens it. Loading a workspace DLL runs its module initializers in
  process, and there is no sandbox left in .NET to do that safely. The hardening path is
  `MetadataLoadContext`, and the reason it is not there yet is in the sharp edge below: the
  compiler's type model is rooted in runtime types, so a metadata-only universe means moving the
  whole binder with it.
- **A property slot per analysis.** `GlobalPropertyHelper` keeps a global list indexed by context
  id and never reuses a slot, so each reading of a file leaves a null entry behind. `ScriptAnalysis`
  is disposable and releases the properties, but the slot itself stays. It is eight bytes per
  keystroke; worth knowing, not worth fixing yet.
- **Signature help and formatting were not built.** Both were already flagged as low priority here.
  Multi-file support is still a language design question rather than an editor one.

## Out of scope

- Debugger / DAP support. Separate project. Would need sequence points and `ISymbolDocumentWriter`,
  which the emitter does not currently produce.
- Formatting. Needs full-fidelity trivia (step 2 provides the input) but is its own body of work.
- Refactorings beyond rename.
- Multi-file projects. LENS scripts are single compilation units; changing that is a language design
  decision, not an LSP one.
