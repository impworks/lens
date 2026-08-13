# Phase 3 — Generics in functions, records and types

## Goal

Let LENS *declare* generic functions, generic records and generic algebraic types, with constraints
and with inference at call sites. The target surface is [generics.lns](generics.lns):

```
pure fun doStuff<T = class & new & IFoo, K> (x:T) -> x

record KeyValue<K, V>
    Key: K
    Value: V

type Foo<T>
    Bar
    Baz of Tuple<int, T>
```

Plus the consuming side:

```
fun swap<T> (a:T b:T) -> new (b; a)

let p = new KeyValue<string, int> "x" 1
let s = swap 1 2                            // T inferred as int
let f = Baz (new (1; "x"))                  // Foo<string> inferred
```

> **Note on the example.** `pure fun doStuff<...> () -> ()` as literally written in `generics.lns` is
> rejected for a reason unrelated to generics: `CreatePureWrapper` errors with
> `PureFunctionReturnUnit` on a `pure` function returning `unit`, since there is nothing to memoize.
> The example above adds an argument and a return value so it is actually compilable. `K` being
> unused is fine — an unreferenced type parameter is legal, as in C#.

## Starting position

The good news: **inference machinery for *consuming* generic .NET methods is already mature.**
`GenericHelper.ResolveMethodGenericsByArgs` / the nested `GenericResolver` do real unification —
recursive descent through constructed generic types, interface implementation search
(`FindImplementation`), explicit hints, and a `LambdaResolver` callback that infers a lambda's return
type mid-resolution so `Select x -> x ** 2` works. `ApplyGenericArguments` does substitution.
`MakeGenericTypeChecked` validates the four constraint kinds.

That is most of the hard algorithmic work, and it should be reused for LENS-declared generics rather
than reimplemented.

## The real obstacle

This is the design flaw called out when this phase was scoped, and it deserves to lead the plan:

**Type compatibility, distance, and resolution are static extension methods with process-global
caches.** They assume a `Type` means the same thing everywhere. With generics that assumption breaks,
because a type parameter is scoped to a particular method or record.

Concretely, in `Resolver/TypeExtensions.cs` (847 lines):

```csharp
private static readonly Dictionary<Tuple<Type, Type, bool>, int> DistanceCache;

public static int DistanceFrom(this Type varType, Type exprType, bool exactly = false)
{
    var key = new Tuple<Type, Type, bool>(varType, exprType, exactly);
    if (!DistanceCache.ContainsKey(key))
        DistanceCache.Add(key, distanceFrom(varType, exprType, exactly));
    return DistanceCache[key];
}
```

Four distinct problems, all in those six lines:

1. **Correctness under generics.** The distance between `T` and `int` is not a property of the pair
   `(T, int)` — it depends on `T`'s constraints, which are a property of the declaring method. Two
   different LENS functions can each declare a `T`; a cache keyed on `Type` alone conflates them the
   moment `GenericTypeParameterBuilder` instances enter the picture.
2. **Cross-`Context` leakage.** The cache is `static`, so it is shared by every `LensCompiler`
   instance in the process. Types from one compilation are cached against types from another.
   Already latent today; generics make it reachable.
3. **Thread safety.** `ContainsKey` then `Add` on a plain `Dictionary`, unguarded. Two concurrent
   `LensCompiler` instances can corrupt it.
4. **Leak.** The cache holds strong references to `TypeBuilder`s forever, so every compilation's
   generated types are pinned for the process lifetime.

`ReflectionHelper.InterfaceCache` (`Dictionary<Type, Type[]>`, also static) has the same shape, plus a
particular hazard of its own: caching the interface list of a `TypeBuilder` that is still being built
returns a stale answer once more interfaces are added.

So this phase does not start with generics. It starts with making resolution context-scoped.

## Step 1 — Introduce a resolution context

Create a `TypeResolutionContext` (name negotiable) owned by `Context`, holding:

- the distance/interface caches, now per-compilation and safely disposed with the `Context`;
- the **generic environment**: the set of type parameters currently in scope, keyed by LENS name,
  together with their *constraint model* (see step 2), pushed and popped as resolution enters and
  leaves a generic function, record or type body.

Then convert `TypeExtensions` and the relevant parts of `ReflectionHelper` from static extension
methods to instance methods on that object.

This is a broad, mostly mechanical diff — `IsExtendablyAssignableFrom` and `DistanceFrom` alone have
call sites scattered across most of `SyntaxTree`. Two ways to soften it:

- Keep thin extension-method wrappers that take the context as an explicit first argument, so call
  sites change from `a.IsExtendablyAssignableFrom(b)` to `a.IsExtendablyAssignableFrom(ctx, b)`
  rather than being restructured. Mechanical, greppable, reviewable.
- The genuinely pure helpers — `IsNumericType`, `IsVoid`, `IsStruct`, `GetNullableUnderlyingType`,
  the numeric conversion tables — have no context dependency and should **stay** static. Only the
  distance/assignability/interface family moves. That roughly halves the diff.

> **Sequencing note.** This step overlaps substantially with [Phase 3.5](Phase3.5.md), which
> threads a binding context through resolution for a different reason. Do this step with Phase 3.5's
> shape in mind — ideally `TypeResolutionContext` *is* the object Phase 3.5 later grows into, rather
> than a thing Phase 3.5 has to replace. If this step turns out larger than expected, seriously
> consider swapping the two phases and doing 3.5 first.

Land this step, with the full test suite green, **before** writing any generics code. It is a
refactor with no behaviour change and should be reviewable as such.

## Step 2 — Type parameters and inline constraints

Constraints are **inline**, in the parameter list, not in a trailing `where` clause:

```
fun sum<T = IComparable<T>> (items:T[]) -> ...
record Cache<K = IEquatable<K>, V = class & new> ...
```

### Grammar

```
type_params  = "<" type_param { "," type_param } ">"
type_param   = identifier [ "=" type_constraints ]
type_constraints = type_constraint { "&" type_constraint }
type_constraint  = "class" | "struct" | "new" | type

record_def   = "record" identifier [ type_params ] INDENT record_stmt { NL record_stmt } DEDENT
type_def     = "type"   identifier [ type_params ] INDENT type_stmt   { NL type_stmt   } DEDENT
fun_def      = [ "pure" ] "fun" identifier [ type_params ] [ ":" type ] fun_args "->" block
```

Add these to `Lens/Grammar/Grammar.v2.txt` alongside the existing `type_args`, and keep the
distinction visible: `type_args` is a *use* site (`Foo<int>`), `type_params` is a *declaration* site
(`Foo<T = class>`). They are not interchangeable, and only `type_args` needs backtracking.

### Parser notes

- **No new lexemes.** `class` and `struct` must **not** become `LexemType`s — they are currently
  legal identifiers and promoting them would break existing scripts. Parse them as
  `LexemType.Identifier` with value `"class"` / `"struct"`, recognised *only* inside
  `type_constraint`. `new` already exists as `LexemType.New`, so check for that lexeme explicitly.
- Consequence, worth documenting: inside a constraint list the words `class` and `struct` always win
  over a host type of the same name. Nobody has such a type; say so anyway.
- `&` is `LexemType.BitAnd`. There is no ambiguity with the binary operator because a constraint list
  is not an expression context.
- `type_params` in declaration position is unambiguous — after the declared name a `<` can start
  nothing else — so `ParseTypeParams` can `Ensure` its way through and produce good errors, unlike
  `ParseTypeArgs`, which must stay wrapped in `Attempt` because `<` there collides with less-than.
- Order inside the list is free (`T = IFoo & class & new` is as valid as `T = class & new & IFoo`).
  Collect first, validate after — do not encode ordering in the grammar.
- Attach to `FunctionNode`, `RecordDefinitionNode` and `TypeDefinitionNode`. The natural home is
  `TypeDefinitionNodeBase` for the latter two, since both need identical handling.

### Constraint model

Keep constraints in the compiler's **own** model — a `GenericParameterEntity` (name negotiable)
carrying: LENS name, ordinal, `IsReferenceType`, `IsValueType`, `RequiresDefaultCtor`, base type
signature, interface signatures, and the `GenericTypeParameterBuilder` once created.

This is not gold-plating. It is required, because you cannot reliably read constraints back off a
builder: `GenericTypeParameterBuilder.GetGenericParameterConstraints()` is not supported on an
unfinished builder, and even where `GenericParameterAttributes` reads back, relying on it makes
`MakeGenericTypeChecked` behave differently for LENS-declared and imported generics. So:

- `MakeGenericTypeChecked` gains a path that validates against the entity model when the definition
  is one of ours, and keeps its present reflection path for imported types;
- `DistanceFrom` / `IsExtendablyAssignableFrom` consult the same model when either side is a
  LENS-declared type parameter.

### Validation

Mirror C#'s rules, with LENS-level diagnostics that name the parameter (new entries in
`CompilerMessages.resx` **and** `CompilerMessages.ru.resx`, then regenerate the Designer file):

| Condition | C# analogue |
|---|---|
| `class` and `struct` together | CS0450-ish; mutually exclusive by definition |
| `struct` and `new` together | CS0451 — `struct` already implies a default ctor |
| `class`/`struct` together with a base type | CS0450 |
| more than one base type | CS0406 |
| base type is sealed, static, or a special type (`object`, `Array`, `ValueType`, `Enum`, `Delegate`, `MulticastDelegate`) | CS0701 / CS0702 |
| an entry the parser took as an interface is not an interface | CS0701 |
| duplicate interface | CS0405 |
| circular naked-type constraints (`T = K`, `K = T`) | CS0454 |

A naked type parameter as the base constraint (`fun f<T, K = T> ...`) is legal and should work; it is
the reason constraint resolution has to be a second pass.

### Emission

Two passes per declaration, in this order — getting it wrong is the most common Reflection.Emit
failure here:

1. `DefineGenericParameters(names)` for **all** parameters of the declaration, collecting the
   returned `GenericTypeParameterBuilder`s into the entity model and pushing them onto the generic
   environment.
2. Only then resolve each parameter's constraint signatures (which may reference sibling parameters)
   and apply `SetGenericParameterAttributes` / `SetBaseTypeConstraint` / `SetInterfaceConstraints`.

Attribute mapping: `class` → `ReferenceTypeConstraint`, `struct` → `NotNullableValueTypeConstraint`,
`new` → `DefaultConstructorConstraint`.

### Naming and arity

The CLR requires arity-mangled names: a two-parameter record `KeyValue` is emitted as
`` KeyValue`2 ``. Keep `Context`'s type dictionaries and `ResolveType` keyed on the **LENS** name and
mangle only at the `DefineType` call, so error messages and source lookups keep saying `KeyValue`.

**Forbid arity overloading**: one LENS name is one declaration. C# permits `Box` and `Box<T>` to
coexist; LENS should not, for either types or functions — it buys nothing for glue code and it makes
`ResolveType`, `ResolveMethod` and the error messages materially worse. Reject at declaration with a
"already declared" error. Same rule for functions: two `fun foo` with the same argument count may not
differ only in generic arity.

## Step 3 — Generic functions

- `FunctionNode` carries the type-parameter list from step 2. `MethodEntity.PrepareSelf` must call
  `MethodBuilder.DefineGenericParameters` **before** `SetParameters`/`SetReturnType`, since the
  returned builders are the types the signature refers to. This changes `PrepareSelf`'s current
  shape, which passes types straight to `DefineMethod`. Full order: `DefineMethod` (name/attrs only)
  → `DefineGenericParameters` → push generic env → resolve + apply constraints → resolve argument and
  return signatures → `SetParameters` / `SetReturnType`.
- `Context.ResolveType` consults the generic environment from step 1, so `T` inside the body resolves
  to that function's parameter and not to a host type.
- Call-site inference: reuse `GenericResolver` with the LENS function's parameters as `genericDefs`.
  `Context.Lookup.ResolveMethod` already accepts `hints` and a `LambdaResolver`, so explicit
  `foo<int> x` — already in the grammar as `get_id_expr = identifier [ type_args ]` — comes nearly
  free.
- The body must be checked against the parameter's *own* constraints: `T.CompareTo` is legal only if
  `T` is constrained to `IComparable`. Today nothing checks this because nothing could declare a `T`.

## Step 4 — Generic records

```
record KeyValue<K, V>
    Key: K
    Value: V
```

`TypeEntity.PrepareSelf` currently does `ResolveType(ParentSignature)` and then
`MainModule.DefineType(Name, attrs, Parent)`. For a generic type the parent may be expressed in terms
of the type's own parameters, so the order becomes: `DefineType(mangledName, attrs)` →
`DefineGenericParameters` → constraints → `SetParent(resolvedParent)` → `AddInterfaceImplementation`.

Auto-generated members need real work. `CreateSpecificEquals`, `CreateGenericEquals` and
`CreateGetHashCode` in `TypeEntity.Autogeneration` assume concrete field types:

- `Equals(other: KeyValue)` becomes `Equals(other: KeyValue<K,V>)` — the argument type is the
  self-constructed generic type, which means `TypeBuilder.GetMethod` at every call site.
- Field comparison must go through `EqualityComparer<T>.Default` rather than the current
  `Expr.Invoke(This, "Equals", cast to object, ...)`. Same reason C# does it: `T` may be a value
  type, a reference type, or `Nullable<>`, and the correct comparison differs. `GetHashCode` likewise.
- The sequence special case — `f.Type.IsGenericType && f.Type.Implements(typeof(IEnumerable<>), true)`
  → `Enumerable.SequenceEqual` — is exactly the code that hits the stale `InterfaceCache` from step 1
  when the field type is `T[]` or `IEnumerable<T>` over an unfinished builder. Verify it against a
  fixed cache before trusting the result.
- For a bare `T` field, `Implements` is false, so it falls to the comparer path. That is correct, but
  it means a `KeyValue<int, int[]>` and a `KeyValue<int, T>` instantiated at `int[]` compare
  *differently* (structural vs. reference). Decide deliberately and write it down; recommendation is
  to accept it, matching C#.

## Step 5 — Generic algebraic types

```
type Foo<T>
    Bar
    Baz of Tuple<int, T>
```

This is the largest new piece relative to the previous draft, where generic `type` was out of scope.
`Context.Compilation.DeclareType` currently emits, for `type Foo`:

- a base type `Foo`;
- one sealed type per label, deriving from `Foo` by *name* (`CreateType(tagName, node.Name, ...)`);
- for tagged labels, a `Tag` field plus a static factory method `Baz` on `MainType`.

Each of those grows a generic dimension:

- **Every label type must be generic in the full parameter list of the parent**, even labels that do
  not mention it — `Bar` has to become `` Bar`1 `` deriving from `Foo<T>`, because the CLR has no
  other way for it to extend a constructed generic base. Its parameters are fresh builders, and the
  parent's constraints must be **copied** onto them; the CLR checks that a derived type's arguments
  satisfy the base's constraints.
- The parent signature is a *constructed* generic type over the label's own parameters, so the
  `DefineType` → `DefineGenericParameters` → `SetParent` order from step 4 is mandatory here, not
  merely preferable.
- **The static factory becomes a generic method.** `Baz of Tuple<int, T>` yields
  `Baz<T> (value: Tuple<int,T>) : Foo<T>`, which is just another generic function — so `Baz (new (1; "x"))`
  infers `Foo<string>` through the machinery from step 3, and `Baz<string> ...` works explicitly.
  This is the payoff for reusing `GenericResolver` instead of writing a second inference path.
- The factory body's `Expr.New(tagName, ...)` must construct `Baz<T>` and reach its constructor
  through `TypeBuilder.GetConstructor`.
- **Untagged labels of a generic type cannot infer their arguments.** `Bar` alone carries no
  information about `T`, and untagged labels get no factory method today. Require explicit type
  arguments - either `Bar<int>` or `new Bar<int> ()` - and emit a clear error otherwise. C# has the identical wart with
  `Option<T>.None`; do not invent inference for it.
- **Pattern matching.** `MatchTypeRule` resolves a label by name and reads its `Tag` field. Against a
  value of type `Foo<int>` it must bind the label as `Baz<int>` and `Tag` as `Tuple<int,int>` — i.e.
  substitute the scrutinee's arguments into the label type, then `TypeBuilder.GetField`. `MatchRecordRule`
  needs the same treatment for generic records. These are easy to forget and produce
  `NotSupportedException` at emit time rather than a diagnostic; test them explicitly.

## Step 6 — `pure` on generic functions

`generics.lns` opens with `pure fun doStuff<...>`, so this is in scope rather than rejected.

The obstacle: `CreatePureWrapper` memoizes into a **static field on `MainType`**, typed
`Dictionary<argType, returnType>` (or `Dictionary<Tuple<...>, returnType>` for many arguments). A
method's type parameters cannot appear in the type of a field on an unrelated class, so that field
cannot be typed for a generic function, and the cache must be per-instantiation anyway.

The standard answer, and the one C# uses for exactly this problem, is a **generic holder class**: for
each `pure` generic function emit a helper type `` <pure_cache_doStuff>`n `` generic in the function's
parameters, with the cache field (and, for the zero-argument case, the flag field) static on it.
Access is `TypeBuilder.GetField(holder.MakeGenericType(T, ...), field)`.

Note two existing limits carry over unchanged: `pure` requires a non-`unit` return type
(`PureFunctionReturnUnit`) and at most 7 arguments (`PureFunctionTooManyArgs`).

If this step slips, the fallback is a clear "pure is not supported on generic functions" error — but
it should be attempted, because the holder-class pattern is small and self-contained once step 4's
generic-`TypeBuilder` plumbing exists.

## Sharp edges

**Members of a generic `TypeBuilder`.** Reflection.Emit's most notorious trap, and it will account
for a large share of the debugging time in this phase. To reference a field, method or constructor of
a *constructed* generic type whose definition is still a `TypeBuilder`, you must use the static
helpers `TypeBuilder.GetField`, `TypeBuilder.GetMethod`, `TypeBuilder.GetConstructor`. Calling
`.GetField(...)` on the constructed type throws `NotSupportedException`. Every place that resolves a
member on a user-defined type — `TypeEntity.ResolveField`, `ResolveMethod`, `ResolveConstructor`, and
their callers in `GetMemberNode` / `NewObjectNode` — needs a generic branch. Centralise it in one
helper rather than scattering the special case; steps 4, 5 and 6 all depend on that one helper.

**Generic closures.** A lambda inside a generic function closes over values whose types mention the
function's type parameters. `Scope.CreateClosureType` creates a *non-generic* `TypeEntity`, so the
closure class cannot hold a field of type `T`. The closure class must itself become generic in the
enclosing function's parameters, and the closure method must be invoked on the constructed type. This
means:

- `CreateClosureType` takes the enclosing generic parameters and forwards them;
- `EmitClosureInstance`'s parent-chain walk must construct each closure type with the right arguments
  as it walks;
- and the `TypeBuilder.GetField` rule above applies to every closure field access.

This is the highest-risk item in the phase. It is entangled with `Scope`, which is also the machinery
Phase 4 depends on — budget for it, and get closure-inside-generic-function under test early rather
than discovering it at the end.

**Variance on builders.** `GenericDistance` / `GenericParameterDistance` in `TypeExtensions` handle
variance for *imported* types and must keep doing so. Confirm they behave when one side is a
`GenericTypeParameterBuilder` rather than a runtime `Type` — `IsAssignableFrom` and friends are
unreliable on builders, and `GenericParameterAttributes` on a builder is a poor thing to branch on
(hence the entity model in step 2).

## Acceptance criteria

- Step 1 lands as a standalone, behaviour-preserving refactor with the full suite green.
- A generic function with inference, with explicit type arguments, and with a lambda argument whose
  type is inferred through the generic parameter.
- Every constraint kind accepted, in arbitrary order, including a naked type parameter as base
  (`fun f<T, K = T>`) and a self-referential interface (`T = IComparable<T>`).
- Every invalid combination in the validation table rejected with a message naming the parameter and
  the offending constraint — plus constraints enforced at call sites.
- A generic record, constructed, with working `Equals`/`GetHashCode` for reference-type, value-type
  and `Nullable<>` instantiations.
- A generic algebraic type: tagged label constructed with inference, untagged label constructed with
  explicit arguments, and both matched in a `match` block against a constructed instantiation.
- A `pure` generic function memoizing independently per instantiation (`doStuff<int>` and
  `doStuff<string>` do not share a cache).
- A lambda closing over a `T`-typed local inside a generic function; and a lambda inside a generic
  function inside a loop — the case that stresses closure-parent chaining and generic forwarding
  simultaneously.
- Both TFM legs green.

## As built

The phase landed as planned. Where reality argued with the plan, it won; those places are worth
recording.

**Record and label patterns infer their type arguments.** `case Pair(First = f; Second = s)` matches
a `Pair<string, int>` — the arguments come from the scrutinee, exactly as they do for a label. The
plan assumed patterns would spell them out, but `rule_record = identifier "(" ...` leaves no room for
`type_args` without reintroducing the `<` ambiguity, and inference is the better surface anyway.
`MatchTypeRule` and `MatchRecordRule` share one helper, `Context.ResolvePatternType`.

**Members of a type parameter are reached by boxing the receiver**, not with a `constrained.` prefix.
`box !T` is valid for value and reference instantiations alike, so one call site serves every
instantiation; the cost is a box on constrained calls. `EmitNodeForAccess` does it in one place, and
`CastOperatorNode` uses the same `box` / `unbox.any` pair for casts to and from a parameter.

**Constraints do not filter overload resolution.** For distance purposes a LENS-declared parameter
looks like `object` in the "what can be stored into `T`" direction. Otherwise a `struct`-constrained
function invoked with a string simply disappeared from the candidate set, and the user got "no
function named 'test' with suitable arguments" instead of a message naming the constraint. Real
enforcement happens in `GenericHelper.CheckConstraints` after a candidate is chosen.

**Generic instantiations are canonicalised.** `TypeBuilder.MakeGenericType` returns a fresh object
on every call, and those objects compare unequal to each other, so `Holder<int>` built twice was two
different types as far as the compiler's `==` comparisons were concerned.
`TypeResolutionContext.MakeGenericType` hands out one shared instance per instantiation. This was not
anticipated by the plan and would have quietly broken most of it.

**The ancestor search for constructed generics is limited to declared types.** `Option<T>` accepting a
`Some<int>` needs distance to keep walking the inheritance chain after `GenericDistance` fails. Doing
that for imported generics too changed existing conversions, so the walk only applies when the target's
definition is a `TypeBuilder`.

**A type parameter that appears nowhere in the signature must be given explicitly**, as in C#: the
plan's note that an unused `K` "is fine" holds for the declaration, not for the call site. The pure
wrapper therefore passes its own parameters to the internal method explicitly rather than relying on
inference.

**Generated equality goes through `EqualityComparer<T>.Default` for every record**, generic or not,
including `GetHashCode`. Making it conditional would have left two code paths for no benefit.

## Out of scope

- **`in`/`out` variance annotations on LENS-declared generics.** Deliberately excluded, not deferred.
  The CLI permits variance only on generic parameters of interfaces and delegates; records and
  algebraic types emit as classes and LENS functions emit as methods, so there is no declaration site
  in LENS that could legally carry it. Since Phase 2 was dropped, LENS declares no interfaces and
  never will. Variance on *imported* interfaces continues to work through
  `GenericParameterDistance`, which is where it belongs.
- Arity overloading of a single name (see step 2).
- Generic constraints referencing a type parameter of an *enclosing* declaration — LENS has no nested
  declarations, so this cannot arise; the compiler-generated closure and pure-cache holder classes
  handle their own forwarding internally.
