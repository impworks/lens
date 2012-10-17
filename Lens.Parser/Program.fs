open FParsec

type Identifier = string

type Statement = Loop of Identifier * int * int * Statement list
               | Print of Identifier

let tabStopDistance = 8 // must be a power of 2

module IndentationParserWithBacktracking =
    type UserState = 
        {Indentation: int}
        with
           static member Create() = {Indentation = -1}

    type CharStream = CharStream<UserState>
    type Parser<'t> = Parser<'t, UserState>

    let skipIndentation (stream: CharStream) =
        let mutable indentation = stream.SkipNewlineThenWhitespace(tabStopDistance, false)
        while stream.Peek() = '#' do
            stream.SkipRestOfLine(false) // skip comment
            indentation <- stream.SkipNewlineThenWhitespace(tabStopDistance, false)
        indentation

    let indented p : Parser<_> =
        fun stream ->
            let state = stream.State
            let indentation = skipIndentation stream
            let expectedIndentation = stream.UserState.Indentation
            if indentation < expectedIndentation || stream.IsEndOfStream then
                stream.BacktrackTo(state)
                Reply(Error, NoErrorMessages)
            elif indentation = expectedIndentation then
                p stream
            else // indentation > expectedIndentation
                Reply(Error, messageError "wrong indentation")
        
    let indentedBlock p =
        Inline.Many(stateFromFirstElement = (fun x -> [x]), 
                    foldState = (fun xs x -> x::xs),
                    resultFromState = List.rev,
                    firstElementParser = p,
                    elementParser = indented p)        
      
    let indentedMany1 (p: Parser<'t>) label : Parser<'t list> =
        let indentedBlock = indentedBlock p
        fun stream ->
            let oldIndentation = stream.UserState.Indentation
            let indentation = skipIndentation stream
            if indentation <= oldIndentation then 
                Reply(Error, expected (if indentation < 0 then "newline" else "indented " + label))
            else
                stream.UserState <- {stream.UserState with Indentation = indentation}            
                let reply = indentedBlock stream
                if reply.Status = Ok then 
                    stream.UserState <- {stream.UserState with Indentation = oldIndentation}
                reply

module IndentationParserWithoutBacktracking =
    type LastParsedIndentation() =
        [<DefaultValue>]
        val mutable Value: int32
        [<DefaultValue>]
        val mutable EndIndex: int64

    type UserState = 
        {Indentation: int
         // We put LastParsedIndentation into the UserState so that we 
         // can conveniently use a separate instance for each stream.
         // The members of the LastParsedIndentation instance will be mutated
         // directly and hence won't be affected by any stream backtracking. 
         LastParsedIndentation: LastParsedIndentation}
        with
           static member Create() = {Indentation = -1
                                     LastParsedIndentation = LastParsedIndentation(EndIndex = -1L)}

    type CharStream = CharStream<UserState>
    type Parser<'t> = Parser<'t, UserState>

    // If this function is called at the same index in the stream
    // where the function previously stopped, then the previously
    // returned indentation will be returned again. 
    // This way we can avoid backtracking at the end of indented blocks.
    let skipIndentation (stream: CharStream) =    
        let lastParsedIndentation = stream.UserState.LastParsedIndentation
        if lastParsedIndentation.EndIndex = stream.Index then
            lastParsedIndentation.Value
        else
            let mutable indentation = stream.SkipNewlineThenWhitespace(tabStopDistance, false)
            while stream.Peek() = '#' do
                stream.SkipRestOfLine(false) // skip comment
                indentation <- stream.SkipNewlineThenWhitespace(tabStopDistance, false)
            lastParsedIndentation.EndIndex <- stream.Index
            lastParsedIndentation.Value <- indentation
            indentation

    let indentedMany1 (p: Parser<'t>) label : Parser<'t list> =
        fun stream ->
            let oldIndentation = stream.UserState.Indentation
            let indentation = skipIndentation stream
            if indentation <= oldIndentation then 
                Reply(Error, expected (if indentation < 0 then "newline" else "indented " + label))
            else
                stream.UserState <- {stream.UserState with Indentation = indentation}            
                let results = ResizeArray()
                let mutable stateTag = stream.StateTag
                let mutable reply = p stream // parse the first element
                let mutable newIndentation = 0
                while reply.Status = Ok 
                      && (results.Add(reply.Result)
                          newIndentation <- skipIndentation stream
                          newIndentation = indentation)
                   do
                     stateTag <- stream.StateTag
                     reply <- p stream
                if reply.Status = Ok 
                   || (stream.IsEndOfStream && results.Count > 0 && stream.StateTag = stateTag) 
                then
                    if newIndentation < indentation || stream.IsEndOfStream then
                        stream.UserState <- {stream.UserState with Indentation = oldIndentation}
                        Reply(List.ofSeq results)
                    else
                        Reply(Error, messageError "wrong indentation")
                else // p failed
                    Reply(reply.Status, reply.Error) 

open IndentationParserWithoutBacktracking

let isBlank = fun c -> c = ' ' || c = '\t'
let ws1 = skipMany1SatisfyL isBlank "whitespace"
let comment = pstring "#" >>. skipRestOfLine false
let wsBeforeEOL = skipManySatisfy isBlank >>. optional comment
let identifier = asciiLower |>> string
let keyword str = pstring str >>? nextCharSatisfiesNot (fun c -> isLetter c || isDigit c) <?> str

let indentedStatements, indentedStatementsRef = createParserForwardedToRef()

let print = keyword "print" >>. (ws1 >>. identifier .>> wsBeforeEOL |>> Print)
let loop = keyword "loop" >>. (pipe4 (ws1 >>. identifier) (ws1 >>. pint32) (ws1 >>. pint32 .>> wsBeforeEOL) 
                                     indentedStatements
                                     (fun id min max stmts -> Loop(id, min, max, stmts)))
let statement = print <|> loop

do indentedStatementsRef := indentedMany1 statement "statement"

let document = indentedStatements .>> spaces .>> eof

let test str =
    match runParserOnString document (UserState.Create()) "" str with
    | Success(result, _, _)   -> printfn "Success: %A" result
    | Failure(errorMsg, _, _) -> printfn "Failure: %s" errorMsg

test @"
loop i 1 10
  loop k 1 10
    print k
  print i
print j

";
