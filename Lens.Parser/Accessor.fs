module Lens.Parser.Accessor

open Lens.SyntaxTree.SyntaxTree
open Lens.SyntaxTree.SyntaxTree.Expressions

type Accessor =
| Member of string
| Indexer of NodeBase

let accessorGetter accessor : AccessorNodeBase =
    match accessor with
    | Member(name)        -> upcast GetMemberNode(MemberName = name)
    | Indexer(expression) -> upcast GetIndexNode(Index = expression)

let accessorSetter accessor value : AccessorNodeBase =
    match accessor with
    | Member  name       -> upcast SetMemberNode(MemberName = name, Value = value)
    | Indexer expression -> upcast SetIndexNode(Index = expression, Value = value)
