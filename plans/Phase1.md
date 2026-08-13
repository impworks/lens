# Phase 1 — Safe navigation & string interpolation

## Goal

Two self-contained ergonomic features with a high daily-use payoff. Both are pure front-end plus
`Expand()` work: neither touches the emitter, the closure machinery, or the type system.

They are also a deliberate re-onboarding ramp. Each one exercises the full
lexer → parser → `Resolve` → `Expand` → emit path end to end, which is the fastest way to get
reacquainted with the codebase before the structural phases.

**Do string interpolation first.** It is strictly simpler, it touches only the lexer and parser, and
it has no interaction with the type system. Safe navigation has real semantic depth.

## Dropped from this phase

### Named and optional arguments — dropped

Rejected: they do not compose with LENS's functional style. The invocation grammar is
whitespace-separated positional application (`invoke_line_args = { invoke_line_arg }`), which is what
makes partial application readable. Introducing `name: value` at call sites would make
`ReflectionHelper.IsPartiallyApplied` ambiguous — given a name/value pair you cannot tell whether the
caller is naming an argument or supplying fewer arguments than the method takes. Not worth it.

### `out` parameters — already supported, verified

This was listed as a gap in the original assessment. **That was wrong.** Checked and confirmed:

- `TypeEntity.Structure.cs:23` builds arguments for imported methods as
  `new FunctionArgument(p.Name, p.ParameterType, p.ParameterType.IsByRef)`. In IL an `out` parameter
  *is* a by-ref parameter (`out` is only `[Out]` metadata plus a C# rule about definite assignment),
  so `out` and `ref` land in the same code path.
- `UserDeclarationsTest.RefFunction1` covers exactly this and passes on net47:

  ```
  var x = 0
  int::TryParse "100" ref x
  x                            // => 100
  ```

- Argument-side plumbing (`IPointerProvider.RefArgumentRequired`, `InvocationNode.cs:328`) already
  handles the by-ref/pointer distinction for locals, fields, static members, and array elements.

So `TryParse`/`TryGetValue` and the rest of the by-ref BCL are reachable today. What is missing is
only cosmetic, and is **optional, low priority, not scheduled**:

- accepting `out` as a call-site synonym for `ref`, for readability;
- C#-style inline declaration (`out var x`), which would save the mandatory `var x = 0` line.

If ever done, both belong in the parser alone.

---

## 1a. String interpolation

### Syntax

```
$"the result is {a + b}"
$"{price:C} for {count} items"
$@"path: {dir}\{file}"          // verbatim + interpolated
```

Support `{expr}` and `{expr:format}`. Escape a literal brace as `{{` / `}}`, matching C#.

**Alignment (`{expr,-10}`) is out of scope for the first cut.** It is rarely used, and the comma
creates a parsing ambiguity against expressions that legitimately contain commas (generic type
arguments — `{foo.Bar<int, string>()}`). Add later if wanted, resolved by brace/angle depth.

The existing `print "{0}" x` idiom is untouched; this is purely additive.

### Design

Lexing an interpolated string in one pass would require the lexer to become re-entrant — the holes
contain arbitrary expressions, which may themselves contain strings, which may themselves be
interpolated. Don't do that. Instead:

1. **Lexer** produces a single `InterpolatedString` lexem capturing the *raw body*, plus a flag for
   verbatim. It scans to the closing quote while tracking brace depth and nested quotes, so a `}`
   inside a hole does not terminate the hole and a `"` inside a hole does not terminate the string.
   This scanner is the only genuinely fiddly part of the feature.
2. **Parser** splits the raw body into alternating literal chunks and holes, then for each hole runs
   a nested `new LensParser(new LensLexer(holeSource).Lexems)` and takes the single expression.
3. **`Expand()`** rewrites to `string.Format(formatString, args)` — where the format string is the
   literal chunks with holes replaced by `{0}`, `{1}`, … (carrying through any `:format` suffix), and
   `args` is an `object[]`.

Using `string.Format` rather than `DefaultInterpolatedStringHandler` is not a shortcut, it is
required: the handler does not exist on net47 (Phase 0 keeps that TFM). It also happens to be exactly
what C# itself lowered to before C# 10.

Degenerate cases:

- zero holes → emit a plain `StringNode`, no call.
- one hole, no format specifier → still go through `string.Format` for uniformity. Optimising this
  into a `ToString()` call is a possible later refinement, but the null-handling differs
  (`string.Format` renders null as empty, `x.ToString()` throws), so don't do it casually.

### Sharp edges

- **Error locations inside holes.** The nested lexer/parser produces `LexemLocation`s relative to the
  hole's own source, so an error in a hole will point at line 1 column 3 of nothing. Offset every
  location produced by the nested parse by the hole's position in the outer file. Do this from the
  start — retrofitting it means every test with an error message in a hole has to be redone.
  Multi-line verbatim interpolated strings make this a line *and* column offset, not just a column
  one.
- **Brace-depth tracking must account for LENS dict literals**, which use `{ }`
  (`new_dict_line = "{" init_dict_expr_line "}"`). `$"{new {1 => 2}}"` is legal and must lex.
- **Verbatim escaping** — in `$@"..."`, `""` is an escaped quote but `\` is literal. The brace rules
  are unchanged.

### Tests

- `LexerTest` — brace escaping, nested quotes, nested braces, verbatim combination, unterminated
  string, unterminated hole.
- `ParserTest` — AST shape for zero/one/many holes, format specifiers.
- A new `Features/StringInterpolationTest` — runtime results, including a hole containing a lambda, a
  dict literal, and a nested interpolated string; plus error-location assertions for a syntax error
  inside a hole.

---

## 1b. Safe navigation

### Syntax

```
foo?.bar
foo?.bar.baz            // whole chain short-circuits
foo?[idx]
foo?.method arg
a?.b ?? c
```

Grammar changes, against `Grammar.v2.txt`:

```
accessor_mbr  = ( "." | "?." ) identifier [ type_args ]
accessor_idx  = ( "[" | "?[" ) line_expr "]"
```

### The central design decision

`a?.b.c.d` must yield null if `a` is null **without evaluating `.b`, `.c`, or `.d`**. The check
short-circuits the entire remainder of the chain, not one link.

This means the desugaring **cannot live on the individual accessor node**. It has to be done at the
level of the whole accessor chain. Getting this wrong is the classic way to implement `?.`
incorrectly, and it is not something the tests will catch by accident.

Concretely: when the parser finishes building an accessor chain (`get_expr = atom { accessor }`) and
any accessor in it was null-safe, wrap the result in a new `NullSafeChainNode` holding the root
expression and the ordered accessor list. `Expand()` on that node produces, for a single null-safe
link:

```
let tmp = <root>
if tmp == null then <null result> else <rest of chain, rooted at tmp>
```

For multiple null-safe links (`a?.b?.c`), generate one shared "chain result" local and have every
null check branch to a single exit label, rather than nesting `if`s — nesting works but produces
quadratic IL on long chains and is harder to read in a disassembler.

### Result typing

Three cases, matching C#:

| Chain result type `T` | Expression type | Null case yields |
|---|---|---|
| reference type | `T` | `null` |
| non-nullable value type | `T?` (`Nullable<T>`) | `default(T?)` |
| `Unit` / void (`foo?.DoStuff()`) | `Unit` | no-op |

The `Nullable<T>` lifting is the fiddly one. Note it must lift only *once* for the whole chain, and
if the chain already ends in `T?` it must not double-wrap into `T??`.

`TypeExtensions` already has `IsNullableType` and `GetNullableUnderlyingType`, so the primitives
exist.

### Receiver typing

- Receiver is a **reference type** → ordinary null comparison.
- Receiver is `Nullable<T>` → check `.HasValue`, and the chain continues from `.Value`. Do not emit a
  null comparison against a `Nullable<T>`; it will box.
- Receiver is a **non-nullable value type** → compile error ("expression can never be null"). C#
  errors here and so should LENS; silently allowing it hides bugs.

### Restrictions to enforce

- **No null-safe lvalues.** `a?.b = c` is illegal in C# and should be illegal here. `lvalue_expr` and
  its accessor chain must reject null-safe accessors with a clear message, not fall through to a
  confusing downstream error.
- **No pointers through a null-safe accessor.** `IPointerProvider.RefArgumentRequired` /
  `PointerRequired` cannot be honoured across a conditional — there is no address to take when the
  receiver is null. `test (ref a?.b)` must be a clean compile error.

### Sharp edges

- **Lexer token ordering.** `?` is already significant in two places: nullable type suffixes
  (`type = namespace [type_args] { "[]" | "?" | "~" }`) and the `??` binary operator. Adding `?.` and
  `?[` means the static lexem table must match longest-first, and `??` must win over `?` + `?`.
  Verify the ordering in `LensLexer.ProcessStaticLexem` before writing the tokens — the table's
  iteration order is load-bearing.
- **`?.` vs a nullable type followed by a member access.** `int?.ToString()` is pathological but
  should be decided deliberately rather than by accident.
- **Interaction with `??`.** `a?.b ?? c` should work naturally once lifting is right, but it needs an
  explicit test: the left operand's type is now `T?` where it used to be `T`, and `??`'s existing
  type resolution has to cope.
- **Null-safe invocation.** `foo?.method arg` falls out of the design for free, because invocation
  wraps a getter node — but only if `NullSafeChainNode` wraps the chain *before* the invocation node
  is built. Check the parser's precedence here (`line_invoke_base_expr = get_expr [ invoke_line_args ]`).

### Tests

New `Features/SafeNavigationTest`:

- short-circuiting across a multi-link chain, asserting the later links were **not** evaluated (use a
  host-registered counter function to prove it);
- all three result-typing cases;
- all three receiver-typing cases, including the value-type compile error;
- `?[ ]` on arrays, lists, and dictionaries;
- null-safe invocation, including one returning Unit;
- `a?.b ?? c`;
- rejection of `a?.b = c` and of `ref a?.b`.

## Acceptance criteria

- Both features work on `net47` and `net8.0`.
- Short-circuit semantics proven by observable side effects, not just by result values.
- Error messages for the rejected cases name the construct, and point at the right source location.
- New compiler messages added to `CompilerMessages.resx` / `ParserMessages.resx` with translations,
  matching existing practice (`TranslationsTest` enforces this).
