# LENS modernization roadmap

This folder holds the per-phase implementation plans for bringing LENS up to date with the
modern .NET ecosystem.

## Design constraints

These apply to every phase and were set explicitly:

1. **.NET Framework support is retained.** LENS must keep working on traditional .NET FW (net47).
   No plan may assume a Core-only BCL surface.
2. **LENS is glue code.** The language exists to drive types provided by the *host*. It is not
   trying to become a general-purpose OO language. When a feature can be expressed by consuming
   host types instead of by declaring new topology, prefer the former.
3. **Reflection.Emit is the only backend.** Its limitations on class topology are a hard boundary,
   not an obstacle to work around.
4. **Roughly C# in semantics.** Where a feature exists in C#, match its behaviour and its edge cases
   unless there is a specific reason not to.

## Phases

| Phase | Title | Status | Depends on |
|---|---|---|---|
| [0](Phase0.md) | Target framework modernization | Done | — |
| [1](Phase1.md) | Safe navigation & string interpolation | Done | 0 |
| ~~2~~ | ~~User-defined types with behaviour~~ | **Dropped** | — |
| [3](Phase3.md) | Generics in functions, records and types | Done | 0, 1 |
| [3.5](Phase3.5.md) | Split binding from emission | Done | 3 |
| [4](Phase4.md) | State machines: iterators and async | Done | 3, 3.5 |
| [5](Phase5.md) | `Expression<T>` | Done | 3 |
| [6](Phase6.md) | Language server | Planned | 3.5 |

## Why Phase 2 was dropped

The original proposal included full methods/properties/interface-implementation on user-defined
`record` and `type` declarations. This was rejected, for good reasons worth recording:

- It contradicts constraint #2. LENS types are data carriers passed between host APIs; behaviour
  belongs in the host.
- Reflection.Emit cannot express large parts of the class topology this would imply, and the parts
  it can express (`DefineMethodOverride`, virtual slots, generic base classes with generic method
  overrides) are precisely where its known bugs cluster.
- It is the single largest item in the roadmap and would displace everything the project actually
  wants.

Consequences that later phases must accommodate rather than fix:

- Records stay field-only, with auto-generated `Equals`/`GetHashCode` (`TypeEntity.Autogeneration`).
- LENS cannot implement a host interface. Where a host API demands one, the host must accept a
  delegate instead. This is worth documenting for embedders.
- Phase 4's state machine classes are compiler-generated, not user-declared, so they are unaffected —
  they already have precedent in the closure classes emitted by `Scope.CreateClosureType`.
- LENS declares no interfaces or delegates, so Phase 3 has no legal site for `in`/`out` variance
  annotations — the CLI allows them nowhere else. Variance on imported host interfaces is unaffected.

## Sequencing notes

**Phase 5 is independent of Phase 4.** They were originally listed together because both are
motivated by Entity Framework, but they share no machinery. If EF interop is the priority,
Phase 5 can be pulled ahead of Phase 4 — you can call `.ToList()` synchronously against EF, but
without expression trees you cannot build the query at all.

**Phase 3 and Phase 3.5 overlap.** Both require replacing static, globally-cached type resolution
with context-scoped resolution. Phase 3 step 1 does the shared part. See
[Phase3.md](Phase3.md#step-1-introduce-a-resolution-context) — if that step grows, consider
swapping the two phases.

**Phase 6 is last on purpose.** Every phase above changes the grammar and the semantic model.
The one exception is lexer-driven syntax highlighting, which is grammar-stable and can ship at
any time; it is broken out as [Phase 6 step 1](Phase6.md#step-1-syntax-highlighting-and-server-skeleton).
