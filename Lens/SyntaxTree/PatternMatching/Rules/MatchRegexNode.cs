using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text.RegularExpressions;

using Lens.Compiler;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.PatternMatching.Rules
{
	class MatchRegexNode : MatchRuleBase
	{
		#region Static constants

		/// <summary>
		/// Available regex modifiers.
		/// </summary>
		private static readonly Dictionary<char, RegexOptions> AvailableModifiers = new Dictionary<char, RegexOptions>
		{
			{'i', RegexOptions.IgnoreCase},
			{'m', RegexOptions.Multiline},
			{'s', RegexOptions.Singleline},
			{'c', RegexOptions.CultureInvariant}
		};

		/// <summary>
		/// Named group name's pattern.
		/// </summary>
		private static readonly Regex NamedGroupPattern = new Regex(@"\(\?<(?<name>[a-z0-9_]+)(?::(?<type>[a-z\0-9_]+))?>", RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase);

		#endregion

		#region Fields

		/// <summary>
		/// The source regex string.
		/// </summary>
		public string Value;

		/// <summary>
		/// The modifier string.
		/// </summary>
		public string Modifiers;

		private string _Regex;
		private RegexOptions _Options;
		private List<PatternNameBinding> _NamedGroups;

		#endregion

		#region Resolve

		public override IEnumerable<PatternNameBinding> Resolve(Context ctx, Type expressionType)
		{
			if(expressionType != typeof(string))
				Error(CompilerMessages.PatternTypeMismatch, expressionType, typeof(string));

			parseValue(ctx);
			parseModifiers();

			return _NamedGroups;
		}

		/// <summary>
		/// Finds named groups, checks their types and fixes the regex string.
		/// </summary>
		private void parseValue(Context ctx)
		{
			_NamedGroups = new List<PatternNameBinding>();
			var groups = NamedGroupPattern.Matches(Value);
			foreach (Match group in groups)
			{
				if (!group.Success)
					continue;

				var name = group.Groups["name"].Value;
				var type = group.Groups["type"].Value;

				if(name == "_")
					Error(CompilerMessages.UnderscoreNameUsed);

				// no type specified: assume string
				if (string.IsNullOrEmpty(type))
				{
					_NamedGroups.Add(new PatternNameBinding(name, typeof(string)));
					continue;
				}

				var actualType = wrapError(
					() => ctx.ResolveType(type),
					CompilerMessages.RegexConverterTypeNotFound, type
				);

				wrapError(
					() => ctx.ResolveMethod(actualType, "TryParse", new[] {typeof (string), actualType}),
					CompilerMessages.RegexConverterTypeIncompatible, type
				);

				_NamedGroups.Add(new PatternNameBinding(name, actualType));
			}

			_Regex = NamedGroupPattern.Replace(Value, "(?<$1>");

			wrapError(() => new Regex(_Regex), CompilerMessages.RegexSyntaxError);
		}

		/// <summary>
		/// Converts the string of modifiers into a RegexOptions set.
		/// </summary>
		private void parseModifiers()
		{
			_Options = RegexOptions.ExplicitCapture;
			var last = _Options;

			foreach (var mod in Modifiers)
			{
				RegexOptions curr;
				if(!AvailableModifiers.TryGetValue(mod, out curr))
					Error(CompilerMessages.RegexUnknownModifier, mod);

				last |= curr;
				if (last == _Options)
					Error(CompilerMessages.RegexDuplicateModifier, mod);

				_Options = last;
			}
		}

		#endregion

		#region Expand

		public override IEnumerable<NodeBase> Expand(Context ctx, NodeBase expression, Label nextStatement)
		{
			var regexExpr = Expr.New(
				typeof (Regex),
				Expr.Str(_Regex),
				Expr.RawEnum<RegexOptions>((long) _Options)
			);

			if (_NamedGroups.Count == 0)
			{
				yield return MakeJumpIf(
					nextStatement,
					Expr.Not(
						Expr.Invoke(
							regexExpr,
							"IsMatch",
							expression
						)
					)
				);
				yield break;
			}

			// var match = new Regex(...).Match(str)
			// if not match.Success then Jump!
			var matchVar = ctx.Scope.DeclareImplicit(ctx, typeof (Match), false);
			yield return Expr.Set(
				matchVar,
				Expr.Invoke(
					regexExpr,
					"Match",
					expression
				)
			);
			yield return MakeJumpIf(
				nextStatement,
				Expr.Not(
					Expr.GetMember(
						Expr.Get(matchVar),
						"Success"
					)
				)
			);

			foreach (var curr in _NamedGroups)
			{
				// match.Groups['name'].Value
				var valueExpr = Expr.GetMember(
					Expr.GetIdx(
						Expr.GetMember(
							Expr.Get(matchVar),
							"Groups"
						),
						Expr.Str(curr.Name)
					),
					"Value"
				);

				if (curr.Type == typeof (string))
				{
					// name = value
					yield return Expr.Set(
						curr.Name,
						valueExpr
					);
				}
				else
				{
					yield return MakeJumpIf(
						nextStatement,
						Expr.Not(
							Expr.Invoke(
								curr.Type.FullName,
								"TryParse",
								valueExpr,
								Expr.Ref(Expr.Get(curr.Name))
							)
						)
					);
				}
			}
		}

		#endregion

		#region Helpers

		/// <summary>
		/// Return a value if no exception arises, or throw a customized exception otherwise.
		/// </summary>
		private T wrapError<T>(Func<T> action, string msg, params object[] args)
		{
			try
			{
				return action();
			}
			catch
			{
				Error(msg, args);
				return default(T);
			}
		}

		#endregion
	}
}
