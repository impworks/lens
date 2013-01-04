namespace Lens.Parser

type ParserState = {
    Indentation: int
}
    with
        static member Create() = {Indentation = -1}
