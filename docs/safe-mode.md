# Safe mode

Safe mode restricts what a LENS script is allowed to name. It exists so that a host can compile a
script it did not write without that script reaching the file system, the network, or the host's own
internals.

Read the last section before relying on it. Safe mode is a compile-time name filter and not a
security boundary, and the difference matters.

## Turning it on

```csharp
var options = new LensCompilerOptions
{
    SafeMode = SafeMode.Whitelist,
    SafeModeExplicitNamespaces = { "System.Collections.Generic", "System.Linq" },
    SafeModeExplicitTypes = { "System.Int32", "System.String", "System.Math" }
};
```

`SafeMode` has three values:

| Mode | Meaning |
|---|---|
| `Disabled` | No restrictions. The default. |
| `Blacklist` | Everything is allowed except what the rules name. |
| `Whitelist` | Only what the rules name is allowed. |

## Rules

There are four ways to write a rule.

**Namespaces** — `SafeModeExplicitNamespaces`. A rule naming `System.Net` covers `System.Net` and
every namespace nested in it, and stops at a dot: it does not cover a namespace that merely starts
with the same letters.

Note what this means for a whitelist: the whole of the BCL lives under `System`, so whitelisting
`System` is close to no restriction at all. Name the namespaces you actually want.

**Types** — `SafeModeExplicitTypes`, by full name. A rule covers the type, arrays and by-ref forms
of it, the types nested inside it, and every instantiation of it if it is generic. Generic types may
be written either way: `System.Collections.Generic.List` and
``System.Collections.Generic.List`1`` both match `List<int>`.

**Members** — `SafeModeExplicitMembers`, as `Namespace.Type::Member`. A member rule matches every
overload of the name, and matches the member as reached through a derived type as well as through
the one it is declared on.

Member rules are the one asymmetric part of the design: **they always deny, in both modes.** A
whitelist of members would mean naming every method a script is allowed to call, which is not a list
anyone maintains correctly, so a member rule subtracts from what the type rules allowed and never
adds to it. This is the granularity for "this type is fine, that one call on it is not":

```csharp
SafeModeExplicitMembers = { "System.String::ToUpper" }   // the rest of String is untouched
```

**Subsystems** — `SafeModeExplicitSubsystems`, a flags enum covering `Network`, `IO`, `Reflection`,
`Threading` and `Environment`. Each one expands into namespace, type and member rules, and is read
the same way as the lists: denied under a blacklist, allowed under a whitelist.

A malformed member rule throws rather than being ignored, and surrounding whitespace and blank
entries in any list are dropped. A rule that silently fails to match is the worst outcome available
here, because nothing goes wrong until something does.

## Where a rule is applied

Every mention of a type in a script goes through the rules — the type of every expression, and also
the places where a type is named but the expression's own type is something else:

- the operand of `typeof`, `is`, `as` and `default`
- the type in a `catch` clause and in a `case x:Type` pattern
- the argument and return types of a function, and the field types of a `record` or `type`
- the target of a `declare type` alias
- the type an extension method is declared on, which the script never names itself

A type is checked structurally rather than by its outer name: an array is as allowed as its element
type, a constructed generic as its definition and every one of its arguments.

Two things are deliberately always allowed. A generic parameter is not a type — `T` is whatever it
is substituted with, and that substitution is what the argument check asks about. A type the script
declared is the script's own; the host types its fields and methods are built out of are checked
where they appear.

## Core restrictions

Any mode other than `Disabled` also switches on a set of restrictions **the host's own rules cannot
lift**, in either direction:

- **The compiler itself.** The `Lens` namespace is denied. A script that can name `LensCompiler`
  constructs a second one with the default options — which is to say with no safe mode — and runs
  whatever it likes through it.
- **Reflection.** `System.Reflection` (including `.Emit`), `System.Runtime.Loader`,
  `System.Runtime.InteropServices`, `System.Activator`, `System.AppDomain`, `System.Delegate` and
  the runtime handle types are denied, as are `System.Type::GetType` and `System.Type::InvokeMember`.
- **`System.Type` itself stays available**, so `typeof` and `is` keep working. A `Type` on its own
  does nothing; what is denied is the two members that turn one into code, and everything that can
  be reached from one.

These are not capabilities a host chooses between. Each is a way around every other rule: reflection
turns a string into an invocation, so no rule about which types may be named can see it coming, and
`Type.GetType` in particular is the entire filter defeated in one call. A blacklist that does not
close these doors is not a restriction but a suggestion.

`declare reference` is refused in any safe mode for the same reason: loading an assembly runs its
module initializers whatever the type checks say.

## What safe mode does not do

**It is not a security boundary.** The IL a script compiles to runs in the host process with the
host's full trust, and .NET has had no in-process trust boundary since Code Access Security was
removed. Safe mode narrows what a script can *name*; it cannot contain what a script can *do* once
it names something allowed. For code from a genuinely untrusted source, the boundary has to be the
process or the machine — a container, a separate process with an OS-level sandbox, or WebAssembly.
The playground runs in the browser for exactly this reason, and its own blacklist exists only to
turn a `PlatformNotSupportedException` from inside the BCL into a sentence that names the API.

**There are no resource limits.** `while true do ...` compiles and runs forever, a recursive
function will overflow the stack, and a loop that appends to a list will exhaust memory. Nothing in
safe mode bounds time, allocation or stack depth. A host that runs scripts it did not write needs a
timeout and a memory ceiling of its own, and both belong outside the compiler.

**A blacklist is a guess.** It is the right tool when the host knows the small set of things it does
not want and trusts the author otherwise — a config script, a rules engine, an in-house macro. It is
the wrong tool for a script from outside: you are enumerating the ways in, and the surface of the BCL
is larger than any list. Every escape the core restrictions above now close was found by asking of a
blacklist "and what reaches the same capability without matching a rule?", and the answer was never
hard to find. A whitelist is the only posture where a missing rule fails closed.

## Choosing a posture

- **Host code you trust, restricted for tidiness** — `Blacklist` with the subsystems you do not
  want.
- **Script from a known author, wrong turns to be caught early** — `Blacklist`, and read it as a
  guard rail rather than as a wall.
- **Script from outside** — `Whitelist`, naming the namespaces and types the script legitimately
  needs, *and* an OS-level boundary around the process, *and* a timeout. Safe mode is one of the
  three, not a substitute for the other two.
