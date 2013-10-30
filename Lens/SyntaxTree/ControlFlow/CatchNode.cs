using System;
using System.Collections.Generic;
using Lens.Compiler;
using Lens.Translations;

namespace Lens.SyntaxTree.ControlFlow
{
	/// <summary>
	/// The safe block of code.
	/// </summary>
	internal class CatchNode : NodeBase, IStartLocationTrackingEntity
	{
		public CatchNode()
		{
			Code = new CodeBlockNode();
		}

		/// <summary>
		/// The type of the exception this catch block handles.
		/// Null means any exception.
		/// </summary>
		public TypeSignature ExceptionType { get; set; }

		/// <summary>
		/// A variable to assign the exception to.
		/// </summary>
		public string ExceptionVariable { get; set; }

		/// <summary>
		/// The code block.
		/// </summary>
		public CodeBlockNode Code { get; set; }

		private LocalName m_ExceptionVariable;

		public override LexemLocation EndLocation
		{
			get { return Code.EndLocation; }
			set { LocationSetError(); }
		}

		public override void Analyze(Context ctx)
		{
			var backup = ctx.CurrentCatchBlock;
			ctx.CurrentCatchBlock = this;

			base.Analyze(ctx);

			ctx.CurrentCatchBlock = backup;
		}

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			yield return Code;
		}

		public override void ProcessClosures(Context ctx)
		{
			base.ProcessClosures(ctx);

			var type = ExceptionType != null ? ctx.ResolveType(ExceptionType) : typeof(Exception);
			if (type != typeof(Exception) && !type.IsSubclassOf(typeof(Exception)))
				Error(CompilerMessages.CatchTypeNotException, type);

			if(!string.IsNullOrEmpty(ExceptionVariable))
				m_ExceptionVariable = ctx.CurrentScope.DeclareName(ExceptionVariable, type, false);
		}

		protected override void compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;

			var backup = ctx.CurrentCatchBlock;
			ctx.CurrentCatchBlock = this;

			var type = ExceptionType != null ? ctx.ResolveType(ExceptionType) : typeof(Exception);
			gen.BeginCatchBlock(type);

			if (m_ExceptionVariable == null)
				gen.EmitPop();
			else
				gen.EmitSaveLocal(m_ExceptionVariable);

			Code.Compile(ctx, false);

			gen.EmitLeave(ctx.CurrentTryBlock.EndLabel);

			ctx.CurrentCatchBlock = backup;
		}

		#region Equality members

		protected bool Equals(CatchNode other)
		{
			return Equals(ExceptionType, other.ExceptionType)
				&& string.Equals(ExceptionVariable, other.ExceptionVariable)
				&& Equals(Code, other.Code);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((CatchNode)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				int hashCode = (ExceptionType != null ? ExceptionType.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (ExceptionVariable != null ? ExceptionVariable.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (Code != null ? Code.GetHashCode() : 0);
				return hashCode;
			}
		}

		#endregion
	}
}
