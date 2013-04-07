namespace Lens.Parser

open FParsec

open Lens.SyntaxTree
open Lens.SyntaxTree.SyntaxTree

type TreeBuilder() =
    member this.Parse source : NodeBase seq =
        match runParserOnString Grammar.main (ParserState.create()) "source" source with
        | Success(result,  _, _)             -> result :> NodeBase seq
        | Failure(message, error, userState) -> raise <| ErrorProvider.getException message error userState
