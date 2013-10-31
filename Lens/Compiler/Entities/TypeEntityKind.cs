namespace Lens.Compiler.Entities
{
	/// <summary>
	/// A kind of type entity defined in the type manager.
	/// </summary>
	internal enum TypeEntityKind
	{
		Type,
		TypeLabel,
		Record,
		Closure,
		Imported,
		Main
	}
}
