open FParsec

let indent s = pstring "    " s
let indentCount s = (indent |>> fun _ -> 1
                    |> many
                    |>> Seq.sum) s
let skipIndent count =
    let spaceCount = count * 4
    let isSpace c = c = ' '
    parse { do! skipManyMinMaxSatisfy spaceCount spaceCount isSpace }

let functionBody = pint32 // TODO

let functionDefinition s = 
    (parse {
        let! i = indentCount
        do! skipString "fun"
        do! skipNewline
        do! skipIndent i
        let! body = functionBody
        return body
    }) s

let test p str =
    match run p str with
    | Success(result, _, _)   -> printfn "Success: %A" result
    | Failure(errorMsg, _, _) -> printfn "Failure: %s" errorMsg

test functionDefinition  ("    fun\n" +
                          "    123")