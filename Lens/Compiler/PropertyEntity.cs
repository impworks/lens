using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Lens.Compiler
{
	internal class PropertyEntity : TypeContentsBase
	{
		public PropertyEntity(bool setter)
		{
			HasSetter = setter;
		}

		#region Fields

		/// <summary>
		/// Checks if the current property has a setter.
		/// </summary>
		public bool HasSetter;

		/// <summary>
		/// Flag indicating the property belongs to the type, not its instances.
		/// </summary>
		public bool IsStatic;

		/// <summary>
		/// Flag indicating the property fulfills an interface's contract.
		/// </summary>
		public bool IsVirtual;

		/// <summary>
		/// A string representation of the property's type.
		/// </summary>
		public TypeSignature TypeSignature;

		/// <summary>
		/// Type of the values that can be saved in the property.
		/// </summary>
		public Type Type;

		/// <summary>
		/// Assembly-level property builder.
		/// </summary>
		public PropertyBuilder PropertyBuilder { get; private set; }

		public MethodEntity Getter { get; private set; }
		public MethodEntity Setter { get; private set; }

		#endregion

		/// <summary>
		/// Creates getter and setter entities when the property fields have been instantiated.
		/// </summary>
		public void CreateBackingMethods()
		{
			Getter = ContainerType.CreateMethod("get_" + Name, Type, null, IsStatic, IsVirtual);
			Getter.IsSpecial = true;

			if (HasSetter)
			{
				Setter = ContainerType.CreateMethod("set_" + Name, typeof(void), new[] { Type }, IsStatic, IsVirtual);
				Setter.IsSpecial = true;
			}
		}

		public override void PrepareSelf()
		{
			if (PropertyBuilder != null)
				return;

			if (Type == null)
				Type = ContainerType.Context.ResolveType(TypeSignature);

			PropertyBuilder = ContainerType.TypeBuilder.DefineProperty(Name, PropertyAttributes.None, Type, null);
			Getter.PrepareSelf();

			if (HasSetter)
				Setter.PrepareSelf();
		}
	}
}
