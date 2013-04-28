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
        String.Format(ParserMessages.NestedErrors, produceErrorMessageList errors)
    | CompoundError(label, position, userState, errors) ->
        String.Format(ParserMessages.CompoundErrors, produceErrorMessageList errors)
    | Expected           label -> String.Format(ParserMessages.LexemExpected, label)
    | ExpectedString     str
    | ExpectedStringCI   str   -> String.Format(ParserMessages.StringExpected, str)
    | Unexpected         label -> String.Format(ParserMessages.Unexpected, label)
    | UnexpectedString   str
    | UnexpectedStringCI str   -> String.Format(ParserMessages.StringUnexpected, str)
    | Message            str   -> String.Format(ParserMessages.ErrorMessage, str)
    | OtherErrorMessage  o     -> String.Format(ParserMessages.ErrorCase, o)
    | other                    -> String.Format(ParserMessages.UnknownError, other)

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
    | ExpectedStringCI string -> String.Format(ParserMessages.QuoteString, string)
    | other                   -> failwith ParserMessages.ImpossibleHappened
    
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
    else let tokenString = String.concat ParserMessages.Or expectedTokens
         String.Format(ParserMessages.Expected, tokenString)

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
