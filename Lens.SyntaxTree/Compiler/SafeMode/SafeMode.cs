namespace Lens.SyntaxTree.Compiler.SafeMode
{
	public enum SafeMode
	{
		/// <summary>
		/// All types and namespaces are allowed.
		/// </summary>
		Disabled,

		/// <summary>
		/// All types and namespaces are allowed except for explicitly specified ones.
		/// </summary>
		Blacklist,

		/// <summary>
		/// Only the explicitly specified types and namespaces are allowed.
		/// </summary>
		Whitelist
	}
}
