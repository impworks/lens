namespace Lens.Parser

open FParsec

open Lens.SyntaxTree
open Lens.SyntaxTree.SyntaxTree

type TreeBuilder() =
    member this.Parse source : NodeBase seq =
        match run Grammar.main source with
        | Success(result,  _, _) -> result :> NodeBase seq
        | Failure(message, _, _) -> failwith message
