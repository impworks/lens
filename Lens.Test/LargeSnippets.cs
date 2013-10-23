using Lens.SyntaxTree;
using NUnit.Framework;

namespace Lens.Test
{
	[TestFixture]
	class LargeSnippets : TestBase
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
						new [] { Expr.Arg("x", "int"), Expr.Arg("y", "int") },
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
							new [] { Expr.Arg("x", "int"), Expr.Arg("y", "int") },
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
							new [] { Expr.Arg("x", "int") },
							Expr.Invoke(
								Expr.Int(10),
								"times",
								Expr.Lambda(
									new [] { Expr.Arg("y", "int") },
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

			Test(src, nodes);
		}
	}
}
