module Lens.Parser.Symbol

open Lens.SyntaxTree.SyntaxTree
open Lens.SyntaxTree.SyntaxTree.Expressions
open Lens.SyntaxTree.Utils

type Symbol =
| Static of string * string // type * name
| Local  of string

let symbolGetter symbol : NodeBase =
    match symbol with
    | Local name             -> upcast GetIdentifierNode(Identifier = name)
    | Static(typeName, name) -> upcast GetMemberNode(StaticType = TypeSignature typeName, MemberName = name)

let symbolSetter symbol value : NodeBase =
    match symbol with
    | Local name             -> upcast SetIdentifierNode(Identifier = name, Value = value)
    | Static(typeName, name) -> upcast SetMemberNode(
                                    StaticType = TypeSignature typeName,
                                    MemberName = name,
                                    Value = value)
