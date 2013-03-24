module Lens.Parser.ErrorProvider

open FParsec
open FParsec.Error

/// Set to false when debugging complex parser failures.
let enabled = false

let rec messageSeq (list : ErrorMessageList) : ErrorMessage seq =
    match list with
    | null -> Seq.empty
    | _    -> Seq.concat [Seq.singleton list.Head
                          messageSeq list.Tail]

let rec private produceMessage (message : ErrorMessage) : string =
    match message with
    | Expected           label -> sprintf "Expected %s" label
    | ExpectedString     str
    | ExpectedStringCI   str   -> sprintf "Expected string %s" str
    | Unexpected         label -> sprintf "Unexpected %s" label
    | UnexpectedString   str
    | UnexpectedStringCI str   -> sprintf "Unexpected string %s" str
    | Message            str   -> sprintf "Exceptional message: %s" str
    | NestedError(position, userState, errors)          -> sprintf "Nested error: %s"   <| produceErrorMessageList errors
    | CompoundError(label, position, userState, errors) -> sprintf "Compound error: %s" <| produceErrorMessageList errors
    | OtherErrorMessage  o     -> sprintf "Exceptional case: %A" o
    | other                    -> sprintf "Unknown parse error: %A" other

and private produceErrorMessageList (messages : ErrorMessageList) : string =
    messageSeq messages
    |> Seq.map produceMessage
    |> String.concat "\n"

let produce (message : string) (error : ParserError) (userState : ParserState) : string =
    if enabled then
        let position = error.Position
        sprintf "At line %d, column %d: %s" position.Line position.Column <| produceErrorMessageList error.Messages
    else message
