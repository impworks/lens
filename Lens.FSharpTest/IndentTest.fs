namespace Lens.FSharpTest

open FParsec
open NUnit.Framework

open Lens.Parser
open Lens.Parser.Indentation

[<TestFixture>]
type IndentTest() =
    let testSuccess source parser state =
        match runParserOnString parser (ParserState.Create()) "source" source with
        | Success(_, userState, _) -> Assert.AreEqual(state, userState)
        | other                    -> Assert.Fail <| sprintf "Parse failed: %A" other

    [<Test>]
    member this.Sanity() =
        let parser = parse { do! skipNewline
                             do! skipManyMinMaxSatisfy 4 4 (fun c -> c = ' ') }
        let source = "\n    "
        testSuccess source parser <| ParserState.Create()

    [<Test>]
    member this.SingleIndent() =
        let parser = indent
        let source = "\n    "
        testSuccess source parser { RealIndentation = 1
                                    VirtualIndentation = 1 }