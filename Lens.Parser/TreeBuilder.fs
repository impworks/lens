namespace Lens.Parser

open Lens.SyntaxTree
open Lens.SyntaxTree.SyntaxTree

type TreeBuilder() =
    member this.Parse (source : string) : NodeBase seq =
        raise <| new ParseException "Not implemented"
