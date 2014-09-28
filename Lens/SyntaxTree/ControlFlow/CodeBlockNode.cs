using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;
using Lens.Compiler.Entities;
using Lens.Resolver;
using Lens.SyntaxTree.Declarations.Locals;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.ControlFlow
{
	/// <summary>
	/// A set of consecutive code statements.
	/// </summary>
	internal class CodeBlockNode : NodeBase, IEnumerable<NodeBase>
	{
		#region Constructor

		public CodeBlockNode(ScopeKind scopeKind = ScopeKind.Unclosured)
		{
			Statements = new List<NodeBase>();	
			Scope = new Scope(scopeKind);
		}

		#endregion

		#region Fields

		/// <summary>
		/// The scope frame corresponding to current code block.
		/// </summary>
		public Scope Scope { get; private set; }

		/// <summary>
		/// The statements to execute.
		/// </summary>
		public List<NodeBase> Statements { get; set; }

		#endregion

		#region Resolve

		protected override Type resolve(Context ctx, bool mustReturn)
		{
			var last = Statements.LastOrDefault();
			if (last is VarNode || last is LetNode)
				error(last, CompilerMessages.CodeBlockLastVar);

			ctx.EnterScope(Scope);

			var result = typeof(UnitType);
			foreach(var curr in Statements)
				result = curr.Resolve(ctx);

			ctx.ExitScope();

			return result;
		}

		#endregion

		#region Transform

		public override void Transform(Context ctx, bool mustReturn)
		{
			ctx.EnterScope(Scope);

			base.Transform(ctx, mustReturn);

			ctx.ExitScope();
		}

		protected override IEnumerable<NodeChild> getChildren()
		{
			return Statements.Select((stmt, i) => new NodeChild(stmt, x => Statements[i] = x));
		}

		#endregion

		#region Process closures

		public override void ProcessClosures(Context ctx)
		{
			ctx.EnterScope(Scope);
			base.ProcessClosures(ctx);
			ctx.ExitScope().FinalizeSelf(ctx);
		}

		#endregion

		#region Emit

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			ctx.EnterScope(Scope);

			if(Scope.ClosureType != null)
				emitClosureSetup(ctx);

			emitStatements(ctx, mustReturn);

			ctx.ExitScope();
		}

		private void emitClosureSetup(Context ctx)
		{
			var gen = ctx.CurrentMethod.Generator;

			var type = Scope.ClosureType;
			var loc = Scope.ClosureVariable;

			// create closure instance
			var closureCtor = type.ResolveConstructor(new Type[0]).ConstructorBuilder;
			gen.EmitCreateObject(closureCtor);
			gen.EmitSaveLocal(loc);

			// affix to parent
			if (Scope.ClosureReferencesOuter)
			{
				gen.EmitLoadLocal(loc);

				if (Scope.Kind == ScopeKind.Loop)
					gen.EmitLoadLocal(Scope.OuterScope.ClosureVariable);
				else if (Scope.Kind == ScopeKind.LambdaRoot)
					gen.EmitLoadArgument(0);
				else
					throw new InvalidOperationException("Incorrect scope parent!");

				gen.EmitSaveField(type.ResolveField(EntityNames.ParentScopeFieldName).FieldBuilder);
			}

			// save arguments into closure
			foreach (var curr in Scope.Locals.Values)
			{
				if (!curr.IsClosured || curr.ArgumentId == null)
					continue;

				gen.EmitLoadLocal(loc);
				gen.EmitLoadArgument(curr.ArgumentId.Value);
				gen.EmitSaveField(type.ResolveField(curr.ClosureFieldName).FieldBuilder);
			}
		}

		private void emitStatements(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentMethod.Generator;

			for (var idx = 0; idx < Statements.Count; idx++)
			{
				var subReturn = mustReturn && idx == Statements.Count - 1;
				var curr = Statements[idx];

				var retType = curr.Resolve(ctx, subReturn);

				if (!subReturn && curr.IsConstant)
					continue;

				curr.Emit(ctx, subReturn);

				if (!subReturn && !retType.IsVoid())
				{
					// nested code block nodes take care of themselves
					if(!(curr is CodeBlockNode))
						gen.EmitPop();
				}
			}
		}

		#endregion

		#region Debug

		protected bool Equals(CodeBlockNode other)
		{
			return Statements.SequenceEqual(other.Statements);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((CodeBlockNode)obj);
		}

		public override int GetHashCode()
		{
			return (Statements != null ? Statements.GetHashCode() : 0);
		}

		#endregion

		#region IEnumerable<NodeBase> implementation

		public IEnumerator<NodeBase> GetEnumerator()
		{
			return Statements.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public void Add(NodeBase node)
		{
			Statements.Add(node);
		}

		public void Insert(NodeBase node)
		{
			Statements.Insert(0, node);
		}

		#endregion

		#region Additional methods

		/// <summary>
		/// Loads nodes from other block.
		/// </summary>
		public void LoadFrom(CodeBlockNode other)
		{
			Statements = other.Statements;
		}

		#endregion
	}
}
