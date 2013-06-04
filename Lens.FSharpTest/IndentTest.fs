namespace Lens.FSharpTest

open FParsec
open NUnit.Framework

open Lens.Parser
open Lens.Parser.FParsecHelpers
open Lens.Parser.Indentation

[<TestFixture>]
type IndentTest() =
    let testSuccess source parser state =
        match runParserOnString parser (ParserState.create()) "source" source with
        | Success(_, userState, _) -> Assert.AreEqual(state, userState)
        | other                    -> Assert.Fail <| sprintf "Parse failed: %A" other

    let x = pchar 'x'
    let y = pchar 'y'

    [<Test>]
    member this.SingleIndent() =
        let parser = indent .>>. x .>>. eof
        let source = "\n    x"
        testSuccess source parser { RealIndentation = 1
                                    VirtualIndentation = 1 }

    [<Test>]
    member this.DoubleIndent() =
        let parser = indent .>>. x .>>. indent .>>. x .>>. eof
        let source = "\n    x\n        x"
        testSuccess source parser { RealIndentation = 2
                                    VirtualIndentation = 2 }

    [<Test>]
    member this.Dedent() =
        let parser = indent .>>. x .>>. dedent .>>. x .>>. eof
        let source = "\n    x\nx"
        testSuccess source parser <| ParserState.create()

    [<Test>]
    member this.SingleIndentDedent() =
        let parser = indent .>>. y .>>. indent .>>. y .>>. dedent .>>. pchar 'x'
        let source = "\n    y\n        y\n    x"
        testSuccess source parser { RealIndentation = 1
                                    VirtualIndentation = 1 }

    [<Test>]
    member this.DoubleDedent() =
        let parser = indent .>>. y .>>. indent .>>. y .>>. dedent .>>. dedent .>>. pchar 'x'
        let source = "\n    y\n        y\nx"
        testSuccess source parser <| ParserState.create()

    [<Test>]
    member this.IndentedBlock() =
        let parser = pchar 'b' .>>. indentedBlockOf x .>>. pchar 'e' .>>. eof
        let source = "b\n    x\n    x\ne"
        testSuccess source parser <| ParserState.create()

    [<Test>]
    member this.TwoBlocks() =
        let parser = pchar '1'
                     .>>. indentedBlockOf (pchar '2' .>>. indentedBlockOf (pchar '3') .>>. pchar '2')
                     .>>. pchar '1'
                     .>>. eof
        let source = "1\n    2\n        3\n    2\n1"
        testSuccess source parser <| ParserState.create()
