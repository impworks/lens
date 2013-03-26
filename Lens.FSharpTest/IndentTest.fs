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
    member this.SingleIndent() =
        let parser = parse {
            do! indent
        }

        let source = "
    "
        testSuccess source parser { RealIndentation = 1
                                    VirtualIndentation = 1 }