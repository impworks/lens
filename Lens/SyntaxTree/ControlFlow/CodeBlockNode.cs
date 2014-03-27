using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;
using Lens.Compiler.Entities;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.ControlFlow
{
	/// <summary>
	/// A set of consecutive code statements.
	/// </summary>
	internal class CodeBlockNode : NodeBase, IEnumerable<NodeBase>
	{
		public CodeBlockNode(ScopeKind scopeKind = ScopeKind.Unclosured)
		{
			Statements = new List<NodeBase>();	
			Scope = new Scope(scopeKind);
		}

		/// <summary>
		/// The scope frame corresponding to current code block.
		/// </summary>
		public Scope Scope { get; private set; }

		/// <summary>
		/// The statements to execute.
		/// </summary>
		public List<NodeBase> Statements { get; set; }

		protected override Type resolve(Context ctx, bool mustReturn)
		{
			if (!Statements.Any())
				return typeof (Unit);

			var last = Statements.Last();
			if (last is VarNode || last is LetNode)
				error(last, CompilerMessages.CodeBlockLastVar);

			ctx.EnterScope(Scope);
			var result = Statements[Statements.Count - 1].Resolve(ctx);
			ctx.ExitScope();
			return result;
		}

		public override IEnumerable<NodeChild> GetChildren()
		{
			return Statements.Select((stmt, i) => new NodeChild(stmt, x => Statements[i] = x));
		}

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

				if (!subReturn && retType.IsNotVoid())
					gen.EmitPop();
			}
		}

		public override void ProcessClosures(Context ctx)
		{
			ctx.EnterScope(Scope);
			base.ProcessClosures(ctx);
			ctx.ExitScope().FinalizeSelf(ctx);
		}

		#region Equality members

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

		#region Helpers

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
