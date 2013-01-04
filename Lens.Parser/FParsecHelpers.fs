module Lens.Parser.FParsecHelpers

open FParsec

let debug = false

let (<!>) (p : Parser<_,_>) label : Parser<_,_> =
    if debug then
        fun stream ->
            printfn "%A: Entering %s" stream.Position label
            let reply = p stream
            printfn "%A: Leaving %s (%A)" stream.Position label reply.Status
            reply
    else
        p
