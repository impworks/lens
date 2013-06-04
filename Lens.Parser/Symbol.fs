module Lens.Parser.Symbol

open Lens.Parser.Accessor
open Lens.SyntaxTree.SyntaxTree
open Lens.SyntaxTree.SyntaxTree.Expressions
open Lens.SyntaxTree.Compiler

type Symbol =
| Static     of TypeSignature * string
| Local      of string
| Expression of NodeBase * Accessor

let symbolGetter symbol : NodeBase =
    match symbol with
    | Local name                       -> upcast GetIdentifierNode(Identifier = name)
    | Static(typeSig, name)            -> upcast GetMemberNode(StaticType = typeSig, MemberName = name)
    | Expression(expression, accessor) ->
        let node = accessorGetter accessor
        node.Expression <- expression
        upcast node

let symbolSetter symbol value : NodeBase =
    match symbol with
    | Local name                       -> upcast SetIdentifierNode(Identifier = name, Value = value)
    | Static(typeSig, name)            -> upcast SetMemberNode(
                                              StaticType = typeSig,
                                              MemberName = name,
                                              Value = value)
    | Expression(expression, accessor) ->
        let node = accessorSetter accessor value
        node.Expression <- expression
        upcast node
