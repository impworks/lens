module Lens.Parser.Indentation

open FParsec
open Lens.Parser.FParsecHelpers

let indent s = pstring "    " s
let indentCount s = (indent |>> fun _ -> 1
                    |> many
                    |>> Seq.sum) s

let getIndent (stream : CharStream<_>) =
    let state = stream.State
    stream.Seek stream.LineBegin
    let s = indentCount stream
    stream.BacktrackTo state
    s.Result

let skipIndent count =
    (let spaceCount = count * 4
     let isSpace c = c = ' '
     skipManyMinMaxSatisfy spaceCount spaceCount isSpace) <!> sprintf "skipIndent %d" count

(*let saveIndent : Parser<_, ParserState> =
    fun stream ->
        let indentation = (indentCount stream).Result
        stream.UserState <- { stream.UserState with Indentation = indentation }
        Reply()*)

let indentedBlock parser =
    (fun (stream : CharStream<ParserState>) ->
        let indentation = getIndent stream
        let elementParser = (skipNewline >>? skipIndent (indentation + 1) >>? parser)
                            <!> (sprintf "indentedLine %d" (indentation + 1))

        stream
        |> Inline.Many(stateFromFirstElement = (fun x -> [x]), 
                       foldState = (fun xs x -> x::xs),
                       resultFromState = List.rev,
                       elementParser = elementParser)) <!> "indentedBlock"
