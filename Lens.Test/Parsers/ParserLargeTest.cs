using Lens.SyntaxTree;
using Lens.SyntaxTree.ControlFlow;
using NUnit.Framework;

namespace Lens.Test.Parsers
{
    [TestFixture]
    internal class LargeSnippets : TestBase
    {
        [Test]
        public void GraphicScript1()
        {
            var src = @"
fun maker:Rect (x:int y:int) ->
    let r = new Rect ()
    r.X = (x + 1) * 50
    r.Y = (y + 1) * 50
    r.Focus = ->
        r.Fill = System.Windows.Media.Color::FromRgb
            <| (rand 100 255) as byte
            <| (rand 100 255) as byte
            <| (rand 100 255) as byte
    r

let create = (x:int y:int) -> Screen.Add (maker x y)
10.times (x:int -> 10.times (y:int -> create x y))
";
            var nodes = new NodeBase[]
            {
                Expr.Fun(
                    "maker",
                    "Rect",
                    new[] {Expr.Arg("x", "int"), Expr.Arg("y", "int")},
                    Expr.Let("r", Expr.New("Rect")),
                    Expr.SetMember(
                        Expr.Get("r"),
                        "X",
                        Expr.Mult(
                            Expr.Add(Expr.Get("x"), Expr.Int(1)),
                            Expr.Int(50)
                        )
                    ),
                    Expr.SetMember(
                        Expr.Get("r"),
                        "Y",
                        Expr.Mult(
                            Expr.Add(Expr.Get("y"), Expr.Int(1)),
                            Expr.Int(50)
                        )
                    ),
                    Expr.SetMember(
                        Expr.Get("r"),
                        "Focus",
                        Expr.Lambda(
                            Expr.SetMember(
                                Expr.Get("r"),
                                "Fill",
                                Expr.Invoke(
                                    "System.Windows.Media.Color",
                                    "FromRgb",
                                    Expr.Cast(
                                        Expr.Invoke("rand", Expr.Int(100), Expr.Int(255)),
                                        "byte"
                                    ),
                                    Expr.Cast(
                                        Expr.Invoke("rand", Expr.Int(100), Expr.Int(255)),
                                        "byte"
                                    ),
                                    Expr.Cast(
                                        Expr.Invoke("rand", Expr.Int(100), Expr.Int(255)),
                                        "byte"
                                    )
                                )
                            )
                        )
                    ),
                    Expr.Get("r")
                ),

                Expr.Let(
                    "create",
                    Expr.Lambda(
                        new[] {Expr.Arg("x", "int"), Expr.Arg("y", "int")},
                        Expr.Invoke(
                            Expr.Get("Screen"),
                            "Add",
                            Expr.Invoke(
                                "maker",
                                Expr.Get("x"),
                                Expr.Get("y")
                            )
                        )
                    )
                ),

                Expr.Invoke(
                    Expr.Int(10),
                    "times",
                    Expr.Lambda(
                        new[] {Expr.Arg("x", "int")},
                        Expr.Invoke(
                            Expr.Int(10),
                            "times",
                            Expr.Lambda(
                                new[] {Expr.Arg("y", "int")},
                                Expr.Invoke(
                                    "create",
                                    Expr.Get("x"),
                                    Expr.Get("y")
                                )
                            )
                        )
                    )
                )
            };

            TestParser(src, nodes);
        }

        [Test]
        public void ComplexWhileTest()
        {
            var src = @"
use System.Net

let listener = new HttpListener ()
listener.Prefixes.Add ""http://127.0.0.1:8080/""
listener.Prefixes.Add ""http://localhost:8080/""

var count = 1

while true do
    listener.Start ()

    let ctx = listener.GetContext ()
    let rq = ctx.Request
    let resp = ctx.Response

    let respStr = fmt ""Hello from LENS! This page has been viewed {0} times."" count
    let buf = Encoding::UTF8.GetBytes respStr

    resp.ContentLength64 = buf.Length
    let output = resp.OutputStream
    output.Write buf 0 (buf.Length)
    output.Close ()

    listener.Stop ()

    count = count + 1
";
            var nodes = new NodeBase[]
            {
                new UseNode {Namespace = "System.Net"},
                Expr.Let("listener", Expr.New("HttpListener")),
                Expr.Invoke(
                    Expr.GetMember(
                        Expr.GetMember(Expr.Get("listener"), "Prefixes"),
                        "Add"
                    ),
                    Expr.Str("http://127.0.0.1:8080/")
                ),
                Expr.Invoke(
                    Expr.GetMember(
                        Expr.GetMember(Expr.Get("listener"), "Prefixes"),
                        "Add"
                    ),
                    Expr.Str("http://localhost:8080/")
                ),
                Expr.Var("count", Expr.Int(1)),
                Expr.While(
                    Expr.True(),
                    Expr.Block(
                        Expr.Invoke(Expr.Get("listener"), "Start"),
                        Expr.Let("ctx", Expr.Invoke(Expr.Get("listener"), "GetContext")),
                        Expr.Let("rq", Expr.GetMember(Expr.Get("ctx"), "Request")),
                        Expr.Let("resp", Expr.GetMember(Expr.Get("ctx"), "Response")),
                        Expr.Let(
                            "respStr",
                            Expr.Invoke(
                                Expr.Get("fmt"),
                                Expr.Str("Hello from LENS! This page has been viewed {0} times."),
                                Expr.Get("count")
                            )
                        ),
                        Expr.Let(
                            "buf",
                            Expr.Invoke(
                                Expr.GetMember(
                                    Expr.GetMember("Encoding", "UTF8"),
                                    "GetBytes"
                                ),
                                Expr.Get("respStr")
                            )
                        ),
                        Expr.SetMember(
                            Expr.Get("resp"),
                            "ContentLength64",
                            Expr.GetMember(Expr.Get("buf"), "Length")
                        ),
                        Expr.Let("output", Expr.GetMember(Expr.Get("resp"), "OutputStream")),
                        Expr.Invoke(
                            Expr.Get("output"),
                            "Write",
                            Expr.Get("buf"),
                            Expr.Int(0),
                            Expr.GetMember(Expr.Get("buf"), "Length")
                        ),
                        Expr.Invoke(
                            Expr.Get("output"),
                            "Close"
                        ),
                        Expr.Invoke(
                            Expr.Get("listener"),
                            "Stop"
                        ),
                        Expr.Inc("count")
                    )
                )
            };

            TestParser(src, nodes);
        }
    }
}