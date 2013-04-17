module Lens.Parser.ErrorProvider

open System

open FParsec
open FParsec.Error

open Lens.SyntaxTree
open Lens.SyntaxTree.SyntaxTree
open Lens.SyntaxTree.Translations

/// Set to false when debugging complex parser failures.
let enabled = true

let rec messageSeq (list : ErrorMessageList) : ErrorMessage seq =
    match list with
    | null -> Seq.empty
    | _    -> Seq.concat [Seq.singleton list.Head
                          messageSeq list.Tail]

let rec private produceMessage (indent: int) (expectedWordWritten: bool) (message : ErrorMessage) : bool * string =
    let expected = if not expectedWordWritten
                   then "Expected "
                   else "      or "
    
    // TODO: Filter string and lexem lists by unique values. Sort them.
    let result = 
        match message with
        | NestedError(position, userState, errors)          ->
            false, sprintf "Nested errors: %s"   <| produceErrorMessageList (indent + 1) errors
        | CompoundError(label, position, userState, errors) ->
            false, sprintf "Compound errors: %s" <| produceErrorMessageList (indent + 1) errors
        | Expected           label -> true, expected + String.Format(CompilerMessages.LexemExpected, label)
        | ExpectedString     str
        | ExpectedStringCI   str   -> true, expected + String.Format(CompilerMessages.StringExpected, str)
        | Unexpected         label -> false, sprintf "Unexpected %s" label
        | UnexpectedString   str
        | UnexpectedStringCI str   -> false, sprintf "Unexpected string '%s'" str
        | Message            str   -> false, sprintf "Exceptional message: %s" str
        | OtherErrorMessage  o     -> false, sprintf "Exceptional case: %A" o
        | other                    -> false, sprintf "Unknown parse error: %A" other
    let indentString = List.replicate indent "    " |> String.concat String.Empty
    (fst result, indentString + snd result)

and private produceErrorMessageList (indent: int) (messages : ErrorMessageList) : string =
    let msgSeq = messageSeq messages
    let converter (expected, msg) error = let expected', msg' = produceMessage indent expected error
                                          (expected || expected', msg + "\n" + msg')
    let state = (false, String.Empty)
    snd <| Seq.fold converter state msgSeq

let private getMessage (message : string) (error : ParserError) (userState : ParserState) : string =
    if enabled then
        let position = error.Position
        sprintf "At line %d, column %d: %s" position.Line position.Column <| produceErrorMessageList 0 error.Messages
    else message

let getException message (error : ParserError) userState =
    let ex = LensCompilerException(getMessage message error userState)
    let location = LexemLocation(Line = int error.Position.Line, Offset = int error.Position.Column)
    let locationEntity = LocationEntity(StartLocation = location, EndLocation = location)
    ex.BindToLocation locationEntity
    ex
