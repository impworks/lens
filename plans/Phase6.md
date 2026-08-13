# Phase 6 — Language server

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

**Host-provided context.** A LENS script's meaning depends on what the *host* registered —
`RegisterType`, `RegisterFunction`, `RegisterProperty`, `RegisterFunctionOverloads`. A standalone
editor knows none of it, so completion would miss exactly the API the script exists to call. This is
the deepest design problem in the phase and it has no free answer.

Options: a project-file/manifest declaring assemblies and registrations; a host-side hook that dumps
its registrations for the server to load; or convention over a well-known config file. **Decide this
before step 4** — completion quality depends entirely on it, and it may influence the embedding API
(e.g. making registrations declarative enough to be serialised).

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
- Rename of a local, a function, and a record field updates exactly the right occurrences — verified
  against a script containing shadowing and a same-named member on an unrelated type.
- No `AssemblyBuilder` is created during any editor operation (assert it).
- Analysis of a ~1000-line script stays under a debounce budget; measure, do not assume.

## Out of scope

- Debugger / DAP support. Separate project. Would need sequence points and `ISymbolDocumentWriter`,
  which the emitter does not currently produce.
- Formatting. Needs full-fidelity trivia (step 2 provides the input) but is its own body of work.
- Refactorings beyond rename.
- Multi-file projects. LENS scripts are single compilation units; changing that is a language design
  decision, not an LSP one.
