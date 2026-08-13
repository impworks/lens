# Phase 4 — State machines: iterators and async

## Goal

```
fun naturals:IEnumerable<int> (max:int) ->
    for i in 1..max do
        yield i

fun fetch:Task<string> (url:string) ->
    let client = new HttpClient ()
    await (client.GetStringAsync url)
```

## The core insight

**Iterators and async are one feature with two front-ends.** Both require rewriting a method body
into a class with a `MoveNext` that resumes at a numbered state. The transformation, the local
hoisting, the try/finally handling, and the control-flow flattening are shared. Only the driving
protocol differs — `IEnumerator<T>` for one, `AsyncTaskMethodBuilder` for the other.

Build the machinery once, validate it with `yield` where the protocol is simple and fully
inspectable, then add `await` on top. Done in this order, `await` costs perhaps 40% more than
`yield` alone. Done in the other order, or as two separate implementations, it costs close to double.

## The missing piece

LENS compiles structured AST directly to IL. There is no IR, no CFG, no lowering pass — `IfNode`,
`WhileNode`, `ForeachNode`, and `TryNode` each emit their own branches and labels inline.

A state machine needs the opposite shape: a flat list of statements with explicit labels and jumps,
so that a resume point can be a jump target. You cannot resume into the middle of a structured
`while` node that emits its own loop.

So the phase's real deliverable is **a lowering pass**, and `yield`/`await` are its first two
consumers.

## Prerequisites

- **[Phase 3.5](Phase3.5.md) stages 2 and 4 are effectively required.** The transform is a
  tree-to-tree rewrite, which needs the non-destructive bound model (stage 2). And state-machine
  hoisting must *merge* with closure hoisting rather than nest inside it, which needs the closure
  analysis/emission split (stage 4).
- **[Phase 3](Phase3.md)** — because `fun naturals<T>:IEnumerable<T>` should work, and retrofitting
  generics into a state-machine emitter is much worse than the reverse.

## Step 1 — The lowering pass

Rewrite a method body from structured control flow into a flat statement list over an explicit label
and jump vocabulary:

- new internal nodes: `LabelNode`, `GotoNode`, `ConditionalGotoNode`. These are compiler-internal —
  **not** exposed in the grammar. LENS gets no `goto`.
- lower `IfNode`, `WhileNode`, `ForNode`, `ForeachNode` (already expands to
  `GetEnumerator`/`MoveNext`), and `MatchNode` into that vocabulary.
- run the pass **only** on methods that contain a `yield` or `await`. Every other method keeps
  today's direct emission path untouched. This keeps the blast radius small and means a bug in the
  pass cannot regress ordinary scripts.

Validate the pass on its own before any state machine exists: lower a method, emit it, and assert it
behaves identically to the un-lowered version. Do this for every control structure. It is much easier
to debug a lowering bug here than through a state machine.

## Step 2 — The state machine transform

Given a lowered body:

1. Assign a state number to each resume point (each `yield`/`await`) plus the initial and final
   states.
2. Hoist every local that is live across a resume point into a field of the machine class. Locals
   that are not live across any resume point can stay as IL locals.
3. Build `MoveNext` as: a dispatch `switch` on the state field jumping to the resume label, then the
   flattened body, with each resume point storing state, returning, and marking its resume label.

Use a **class**, not a struct, for the machine. C# uses a struct in release builds to avoid an
allocation on the synchronous-completion path; that optimisation requires the `MoveNextRunner`
boxing dance and makes debugging significantly worse. LENS is glue code and is not on anyone's hot
path — take the simpler correct thing.

The machine class is compiler-generated, so it is unaffected by Phase 2 being dropped. There is
direct precedent: `Scope.CreateClosureType` already generates classes, gives them fields, and wires
up parent references, and `TypeEntity.Interfaces` +
`TypeBuilder.AddInterfaceImplementation` already works (the main type implements `IScript`).

## Step 3 — `yield`

Grammar: `yield_stmt = "yield" [ "from" ] line_expr`.

`yield from` (yielding a whole sequence) is worth including — it is cheap once the machine exists,
and it is the common case when composing iterators.

The generated class implements `IEnumerable<T>`, `IEnumerator<T>`, `IEnumerable`, `IEnumerator`, and
`IDisposable`, with the standard C# trick of `GetEnumerator` returning `this` on first call (thread
id check) and a fresh instance thereafter.

Return type inference: `T` comes from the yielded expressions' common type via
`TypeExtensions.GetMostCommonType`, unless the function declares `IEnumerable<T>` explicitly, in
which case check the yields against it.

`ForeachNode` already consumes any `IEnumerable<T>`, so iterating a LENS iterator works for free.

## Step 4 — `async` / `await`

On top of the same machine:

- resolve the awaiter pattern structurally — `GetAwaiter()`, `IsCompleted`, `OnCompleted`/
  `UnsafeOnCompleted`, `GetResult()`. Do **not** special-case `Task`/`Task<T>`; pattern-match the
  shape, so `ValueTask`, `YieldAwaitable`, and host-defined awaitables all work.
  `Context.ResolveMethod` and `ResolveProperty` already do everything needed.
- drive with `AsyncTaskMethodBuilder` / `AsyncTaskMethodBuilder<T>`, plus
  `AsyncVoidMethodBuilder` only if fire-and-forget is wanted — recommend **not** supporting async
  void; it is a footgun and glue code has no need for it.
- decide the syntax. `await expr` as a prefix operator is the C#-shaped answer. Whether functions
  need an `async` marker is a real design question: C# needs it for backward compatibility with
  `await` as an identifier, which LENS does not have. Inferring "this function is async because it
  contains `await`" is defensible and less ceremonious — but it makes the function's return type
  depend on its body, which interacts awkwardly with explicit return type signatures. **Decide this
  before writing the parser**, not during.

## Sharp edges

**`yield`/`await` inside `try`.** The hard case, and the one that most implementations get wrong.
`TryNode.EmitInternal` currently calls `gen.BeginExceptionBlock()` / `BeginFinallyBlock()` /
`EndExceptionBlock()` inline (`TryNode.cs:101–115`). You cannot resume into the middle of a protected
region — the IL is not valid and the JIT will reject it.

The standard solution is to keep the `try` in `MoveNext` but move the `finally` body into a separate
method that both the normal path and `Dispose` call, tracking with a per-region flag whether the
region is currently active. Options, in increasing order of cost:

- **first cut: reject `yield`/`await` inside `try` with a clear error.** Legitimate, shippable, and
  keeps step 2 honest.
- then: support `await` in `try`/`catch`/`finally` (the common real need).
- then: support `yield` in `try`/`finally` with proper `Dispose` semantics, which is what makes
  `using` inside an iterator work.

Pick the staging deliberately; do not let "we'll handle try later" go unrecorded.

**Closure and machine hoisting must merge.** A lambda inside an iterator closes over locals; the
machine also hoists locals. If a local is captured *and* live across a yield, it must live in exactly
one place, not be copied into both — otherwise mutations through one view are invisible to the other.
The C# answer is that the closure class wins and the machine holds a reference to it. This is why
Phase 3.5 stage 4 matters, and it is the subtlest correctness issue in the phase. Note that commit
`2607542` fixed a loop-closure bug already; that area has form.

**`Scope.Kind == ScopeKind.Loop` creates a closure per iteration** so each iteration gets fresh
captured variables. Interaction with a state machine that resumes mid-loop needs explicit thought and
an explicit test.

**Pure functions.** `pure` memoizes on arguments. A `pure` iterator would cache the enumerable and
hand the same one-shot enumerator to two callers. Reject `pure` + `yield`/`await`.

**Safe mode.** `Context.SafeMode` gates types. Async pulls in `System.Threading.Tasks` and the
compiler-services builders; confirm the allow-list treats compiler-generated usage correctly and that
scripts cannot use async to reach something safe mode forbids.

## Optional step 5 — `IAsyncEnumerable`

`await` + `yield` in one method, driving `IAsyncEnumerable<T>` /
`ManualResetValueTaskSourceCore`. Only available on `net8.0` and `netstandard2.1`, so it would be a
TFM-conditional feature — the `net47` leg retained in Phase 0 cannot have it without the
`Microsoft.Bcl.AsyncInterfaces` package.

Do this only if a real use case appears. It roughly doubles the protocol complexity for a feature
glue code rarely needs.

## What was built, and what was left out

Steps 1–3 are done. The staging that was actually taken:

- **The lowering pass** (`Lens/Compiler/Lowerer.cs`) runs before binding, on the parse tree, and
  rewrites rather than mutates. Blocks stay nested and only the control flow *between* them is
  flattened, which means a name declared in a loop body still belongs to that body's frame — the
  pass never merges two frames and therefore never has to rename anything. Validated independently
  through `LensCompilerOptions.LowerAllFunctions`: `LENS_LOWER_ALL=1 dotnet test` runs the whole
  suite with every method body flattened.
- **Hoisting merges with closure hoisting**, as the phase demanded. The machine class *is* the
  closure class of MoveNext's root scope (`Scope.MakeMachineRoot`), so a name that is both captured
  by a lambda and live across a `yield` ends up in exactly one field. A lambda declared in an
  iterator is compiled onto the machine class itself and reaches those fields through `this`.
- **`yield` and `yield from`**, with the machine implementing `IEnumerable<T>`, `IEnumerable`,
  `IEnumerator<T>`, `IEnumerator` and `IDisposable`. The two non-generic members are explicit
  overrides; everything else matches its interface method by name.

Deliberately left out, each with a specific diagnostic rather than a crash:

| Rejected | Message |
|---|---|
| `yield` in `try` / `using` / `match` | LE3170 |
| `yield` in a lambda | LE3171 |
| `pure` iterator | LE3169 |
| iterator with no declared return type | LE3167 |
| iterator whose return type is not `IEnumerable<T>` | LE3168 |
| generic iterator (`fun f<T>:IEnumerable<T>`) | LE3173 |
| a name declared in a loop *and* captured, inside an iterator | LE3172 |

Two of these are worth calling out.

**Return type inference was not built.** An iterator must declare `IEnumerable<T>` (or `T~`)
explicitly. Inferring `T` from the yielded expressions needs the body bound, and the machine is
built out of the parse tree — before anything has a type — because that is what lets the rewrite
reuse the existing binding and emission path wholesale rather than re-binding a tree that has
already been bound once.

**`yield` inside `try` is the deferred piece**, exactly as the phase proposed. The consequence
visible today is that a lowered `foreach` inside an iterator disposes its enumerator when the loop
ends normally and not when the iterator is abandoned. Supporting it means moving `finally` bodies
into separate methods that both the normal path and `Dispose` call, and that is the same machinery
`await` in `try` needs.

## Acceptance criteria

- The lowering pass is validated independently, per control structure, before any state machine
  exists.
- Iterators: infinite sequence consumed lazily with `Take`; early `break` out of a `foreach` over a
  LENS iterator disposes correctly; `yield from`; iterator consumed by a host C# method taking
  `IEnumerable<T>`.
- Async: awaiting `Task`, `Task<T>`, `ValueTask<T>`, and a host-defined awaitable; exception
  propagation through `await`; a `Task<T>` returned to and awaited by host C# code.
- A lambda capturing a local that is also live across a `yield`, with mutation visible through both.
- Methods containing neither `yield` nor `await` are provably unaffected — same IL as before the
  phase, on a spot-check of representative scripts.
- Whatever is rejected (`yield` in `try`, `pure` iterators, async void) is rejected with a specific
  message, not a crash.
