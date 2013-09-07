using System;

namespace Lens.SyntaxTree.Compiler
{
	/// <summary>
	/// A predefined subsystem for easier safe mode tweaking.
	/// </summary>
	[Flags]
	public enum SafeModeSubsystem
	{
		None			= 0,
		Network			= 0x001,
		IO		= 0x002,
		Reflection		= 0x004,
		Threading		= 0x010,
		Environment		= 0x020
	}
}
