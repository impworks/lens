namespace Lens.Test.TestClassHierarchy
{
	class ImplicitCastable
	{
		public static implicit operator int(ImplicitCastable o)
		{
			return 0;
		}
	}
}
