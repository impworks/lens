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

let rec private produceMessage (message : ErrorMessage) : string =
    match message with
    | NestedError(position, userState, errors)          ->
        sprintf "Nested errors: %s"   <| produceErrorMessageList errors
    | CompoundError(label, position, userState, errors) ->
        sprintf "Compound errors: %s" <| produceErrorMessageList errors
    | Expected           label -> String.Format(CompilerMessages.LexemExpected, label)
    | ExpectedString     str
    | ExpectedStringCI   str   -> String.Format(CompilerMessages.StringExpected, str)
    | Unexpected         label -> sprintf "Unexpected %s" label
    | UnexpectedString   str
    | UnexpectedStringCI str   -> sprintf "Unexpected string '%s'" str
    | Message            str   -> sprintf "Exceptional message: %s" str
    | OtherErrorMessage  o     -> sprintf "Exceptional case: %A" o
    | other                    -> sprintf "Unknown parse error: %A" other

and private produceErrorMessageList (messages : ErrorMessageList) : string =
    let expectedFilter = function
    | Expected         string
    | ExpectedString   string
    | ExpectedStringCI string
        when not (String.IsNullOrWhiteSpace string) -> true
    | other                                         -> false

    let getLabel = function
    | Expected         label  -> label
    | ExpectedString   string 
    | ExpectedStringCI string -> sprintf "\"%s\"" string
    | other                   -> failwith "Impossible happened"
    
    let errors = messageSeq messages
    let expectedTokens =
        errors
        |> Seq.filter expectedFilter
        |> Seq.map getLabel
        |> Seq.distinct
        |> Seq.sort
        |> Seq.cache
        // TODO: Dispatch message by token
    
    if Seq.isEmpty expectedTokens
    then Seq.head errors |> produceMessage
    else let tokenString = String.concat "\n      or " expectedTokens
         String.Format("Expected {0}", tokenString)

let private getMessage (message : string) (error : ParserError) (userState : ParserState) : string =
    if enabled then
        produceErrorMessageList error.Messages
    else message

let getException message (error : ParserError) userState =
    let ex = LensCompilerException(getMessage message error userState)
    let location = LexemLocation(Line = int error.Position.Line, Offset = int error.Position.Column)
    let locationEntity = LocationEntity(StartLocation = location, EndLocation = location)
    ex.BindToLocation locationEntity
    ex
