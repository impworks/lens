# Phase 0 — Target framework modernization

## Goal

Get the solution onto supported target frameworks and a supported test stack, **without dropping
.NET Framework**, so that later phases can rely on a modern BCL where one is available.

## Current state

| Project | TFM(s) | Notes |
|---|---|---|
| `Lens` | `net45;netcoreapp2.0` | `LangVersion 7.3`, `GeneratePackageOnBuild`, version 4.2.1 |
| `Lens.Test` | `net47;netcoreapp2.0` | NUnit 3.10.1, NUnit3TestAdapter 3.10.0, Test.Sdk 15.7.2 |
| `ConsoleHost` | `net45` | Exe |
| `GraphHost` | `net452` | WinExe, WPF, InteractiveDataDisplay.WPF |
| `GraphicScript` | `net45` | WinExe |

Two concrete problems, both verified:

- `dotnet restore` emits **NU1903** (known high-severity vulnerability) for `Microsoft.NETCore.App`
  2.0.0, on every TFM leg of both `Lens` and `Lens.Test`.
- `dotnet test` **aborts** on the `netcoreapp2.0` leg — the 2.0 runtime is long gone and will not be
  installed on any current machine. Only the `net47` leg actually runs. Half the test matrix is
  silently dead.

The `net45` leg does still build clean under SDK 10 thanks to
`Microsoft.NETFramework.ReferenceAssemblies`.

## Target state

| Project | TFM(s) |
|---|---|
| `Lens` | `net47;netstandard2.0;net8.0` |
| `Lens.Test` | `net47;net8.0` |
| `ConsoleHost` | `net47;net8.0` |
| `GraphHost` | `net47` |
| `GraphicScript` | `net47` |

Rationale for each:

- **`net47`** — the retained .NET Framework baseline, per the project constraint. Bumping `Lens`
  from `net45` costs nothing (the test project is already `net47`) and buys the full 4.7 BCL.
- **`netstandard2.0`** — keeps every embedder that is on .NET Core 2.1–3.1 or .NET 5–7 working
  without a dedicated leg. This matters because LENS ships as a NuGet package and embedders are
  exactly the audience least able to retarget on demand.
- **`net8.0`** — the current LTS, and the leg where later phases get modern BCL surface
  (`ValueTask`, `IAsyncEnumerable`, `System.Runtime.CompilerServices` async builders).

The WPF/WinForms hosts stay on Framework. Porting them to `net8.0-windows` is possible but is pure
scope creep with no bearing on the compiler.

### Why `net8.0` *and* `netstandard2.0`

This looks redundant and deserves a recorded answer, because it will be questioned again.

**netstandard2.0 has no Reflection.Emit whatsoever.** Verified: a bare netstandard2.0 project fails
to resolve `AssemblyBuilder`, `ModuleBuilder`, `TypeBuilder`, and `ILGenerator`. It compiles only
after adding `System.Reflection.Emit` 4.7.0 and `System.Reflection.Emit.ILGeneration` 4.7.0 — legacy
out-of-band packages, last shipped around 2020 and effectively frozen. On `net8.0` the entire API is
inbox.

So the `net8.0` leg buys exactly two things, and it is worth being precise that neither is
functionality:

1. **Packaging hygiene.** Without it, every modern consumer of the NuGet package drags in two frozen
   `System.*` packages. This phase exists to get *off* deprecated dependencies carrying advisories
   (NU1903); shipping netstandard2.0-only would reintroduce that same category of dependency for the
   majority of consumers.
2. **A home for TFM-conditional features later** — Phase 4's optional `IAsyncEnumerable`, any future
   `PersistedAssemblyBuilder` work. Speculative, since both are currently out of scope.

It notably does **not** buy the ability to test on a modern runtime: `Lens.Test` can target `net8.0`
and reference the `netstandard2.0` build of `Lens`. The acceptance criteria below are satisfiable
with two legs.

Cost is one extra build leg and **no** extra `#if` complexity, since `netstandard2.0` and `net8.0`
sit in the same conditional bucket and behave identically. If the build matrix is felt to be too
wide, dropping `net8.0` is a safe, reversible decision that costs nothing today.

## Design

### Compilation constants

Today: `NET_CLASSIC` (net45) and `NET_CORE` (netcoreapp2.0), used in only four files —
`Context.cs`, `Context.Import.cs`, `Context.Compilation.cs`, `LensCompilerOptions.cs`. All of them
guard the same thing: **`AssemblyBuilderAccess.RunAndSave` and `AssemblyBuilder.Save`**, which exist
only on .NET Framework.

Keep the split, but redefine it by capability rather than by runtime family:

- `NET_CLASSIC` — `net47` only. Has `AppDomain.CurrentDomain.DefineDynamicAssembly` with
  `RunAndSave`.
- everything else (`netstandard2.0`, `net8.0`) — `AssemblyBuilder.DefineDynamicAssembly`, run-only.

This is exactly the existing `#if` structure, so the change is a `.csproj` condition, not a code
change.

### Save-to-disk on modern .NET: deliberately deferred

.NET 9 introduced `PersistedAssemblyBuilder`, which would restore `Options.AllowSave` on Core. It is
**not** in scope here, because it is not a drop-in:

- A `PersistedAssemblyBuilder` **cannot be executed**. Framework's `RunAndSave` gives you one builder
  that both runs and saves; the modern API forces you to choose per-instance.
- So supporting it means either compiling twice, or making `AllowSave` and "run this script" mutually
  exclusive on Core — a real API-surface decision, not a port.

Decision: on non-Framework TFMs, `AllowSave` continues to be unavailable. Revisit only if an embedder
asks. Document the limitation in the README rather than papering over it.

### Test stack

Move to `Microsoft.NET.Test.Sdk` 17.x and `NUnit3TestAdapter` 4.x, but **stay on NUnit 3.14**.

NUnit 4 removes the classic assertion model (`Assert.AreEqual` and friends) in favour of
`Assert.That`. The test suite leans on the classic model throughout. Migrating it is a large,
entirely mechanical diff that would bury the real changes in this phase and in every phase after it,
for no functional gain. NUnit 3.14 is still supported and runs fine on the 4.x adapter. Revisit
separately if ever.

### Language version

`LangVersion 7.3` → `latest`. The compiler source itself gets pattern matching, switch expressions,
nullable annotations, etc. Two caveats on the `net47` leg:

- Features needing runtime support (`Index`/`Range`, default interface members, static abstracts)
  are unavailable or need polyfills. Don't reach for them.
- Nullable reference types are opt-in per file; do **not** enable `<Nullable>enable</Nullable>`
  solution-wide in this phase. It would produce hundreds of warnings across 26k LOC and has nothing
  to do with retargeting.

## Work breakdown

1. **`Lens.csproj`** — retarget to `net47;netstandard2.0;net8.0`; rework the `Choose`/`When` block so
   `NET_CLASSIC` is `net47` and the else-branch covers both other TFMs; drop the explicit
   `System.Reflection.Emit.ILGeneration` and `Microsoft.CSharp` package references where the TFM
   provides them inbox (`net8.0` does; `netstandard2.0` still needs `Microsoft.CSharp` for the
   `dynamic` used by `NodeBase.ConstantValue`); bump `LangVersion`.
2. **`Lens.Test.csproj`** — retarget to `net47;net8.0`; update Test.Sdk/adapter; delete the
   `netcoreapp2.0` conditional property groups.
3. **Host projects** — retarget as per the table. `GraphHost` may be able to drop the
   `System.ValueTuple` package reference on net47.
4. **Verify assembly discovery still works on `net8.0`.** See risks below — this is the one item in
   the phase that is not mechanical.
5. **Safe mode review** — `Context.SafeMode` / `SafeModeSubsystem` allow-lists were written against
   the Framework BCL. Confirm the type names still resolve on Core and that nothing became
   unexpectedly reachable.
6. **CI** — add a GitHub Actions workflow building and testing all legs on `windows-latest`. There is
   currently none. This phase is the moment to add it, because it is the first time the full matrix
   can actually run.
7. **Package metadata** — bump `Version` to 5.0.0, update `Copyright` (still says 2018).

## Risks and sharp edges

**Assembly discovery on modern .NET** — `ReferencedAssemblyCache` does two things that behave
differently on Core:

- It loads `mscorlib`, `System`, and `System.Core` by full four-part name with the Framework's
  `b77a5c561934e089` public key token. These exist on Core as type-forwarding facades and the loads
  do generally succeed — but the `try { } catch { }` around them means a failure is silent, and the
  resulting compiler would fail much later with a confusing "type not found". Add a debug assertion
  or a diagnostic so a miss is visible.
- It seeds from `AppDomain.CurrentDomain.GetAssemblies()`, i.e. *currently loaded* assemblies.
  Framework and Core both load lazily, but Core's split of the BCL into many small assemblies makes
  it much more likely that e.g. `System.Linq` simply is not loaded yet when the `Context` is
  constructed. Symptom: a script using LINQ fails to resolve an extension method depending on what
  the host happened to touch first. Mitigation: explicitly `Assembly.Load` the assemblies backing the
  default namespaces (`System`, `System.Linq`, `System.Text.RegularExpressions`) rather than relying
  on them being present.

  This is the most likely source of "works on net47, fails on net8.0" test failures in this phase.
  Expect to spend time here.

**`Type.GetType`/name-based resolution** — `TypeResolver` resolves by string signature. Framework
and Core disagree on the canonical assembly for several common types. The existing
`TypeResolverTest` should catch regressions; run it early on the new leg.

**`net45` → `net47` for hosts** — trivial, but `GraphHost` depends on `InteractiveDataDisplay.WPF`
1.0.0, which was migrated only recently (commit `d729bab`). Re-verify it still resolves.

## Acceptance criteria

- `dotnet build` succeeds on every project and every TFM with **zero** NU19xx advisories.
- `dotnet test` runs and passes on **both** `net47` and `net8.0` — no aborted legs. Record the test
  count per leg; they must match.
- `ConsoleHost` runs a non-trivial script (one using LINQ, records, and pattern matching) identically
  on both legs.
- CI is green on a clean checkout.
- README documents that `AllowSave` is .NET Framework only.

## Out of scope

- `PersistedAssemblyBuilder` / save-to-disk on Core (see above).
- NUnit 4 migration.
- Solution-wide nullable reference types.
- Porting the WPF hosts off .NET Framework.
