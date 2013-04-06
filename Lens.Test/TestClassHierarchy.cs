namespace Lens.Test
{
	internal class ParentClass { }

	internal struct Struct { }

	internal interface IInterface { }

	interface IDerivedInterface : IInterface { }

	internal class InterfaceImplementer : IInterface { }

	internal class InterfaceDerivedImplementer : InterfaceImplementer { }

	internal class ImplicitCastable
	{
		public static implicit operator int(ImplicitCastable o)
		{
			return 0;
		}
	}

	internal class DerivedInterfaceImplementer : IDerivedInterface { }

	internal class DerivedClass : ParentClass { }

	internal class SubDerivedClass : DerivedClass { }

	internal class DerivedClass2 : ParentClass { }
}
