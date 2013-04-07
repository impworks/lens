namespace Lens.Parser

type ParserState = {
    RealIndentation : int
    VirtualIndentation : int
}
    with
        static member create() = { RealIndentation = 0
                                   VirtualIndentation = 0 }
