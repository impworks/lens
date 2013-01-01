using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Lens.SyntaxTree.Compiler
{
	/// <summary>
	/// The main context class that stores information about currently compiled Assembly.
	/// </summary>
	public class Context
	{
		public Context()
		{
			var an = new AssemblyName(getAssemblyName());
			CurrentAssembly = AppDomain.CurrentDomain.DefineDynamicAssembly(an, AssemblyBuilderAccess.RunAndSave);
			CurrentModule = CurrentAssembly.DefineDynamicModule(an.Name, an.Name + ".exe");
			MainType = CurrentModule.DefineType("_RootType", TypeAttributes.Public | TypeAttributes.Class);
			EntryPoint = MainType.DefineMethod("_EntryPoint", MethodAttributes.Public | MethodAttributes.Static, typeof(object), new Type[0]);

			CurrentType = MainType;
			CurrentMethod = EntryPoint;
		}

		#region Properties

		private static int AssemblyCounter;

		/// <summary>
		/// The assembly that's being currently built.
		/// </summary>
		public AssemblyBuilder CurrentAssembly { get; private set; }

		/// <summary>
		/// The main module of the current assembly.
		/// </summary>
		public ModuleBuilder CurrentModule { get; private set; }

		/// <summary>
		/// The main type of the module.
		/// </summary>
		public TypeBuilder MainType { get; private set; }

		/// <summary>
		/// The main method of the module.
		/// </summary>
		public MethodBuilder EntryPoint { get; private set; }

		/// <summary>
		/// The currently defined type.
		/// </summary>
		public TypeBuilder CurrentType { get; set; }

		/// <summary>
		/// The currently defined method.
		/// </summary>
		public MethodBuilder CurrentMethod { get; set; }

		#endregion

		#region Methods



		#endregion

		#region Helpers

		private string getAssemblyName()
		{
			lock(typeof(Context))
				AssemblyCounter++;
			return "_CompiledAssembly" + AssemblyCounter;
		}

		#endregion
	}
}
