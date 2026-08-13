# Phase 3.5 — Split binding from emission

## Status

**Done.** Five stages, and a sixth piece of work that stage 5 turned out to require.

| Stage | State | Commit |
|---|---|---|
| 1 — Diagnostics instead of exceptions | Done | `4c816d2` |
| 2 — Move binding results off the nodes | Done | `72b3f61` |
| 3 — Symbols | Done | `ab68f30` |
| 4 — Split closure analysis from closure emission | Done | `c01b005` |
| 5 — Split binding from emission in the type system | Done | see sub-steps |

Acceptance criteria, all met: the suite is green after every commit (717 tests, net47 and net8.0,
zero warnings; 730 now, with the record-equality fix); three independent errors report three diagnostics; the same parse tree binds twice
in two contexts with identical results; analysing a script allocates no `AssemblyBuilder`, asserted
rather than asserted about; compile time is within the ~15% budget (about +5%, measured
interleaved - see the correction below).

## Stage 5: the type entry model

**Decision taken: option (a)** — a symbol layer for user-defined types, with `System.Type` obtained
only at emission. Option (b) (throwaway modules) was rejected on the grounds that it leaks a
`ModuleBuilder` per analysis; that premise turned out to be wrong, and the assembly is collectible
now as well, but the entry model was worth having for its own sake.

It also fixes the long-standing `NotSupportedException` problem: inspecting a user-defined type, or
a constructed generic with a builder anywhere in its argument tree, used to route through a
hand-written fallback. There were **seventeen** such handlers at the peak; there are eight now, and
four of those are the model's own tolerance for host types made of unfinished parts.

### Sub-steps

All on branch `phase3.5/type-entries`; 1a is also merged to `v5`.

| Step | State | Commit |
|---|---|---|
| 1a — `TypeEntry` model, `ReflectedTypeEntry`, canonicalising cache | Done | `8bdb26e`, `679cd29` |
| 1b — member wrappers hold entries | Done | `5ae1417` |
| 1c — type matching and distance on entries | Done | `a90b493` |
| 1d — `Context.ResolveType` and node `Resolve()` on entries | Done | `dba7301`, `33e915c` |
| 1e — `Local`, `FunctionArgument`, `Scope`, the `TypeEntity` family | Done | `ccafa09`, `aba487c` |
| 2 — `TypeEntityEntry` for user-defined types | Done | `9efe146` |
| 3 — `GenericParameterEntry` | Done | `d5d1fb9` |
| 4 — lazy `AssemblyBuilder`, analysis entry point, signature/builder split | Done | `03df5b8`, `560ed9f` |
| Collectible assembly | Done | `0143253` |
| Tier 1 — composite entries, entry-space substitution, `TypeResolver` | Done | `2050c5f`, `d4e545a` |
| Tier 2 — lazy member reflection objects, single-path member lookup | Done | `ad93c01` |

### Step 4, and the tiers that finished it

`Context.Analyze(nodes)` reads declarations, binds every body, works out captures and collects
diagnostics, and defines no assembly. `AnalysisWithoutEmissionTest` asserts that across 13 cases,
including the ones that used to fail: a declared type inside a generic, an array of a type
parameter, a generic record, a generic record with a composite field, and a generic algebraic type.
Earlier revisions of this document listed those as broken - they reported a false
`LE3114 "type could not be resolved"` or threw outright. They work now.

What it took, beyond the original step 4:

- **Tier 1.** `ConstructedTypeEntry` (a definition plus arguments), `ArrayTypeEntry` and
  `ByRefTypeEntry`, answering by substituting arguments into what the definition says and
  materializing lazily. `ConstructedTypeEntry.SubstituteInto` is the entry-space counterpart of
  `GenericHelper.ApplyGenericArguments`. Then `TypeResolver` and `Context.LookupTypeForResolver`
  converted, so signature strings resolve into entries.
- **Tier 2.** Member wrappers resolve structurally and produce their `MethodInfo` / `FieldInfo` on
  demand through `LazyMember`. Member lookup is one path for analysis and emission.

**The dynamic assembly is also collectible now** (`AssemblyBuilderAccess.RunAndCollect`), verified
by `AssemblyReclaimTest` on both runtimes: ten assemblies built, dropped and reclaimed, and a
still-callable script keeps its own alive. Every compilation used to leak one for the life of the
process. This was meant as the safety net for whatever coverage gap remained; the gap closed
anyway, but a compiler that does not leak an assembly per run is worth having regardless.

### What remains

Nothing blocking. Four `NotSupportedException` handlers survive outside the entry model, each on a
surface no acceptance criterion touches, and each measured to fire rather than guessed at:
`ReflectionHelper.ResolveMethodGroup` as reached by `WrapDelegate` (27 firings across the suite),
`ResolveIndexer` (8), `HasDefaultConstructor` (1), and the CLR-side interface cache in
`TypeResolutionContext.FindInterfaces` (3050). Converting `WrapDelegate` and the indexer path would
retire the first two. Four more live inside `ReflectedTypeEntry`, which is where they belong.

Also open, both flagged in the commits rather than fixed: generic-method inference still has two
engines, one CLR-side (the only one that infers through a lambda) and one entry-side (the only one
that works before emission); and `TypeEntity._selfType` is still a reflected
`TypeBuilderInstantiation` where `ResolveType("Foo<T>")` now yields a `ConstructedTypeEntry`, so the
two do not compare equal. Nothing mixes them today.

### Cost, and a correction

**Measured interleaved, back to back on the same machine: about +5%.** `v5` (stages 1-4, before the
entry model) ran the 9 KB generated script at 34.9, 37.1, 38.7 and 39.1 ms; the finished branch ran
it at 38.1, 38.5, 40.5 and 41.1 ms. The ranges overlap.

Earlier revisions of this document reported +14.6% and then +25%. Both were wrong. They compared
measurements taken hours apart, and the machine drifted by roughly that much across the session:
re-measuring `v5` at the end gave 35-39 ms where it had given 30.1 ms at the start. Any performance
claim here has to come from interleaved runs. A number from one sitting compared against a number
from another says more about the machine than about the compiler.

Headroom remains if it is ever wanted: `TypeResolutionContext._instantiations` now serves exactly
one reuse across the whole suite, since `ConstructedTypeEntry` makes instantiations canonical by
construction; and the `IsStable` guard in `CachedDistance` was measured never to fire.

### Where the boundary sits now

`grep -ro "TypeEntryCache.Of\|Materialize()" --include=*.cs SyntaxTree Compiler` counts the
scaffolding, and its two halves say different things:

- `TypeEntryCache.Of` — a place that still produces a raw `Type` and has to wrap it. Peaked at 192
  and is 163 now.
- `Materialize()` — a place that still consumes a raw `Type`. Peaked at 190 and is 123 now.

`Materialize()` going to zero *outside emission* would be the ideal, and it will not get there,
because emission is a legitimate consumer: `EmitterExtensions` and the entity builders speak
`System.Type` by design and always will. What is left divides into that, plus the two unconverted
surfaces named above (`WrapDelegate`, the indexer path) and the `Expr` factory overloads that take a
`Type` for the benefit of the parser and the auto-generated code.

### Changes that are not literally behaviour-preserving

Three, all in the same direction — the model answering where reflection used to throw. Each is
a fix, and each is a behaviour change inside a refactor, so they belong in a review:

1. `ReflectedTypeEntry.BaseType` recovers the base type of a builder-backed instantiation by
   applying the arguments to the definition's base, where the old private `GetBaseType` returned
   null. `IsDerivedFrom` and the declared-generic ancestor walk can now find an ancestor they
   used to stop short of. This *widens* the set of legal conversions, which can change which
   overload wins.
2. `GenericParameterAttributes` and `GenericParameterConstraints` return empty instead of
   throwing.
3. Seven hand-built generic instantiations now route through `TypeResolutionContext`, so an
   instantiation over a script-declared type is interned rather than freshly allocated per call.
   Previously such instances compared unequal to their siblings.

4. `MethodWrapper.ArgumentTypes` of a generic host method are now always substituted. The old
   success path assigned the candidate's raw parameter entries, so an open `T` survived into
   `ArgumentTypes` while `ReturnType` was closed; only the `NotSupportedException` fallback
   substituted. `InvocationNode` emits `Expr.Cast(arg, destTypes[idx])` from those, so this can
   change a cast target where an open `T` used to reach IL. It looks like a latent bug being closed.
5. Wrappers reached through a declared instantiation now carry `ConstructedTypeEntry` argument and
   return types during emission where they used to carry `ReflectedTypeEntry` over a
   `TypeBuilderInstantiation`. The same types, different entry objects, and the two do not compare
   equal - so this makes emission agree with analysis, but it is a change.
6. `new()` constraint checks test `!ContainsDeclared` rather than `!IsDeclared`, which slightly
   relaxes the check for `List<SomeRecord>` in exchange for not materializing during analysis.

Two bugs were found and fixed along the way, both latent and both masked because analysis never
reached the code that would have exposed them: `TypeEntityEntry.GenericArguments` returned nulls for
parameters without builders, so substituting against a declared generic definition silently did
nothing; and `ConstructedTypeEntry.SubstituteInto` skipped generic type *definitions*, which a member
signature routinely names - the type of `EqualityComparer<>.Default` is spelled that way - so the
member got resolved on the open definition.

Consequence worth acting on: a surviving `try/catch (NotSupportedException)` around a now-entry-typed
access may well be dead, and each one should be re-read for an intended "abort the walk" meaning
before being deleted rather than swept blindly.

### A bug found while testing, fixed separately

Records shadowed `Equals` and `GetHashCode` rather than overriding them, because
`MethodEntity.PrepareSelf` emitted every virtual method with `MethodAttributes.NewSlot`. One flag
was doing double duty: `MainType.Run` is virtual because it implements `IScript.Run`, where a new
slot is right, and the generated members are virtual because they override a base class method,
where it is wrong. `MethodEntity.IsOverride` separates them. Commit `c477c5f`, with
`RecordEqualityTest` covering it.

This is a behaviour change, not a refactor, and deliberately its own commit. Records now compare
and hash by value everywhere - dictionary keys, `Contains`, `Distinct`, nested record fields -
where before they compared by reference through any path that did not know the exact static type.

### Known trap in the model

`entry == null` inside the `TypeEntry` hierarchy re-enters the `==` operator and stack-overflows.
Internal null tests must use `ReferenceEquals`. Consumer code outside the hierarchy is fine.
There is a comment on the operator saying so.

**The scope is larger than this plan estimated.** The plan expected the blast radius to be
"`TypeEntity`, `Context.Lookup`, and the nodes that construct or access user-defined types".
Three couplings found while landing stages 1-4 say otherwise:

1. **User-defined types.** As documented: `Context.ResolveType` hands out
   `TypeEntity.TypeInfo`, which is the `TypeBuilder`, and `Context.FindDeclaredType`
   recognises a user type by testing `type is TypeBuilder`. The binding-time *identity* of a
   user type is its builder.

2. **Generic parameters, which Phase 3 added after this plan was written.**
   `GenericParameterEntity.Builder` is a `GenericTypeParameterBuilder`, handed out by
   `Context.ResolveType` and `LookupTypeForResolver` whenever a signature names `T`. So every
   generic parameter of every user-declared function, type or record is also an emission
   artefact serving as a binding token. The symbol layer has to cover these too, and they
   reach much further into `GenericHelper` and `ReflectionHelper` than records do.

3. **Binding needs an `ILGenerator` today, independently of the type question.**
   `PrepareEntities` runs before `TransformTree`, and `MethodEntity.PrepareSelf` creates the
   `MethodBuilder` and its `ILGenerator` in the same step as it resolves the signature. Worse,
   `Scope.DeclareImplicit` — called from a dozen `Expand` implementations — declares an IL
   local on the spot. So even a script that declares no types at all cannot currently be bound
   without an assembly.

Consequently stage 5 wants to be landed as its own sequence, not as one change:

- **5.1** Split signature resolution from builder creation in `PrepareSelf`, so an entity can
  have a resolved signature with no `MethodBuilder`.
- **5.2** Give implicit locals a symbol at binding time and a `LocalBuilder` at emission time,
  like every other local since stage 4. (Note: `DeclareImplicit` currently declares an IL
  local that `Scope.EmitSelf` then declares a second time, wasting a slot per implicit
  variable. Worth fixing whether or not 5.2 happens.)
- **5.3** Replace `type is TypeBuilder` recognition with an explicit token-to-entity registry,
  so the token's runtime class stops being load-bearing.
- **5.4** The symbol layer proper: a token type for user-defined types and for generic
  parameters, substituted for real `System.Type`s at emission.
- **5.5** Lazy `AssemblyBuilder`, the binding/emit context split, and the analysis-only entry
  point that Phase 6 consumes.

Only 5.5 moves the acceptance criterion, and it is worth nothing before 5.1-5.4.

## Goal

Separate *analysing* a script from *emitting IL* for it, so that the analysis can run repeatedly,
non-destructively, without an `AssemblyBuilder`, and can report more than one error.

This is the single refactor that most improves the codebase. It is scheduled here because it is a
prerequisite for Phase 6 and a large convenience for Phase 4, and because doing it before Phase 3
would mean doing it twice (Phase 3 changes what resolution means).

## Why the current design blocks things

The compiler resolves, rewrites, and emits in one entangled pass. Five specific couplings:

1. **`Context`'s constructor creates the `AssemblyBuilder` and `ModuleBuilder` eagerly**
   (`Context.cs:57–78`), plus the main `TypeEntity` and `MainMethod`. You cannot analyse a script
   without building an assembly. For an editor that re-analyses on every keystroke, this is fatal.
2. **`Resolve` memoizes into a field on the node** (`NodeBase.CachedExpressionType`). The parse tree
   carries per-compilation state, so it can only be bound once. `LambdaNode.SetInferredArgumentTypes`
   even clears the cache by hand to work around this.
3. **`Transform` rewrites the parse tree in place** via `child.Setter(sub)` using the `Expand()`
   result. After compilation the tree no longer resembles the source, so no editor feature can map a
   position back to a construct.
4. **`ProcessClosures` mixes analysis with emission.** Deciding *which* locals are captured is
   analysis; `Scope.CreateClosureType` creating a `TypeEntity` and `FinalizeSelf` calling
   `gen.DeclareLocal` are emission. They currently happen in the same walk, against a live
   `ILGenerator`.
5. **Errors are exceptions.** `Context.Error` and `NodeBase.Error` throw `LensCompilerException`
   immediately, so compilation stops at the first problem. An editor needs all diagnostics; a build
   log wants them too.

## Target architecture

Three artefacts instead of one entangled pass:

```
source ──lex/parse──▶  parse tree      (immutable after parsing)
                            │
                       ──bind──▶       bound model   (side tables: types, symbols, expansions,
                            │                         captures, diagnostics)
                            │
                       ──emit──▶       IL            (needs AssemblyBuilder; consumes bound model)
```

**The governing rule: the parse tree becomes immutable once parsing is done.** Everything binding
learns lives in side tables keyed by node, owned by a binding context. This one rule delivers most of
the benefit — it makes binding repeatable, makes the tree safe to share across threads, and makes a
bound-tree-to-bound-tree rewrite (Phase 4) possible.

## Staged work breakdown

Do these in order. Each is independently shippable with the suite green, which matters for a refactor
of this size.

### Stage 1 — Diagnostics instead of exceptions

Introduce a diagnostic bag on the context: severity, message, location, code. Convert `Context.Error`
and `NodeBase.Error` to record a diagnostic and, where the node can produce a plausible type, return
an error placeholder type and keep going. Where it cannot, throw a control-flow-only exception caught
at the statement boundary so the next statement still gets analysed.

Keep `LensCompilerException` as the public API surface — throw the first diagnostic from
`LensCompiler.Compile` — so embedders are unaffected.

Payoff on its own: multiple errors per compile. Independently useful, low risk, good first step.

### Stage 2 — Move binding results off the nodes

- `CachedExpressionType` → `Dictionary<NodeBase, Type>` on the binding context.
- `Expand()` results → an expansion side table, instead of `child.Setter(sub)` mutating the parent.
  Emission walks "the bound tree", which means: for each node, if an expansion exists, emit that
  instead.
- Audit every other mutable field on a node that binding writes. `LambdaNode._method`,
  `LambdaNode._inferredReturnType`, `GetMemberNode`'s resolved `_field`/`_property`,
  `GetIdentifierNode._localConstant`, and the `IPointerProvider.RefArgumentRequired` /
  `PointerRequired` flags are all in this category. They move to the side tables too.

This is the biggest stage and the one that touches the most files. It is also the one that pays for
Phase 4 and Phase 6.

Watch performance: a `Dictionary<NodeBase, T>` lookup per node per phase is slower than a field read.
Use reference equality comparers, and if it shows up in the measurements (`LensCompiler.Measurements`
already times the phases), give nodes an integer id assigned at parse time and use arrays.

### Stage 3 — Symbols

Give every declaration a stable symbol object with identity, a declaration location, and a reference
list: locals, function declarations, records and their fields, type labels, arguments.

Today a `Local` is created during resolution, copied on lookup (`FindLocal` returns
`local.GetCopy()`), and discarded. There is no object that means "the variable `x` declared on line
12", which is precisely what go-to-definition and rename need.

Binding populates the reference list as it resolves identifiers. That single change is what makes
Phase 6's rename possible; without it rename degenerates to text search.

### Stage 4 — Split closure analysis from closure emission

Split `Scope`'s responsibilities:

- **analysis**: which locals are captured, by which lambda, and what the capture-scope nesting is —
  computable from the bound model with no `ILGenerator` present.
- **emission**: creating the closure `TypeEntity`, its fields, its parent-scope field, and
  `DeclareLocal` calls.

Phase 4 needs exactly this split, because a state machine hoists locals into a state class the same
way a closure hoists them into a closure class, and the two must **merge** rather than nest. Doing
the split here means Phase 4 inherits it.

### Stage 5 — Split the context class

`Context` becomes two: a binding context (namespaces, assembly cache, type resolver, symbol tables,
diagnostics, generic environment from [Phase 3](Phase3.md#step-1-introduce-a-resolution-context))
and an emit context (module builder, type builders, IL generators, `CurrentMethod`).

The `AssemblyBuilder` is created when emission starts, not in a constructor. At that point
`LensCompiler` can expose an analysis-only entry point — which is the API Phase 6 consumes.

## Sharp edges

**The user-defined-type chicken and egg.** `Context.ResolveType` resolves user-defined types through
`ExternalLookup`, which returns `ent?.TypeBuilder` — a `TypeBuilder`, an emission artefact. So today
binding a script that mentions a record requires having already begun emitting it.

This is the hardest part of the phase and it needs a decision. Two options:

- **(a) A symbol layer for user-defined types** — a `TypeEntity` that can answer questions about
  fields and methods before a `TypeBuilder` exists, with `System.Type` obtained only at emission.
  Correct, and what Phase 6 ultimately wants, but it means every `Type`-typed field in the resolver
  becomes an abstraction, which is a very large diff.
- **(b) Keep `TypeBuilder`s, but on a throwaway module during analysis** — cheap, preserves all
  existing code, but leaks a `ModuleBuilder` per analysis and so is unusable for an editor that
  re-analyses continuously.

**Recommendation: (a), but scoped.** Only user-defined types need the abstraction — imported host
types are real `System.Type`s and should stay that way. That keeps the blast radius to `TypeEntity`,
`Context.Lookup`, and the nodes that construct or access user-defined types. Decide this explicitly
before starting Stage 5; do not let it be decided by accident mid-refactor.

**Scope of the diff.** This phase touches nearly every file in `SyntaxTree`. Land it in the five
stages above, each green, rather than as one change. Resist bundling behaviour changes into it — a
refactor that also fixes bugs is a refactor nobody can review.

**`dynamic` constants.** `NodeBase.ConstantValue` is `dynamic` and `IsConstant` folding happens during
resolution. Constant folding is analysis and should move into the bound model like everything else,
but the `dynamic` dispatch makes it harder to reason about. Consider replacing `dynamic` with a small
tagged value type while you are in there — optional, but this is the cheapest opportunity.

## Acceptance criteria

- The full suite passes, unchanged in behaviour, after every stage.
- A script with three independent type errors reports three diagnostics, not one.
- The same parse tree can be bound twice, in two different contexts, producing identical results —
  a direct test of the immutability rule, and the thing Phase 6 depends on.
- Analysing a script allocates no `AssemblyBuilder` (assert it).
- Compilation time does not regress by more than ~15% on `ParserLargeTest`-scale input; measure with
  the existing `LensCompiler.Measurements` hooks before and after.

## Out of scope

- Incremental / partial re-binding. Full re-bind per analysis is fine; Phase 6 can debounce.
- Error recovery in the *parser* — that is [Phase 6 step 2](Phase6.md#step-2-tolerant-parser).
  This phase makes the *binder* resilient, not the parser.
