namespace Lens.FSharpTest

open FParsec
open NUnit.Framework

open Lens.Parser
open Lens.Parser.FParsecHelpers
open Lens.Parser.Indentation

[<TestFixture>]
type IndentTest() =
    let testSuccess source parser state =
        match runParserOnString parser (ParserState.Create()) "source" source with
        | Success(_, userState, _) -> Assert.AreEqual(state, userState)
        | other                    -> Assert.Fail <| sprintf "Parse failed: %A" other

    [<Test>]
    member this.SingleIndent() =
        let parser = indent .>>. eof
        let source = "\n    "
        testSuccess source parser { RealIndentation = 1
                                    VirtualIndentation = 1 }

    [<Test>]
    member this.DoubleIndent() =
        let parser = indent .>>. indent .>>. eof
        let source = "\n    \n        "
        testSuccess source parser { RealIndentation = 2
                                    VirtualIndentation = 2 }

    [<Test>]
    member this.Dedent() =
        let parser = indent .>>. dedent .>>. eof
        let source = "\n    \n"
        testSuccess source parser <| ParserState.Create()

    [<Test>]
    member this.SingleIndentDedent() =
        let parser = indent .>>. indent .>>. dedent .>>. pchar 'x'
        let source = "\n    \n        \n    x"
        testSuccess source parser { RealIndentation = 1
                                    VirtualIndentation = 1 }

    [<Test>]
    member this.DoubleDedent() =
        let parser = indent .>>. indent .>>. dedent .>>. dedent .>>. pchar 'x'
        let source = "\n    \n        \nx"
        testSuccess source parser <| ParserState.Create()

    [<Test>]
    member this.IndentedBlock() =
        let block = pchar 'b' .>>. indentedBlockOf (pchar 'x') .>>. pchar 'e' .>>. eof
        let source = "b\n    x\n    x\ne"
        testSuccess source block <| ParserState.Create()

    [<Test>]
    member this.TwoBlocks() =
        let parser = pchar '1'
                     .>>. indentedBlockOf (pchar '2' .>>. indentedBlockOf (pchar '3') .>>. pchar '2')
                     .>>. pchar '1'
                     .>>. eof
        let source = "1\n    2\n        3\n    2\n1"
        testSuccess source parser <| ParserState.Create()
