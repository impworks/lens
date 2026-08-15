# Phase 5 — `Expression<T>`

> **Status: done.** What was actually built, and where it departed from this plan, is recorded in
> [Outcome](#outcome) at the end.

## Goal

Let LENS lambdas be consumed as `System.Linq.Expressions.Expression<TDelegate>`, so that scripts can
drive `IQueryable` providers — Entity Framework above all:

```
use System.Linq

let adults = db.Users
    |> Where u -> u.Age >= 18
    |> OrderBy u -> u.Name
    |> ToList ()
```

with the `Where`/`OrderBy` resolving to `Queryable`'s overloads and the predicate translated to SQL
rather than run in memory.

## Position in the roadmap

**Independent of [Phase 4](Phase4.md).** These two were originally grouped because both are motivated
by EF, but they share no machinery whatsoever.

**If EF interop is the actual driver, this phase outranks Phase 4.** You can call `.ToList()`
synchronously against EF and get correct results; without expression trees you cannot build the query
at all — every LINQ call silently binds to `Enumerable` instead of `Queryable`, pulls the whole table
into memory, and filters client-side. That failure is silent and looks like a performance problem
rather than a correctness one, which makes it worse.

Depends on [Phase 3](Phase3.md) only because `Queryable`'s signatures are generic and the call-site
inference wants to be solid first.

## What it actually is

A **second backend for lambda bodies**. Today `LambdaNode.EmitInternal` creates a delegate: it
resolves a delegate type via `FunctionalHelper.CreateDelegateType`, loads the closure instance, loads
a function pointer to the closure method, and calls the delegate constructor.

The expression-tree path instead emits *calls into `System.Linq.Expressions`* that build a tree
object at runtime, then calls `Expression.Lambda<TDelegate>(body, parameters)`. The lambda body is
never compiled to IL at all — it is walked at compile time and turned into constructor calls.

So the work is:

1. **Call-site selection** — decide whether a given lambda argument becomes a delegate or an
   expression tree.
2. **A translator** — bound node → `Expression` factory call, for the supported subset.

## Step 1 — Call-site selection

When a method parameter's type is `Expression<TDelegate>` rather than `TDelegate`, the lambda must
take the expression path.

This lands in overload resolution. `Queryable.Where` and `Enumerable.Where` differ *only* in that
parameter, so the choice between them is exactly this decision. Touch points:

- `ReflectionHelper.ResolveMethodByArgs` and `TypeExtensions.TypeListDistance` — an argument that is
  a LENS lambda must be considered convertible to `Expression<TDelegate>` at a distance comparable to
  its convertibility to `TDelegate`.
- `TypeExtensions.LambdaDistance` — currently unwraps a delegate type. It needs to unwrap
  `Expression<TDelegate>` one level further.
- `FunctionalHelper` — gains `Expression<>` awareness alongside its delegate handling.
- `NodeBase.EnsureLambdaInferred` — calls `ReflectionHelper.WrapDelegate(delegateType)` to recover
  argument and return types for inference. It must unwrap `Expression<>` first, or argument inference
  inside a queryable lambda breaks.

Tie-break rule: when both `Enumerable` and `Queryable` overloads are applicable, the receiver's static
type decides — `IQueryable<T>` prefers `Queryable`. This is what C# gets from `IQueryable<T>` being
more derived than `IEnumerable<T>`, so the existing interface-distance logic may already produce it;
verify with a test rather than assuming.

## Step 2 — The translator

A visitor over the bound tree producing `Expression` construction. Roughly by node family:

| LENS node | Expression |
|---|---|
| literals | `Expression.Constant` |
| `GetIdentifierNode` (lambda arg) | the `ParameterExpression` |
| `GetIdentifierNode` (captured local) | `Expression.Constant` of the captured value, or a field access on the closure — see below |
| `GetMemberNode` | `Expression.Property` / `Expression.Field` |
| `GetIndexNode` | `Expression.ArrayIndex` / `Expression.Property` for indexers |
| binary operators | `Expression.Add`, `Equal`, `AndAlso`, … |
| unary operators | `Expression.Negate`, `Not` |
| `InvocationNode` | `Expression.Call` |
| `NewObjectNode` | `Expression.New` |
| `IfNode` (line form) | `Expression.Condition` |
| `CastOperatorNode` | `Expression.Convert` |
| `NullNode` | `Expression.Constant(null, type)` |

Not everything can or should translate. The EF-translatable subset is small, and attempting more
produces trees that build fine and then fail at query-translation time with a provider error the user
cannot act on. **Restrict to line-expression lambda bodies** (`lambda_line_expr`) in the first cut and
reject block bodies, `match`, `try`, loops, and assignment with a clear compile-time error naming the
construct. Rejecting early and precisely is far better than deferring to EF's "could not be
translated" message.

Captured variables are a real decision. C# captures them by reference through the closure class, so a
later mutation is visible to a deferred query. The simpler alternative — bake the current value in as
a `Constant` — is easier and usually what a script author expects, but diverges from C#. Given the
project's "roughly C# in semantics" rule, prefer the closure-field approach; if that proves awkward,
choose `Constant` **deliberately** and document it.

`RecordDefinitionNode`-based projections (`Select u -> new UserDto u.Name u.Age`) map to
`Expression.New` with the record's generated constructor — worth an explicit test, since projection to
a DTO is the single most common real EF query shape.

## Sharp edges

**The `Nullable<T>` lifting rules** in expression trees differ subtly from IL semantics, and EF's
translation of null comparison differs again (SQL three-valued logic). Test null handling explicitly;
do not assume a tree that compiles produces the SQL you expect.

**Interaction with [Phase 1](Phase1.md)'s safe navigation.** `Where u -> u.Address?.City == "X"` is
an obvious thing to write. `?.` desugars to a conditional over a temp local, which does not translate
to a useful SQL query and may not translate at all. Either reject `?.` inside an expression-tree
lambda with a specific message, or translate it as `Expression.Condition` and let the provider decide.
Recommend rejecting in the first cut — a clear compiler error beats an opaque provider exception.

**Method resolution inside the tree.** `Expression.Call` needs a `MethodInfo`. The bound model already
has it from ordinary resolution, so reuse that rather than re-resolving — but be careful with
extension methods, which resolve through `ExtensionMethodResolver` and must appear in the tree as
static calls with the receiver as the first argument.

**Safe mode.** Expression trees can invoke arbitrary methods at runtime via `Compile()`. If safe mode
is active, confirm that building a tree cannot smuggle in a type the allow-list forbids.

**Do not attempt `Expression` on the emit side of generics.** The tree is built by runtime calls, so
generic instantiation happens at runtime and the `TypeBuilder.GetMethod` hazard from Phase 3 does not
apply here. Worth noting because it looks like it should.

## Acceptance criteria

- A LENS lambda passed to a `Queryable` extension resolves to the `Queryable` overload, not the
  `Enumerable` one, and this is asserted directly (inspect the resolved `MethodInfo`, do not infer it
  from behaviour).
- The generated `Expression<Func<T,bool>>` matches the tree C# produces for the equivalent lambda, for
  a representative set: comparison, `&&`/`||`, member access, method call, `new` projection.
- An end-to-end test against a real provider. EF Core against SQLite in-memory is the cheapest honest
  option; asserting on generated SQL for a handful of queries is worth more than any number of tree
  shape assertions.
- Unsupported constructs produce a compile error naming the construct and its location.
- Both TFM legs green — `System.Linq.Expressions` is available on net47, but verify, since the EF
  Core test dependency will likely be `net8.0`-only. Split the test assembly if so.

## Out of scope

- Statement-bodied expression trees (`Expression.Block`). Legal in the API, untranslatable by every
  real provider.
- Compiling expression trees back to delegates as an optimisation.
- Any EF-specific integration beyond making `IQueryable` work — no migrations, no change tracking
  helpers, no `DbContext` sugar. LENS drives the host's context; it does not wrap it.

## Outcome

### Call-site selection

`Expression<TDelegate>` is unwrapped to its delegate wherever a signature is inspected:
`FunctionalHelper.IsExpressionType` / `UnwrapExpressionType` (both `Type` and `TypeEntry`),
`ReflectionHelper.WrapDelegate`, `TypeExtensions.distanceFrom`, and both generic inference paths
(`GenericHelper.GenericResolver.ResolveRecursive`, `ReflectionHelper.Unify`). A lambda is therefore
exactly as near to `Expression<Func<T,bool>>` as it is to `Func<T,bool>`.

That leaves `Queryable.Where` and `Enumerable.Where` tied, because the receiver is one step from
`IQueryable<T>` and from `IEnumerable<T>` alike under this distance model. **The tie-break the plan
expected the existing interface-distance logic to produce is not there** — it was verified with a
test, and it reported an ambiguity. Two rules were added instead, applied in order after distance,
in `TypeExtensions.BestCandidates`, which both `ReflectionHelper.ResolveMethodByArgs` and
`ExtensionMethodResolver` now go through:

1. `ExpressionTreeAffinity` — prefer the candidate whose parameter wants a tree and is being handed
   a lambda. This decides `Where`, `Select`, `OrderBy` and friends.
2. `MostSpecific` — prefer the candidate no other candidate is strictly more specific than. This is
   C#'s better-conversion-target rule, and it decides the calls with no lambda at all: `Count ()`,
   `ToArray ()`, `Sum ()` on an `IQueryable` were newly ambiguous without it.

Both rules only fire on an exact distance tie, which previously threw `AmbiguousMatchException`, so
no call that resolved before resolves differently now.

One thing the plan did not anticipate: on .NET Core, `Queryable` lives in its own assembly, while
`ReferencedAssemblyCache` anchored `System.Linq` to `Enumerable`'s. `Queryable` was therefore
invisible unless something else had already loaded the assembly, which made overload selection
depend on what the host happened to have touched. `typeof(Queryable)` and `typeof(Expression)` are
now anchors.

### The translator

`Lens/Compiler/ExpressionTreeBuilder.cs` walks the bound body and emits calls into the `Expression`
factories. `LambdaNode` carries the target `Expression<TDelegate>` in its binding record, resolves
to it, and dispatches to the builder from `EmitInternal`.

Supported: literals and folded constants, parameters, captured locals, fields, properties, array
length and indexing, indexers, method calls (extension methods included, as the static calls they
are), `new` including records, arrays, arithmetic and comparison with numeric and `Nullable<T>`
lifting, `&&` / `||` / `not`, `??`, `as`, `is`, `default`, and the line form of `if`.

Two places translate the node the author wrote rather than what binding rewrote it into, because the
rewrite is an IL-backend concern: `&&` / `||` (which LENS expands into `if`, and which must stay
`AndAlso` / `OrElse` for a provider to read them) and nullable arithmetic. String `+` goes the other
way and is built as C# builds it, an `Add` naming `string.Concat`.

Captured variables are closure-field accesses, as the plan preferred — `Expression.Field` over a
`Constant` holding the closure instance. A mutation after the query is built is visible to it, and
EF Core turns the field into a query parameter rather than a literal. Both are asserted.

The lambda still gets its closure class and backing method even in tree mode. The method is dead
code, but the class is where a captured variable lives, and sharing the one hoisting mechanism is
what keeps a variable captured by both a lambda and a tree in a single place.

### Rejections

Restricted to line-expression bodies as planned. Each rejection names the construct and its
location: `ExpressionTreeBlockBody` (a block body, which is also what a `match` or a loop body
reports), `ExpressionTreeNullSafe` (`?.`, rejected rather than translated, as recommended),
`ExpressionTreeUnsupportedNode`, `ExpressionTreeUnsupportedOperator` (string ordering, shifts,
delegate composition), `ExpressionTreeLambdaRequired` and `ExpressionTreeNoDelegateValue` (a
delegate value passed where a tree is wanted has no body left to walk).

### Safe mode

No change was needed. The tree's own type goes through `CheckTypeInSafeMode` like any other resolved
expression type, and the check recurses into the generic arguments, so a script cannot reach
`System.Linq.Expressions` through a query while the restrictions forbid it. Asserted.

### Tests

`Lens.Test/Features/ExpressionTreeTest.cs` — overload selection asserted on the resolved
`MethodInfo`, tree shapes compared against the string form of the tree the C# compiler builds for
the same lambda, execution through `EnumerableQuery`, and the rejections.

`Lens.Test/Features/ExpressionTreeEfCoreTest.cs` — end to end against EF Core on in-memory SQLite,
asserting on the generated SQL. As the plan foresaw, the dependency is `net8.0`-only; rather than
splitting the assembly, the fixture is compiled out of the net47 leg with `#if !NET_CLASSIC`.

Both TFM legs are green, as is `LENS_LOWER_ALL=1`.
