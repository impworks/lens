using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Lens.SyntaxTree.Compiler
{
	public static class EmitterExtensions
	{
		#region Constants
		
		/// <summary>
		/// Pushes an integer value onto the top of the stack.
		/// </summary>
		public static void EmitConstant(this ILGenerator gen, int value)
		{
			switch (value)
			{
				case 0: gen.Emit(OpCodes.Ldc_I4_0); return;
				case 1: gen.Emit(OpCodes.Ldc_I4_1); return;
				case 2: gen.Emit(OpCodes.Ldc_I4_2); return;
				case 3: gen.Emit(OpCodes.Ldc_I4_3); return;
				case 4: gen.Emit(OpCodes.Ldc_I4_4); return;
				case 5: gen.Emit(OpCodes.Ldc_I4_5); return;
				case 6: gen.Emit(OpCodes.Ldc_I4_6); return;
				case 7: gen.Emit(OpCodes.Ldc_I4_7); return;
				case 8: gen.Emit(OpCodes.Ldc_I4_8); return;
				case -1: gen.Emit(OpCodes.Ldc_I4_M1); return;
			}

			if(value > -127 && value < 127)
				gen.Emit(OpCodes.Ldc_I4_S, (sbyte)value);
			else
				gen.Emit(OpCodes.Ldc_I4, value);
		}

		/// <summary>
		/// Pushes an int64 value onto the stack.
		/// </summary>
		public static void EmitConstant(this ILGenerator gen, long value)
		{
			gen.Emit(OpCodes.Ldc_I8, value);
		}

		/// <summary>
		/// Pushes a float32 value onto the stack.
		/// </summary>
		public static void EmitConstant(this ILGenerator gen, float value)
		{
			gen.Emit(OpCodes.Ldc_R4, value);
		}

		/// <summary>
		/// Pushes a float64 value onto the stack.
		/// </summary>
		public static void EmitConstant(this ILGenerator gen, double value)
		{
			gen.Emit(OpCodes.Ldc_R8, value);
		}

		/// <summary>
		/// Pushes a boolean value onto the stack (actually an integer).
		/// </summary>
		public static void EmitConstant(this ILGenerator gen, bool value)
		{
			gen.Emit(value ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
		}

		/// <summary>
		/// Pushes a boolean value onto the stack (actually an integer).
		/// </summary>
		public static void EmitConstant(this ILGenerator gen, string value)
		{
			gen.Emit(OpCodes.Ldstr, value);
		}
		
		/// <summary>
		/// Pushes a type runtime handle onto the stack.
		/// </summary>
		public static void EmitConstant(this ILGenerator gen, Type type)
		{
			gen.Emit(OpCodes.Ldtoken, type);
		}

		/// <summary>
		/// Pushes a null value onto the stack.
		/// </summary>
		public static void EmitNull(this ILGenerator gen)
		{
			gen.Emit(OpCodes.Ldnull);
		}

		#endregion

		#region Comparison and branching

		/// <summary>
		/// Pops 2 values from the stack and pushes 1 if they are equal, otherwise 0.
		/// </summary>
		public static void EmitCompareEqual(this ILGenerator gen)
		{
			gen.Emit(OpCodes.Ceq);
		}

		/// <summary>
		/// Pops 2 values from the stack and pushes 1 if the first is smaller, otherwise 0.
		/// </summary>
		public static void EmitCompareLess(this ILGenerator gen, bool signed = true)
		{
			gen.Emit(signed ? OpCodes.Clt : OpCodes.Clt_Un);
		}

		/// <summary>
		/// Pops 2 values from the stack and pushes 1 if the first is bigger, otherwise 0.
		/// </summary>
		public static void EmitCompareGreater(this ILGenerator gen, bool signed = true)
		{
			gen.Emit(signed ? OpCodes.Cgt : OpCodes.Cgt_Un);
		}

		/// <summary>
		/// Jumps to a location.
		/// </summary>
		public static void EmitJump(this ILGenerator gen, Label label)
		{
			gen.Emit(OpCodes.Br, label);
		}

		/// <summary>
		/// Jumps to a location if the top of the stack is true.
		/// </summary>
		public static void EmitBranchTrue(this ILGenerator gen, Label label)
		{
			gen.Emit(OpCodes.Brtrue, label);
		}

		/// <summary>
		/// Jumps to a location if the top of the stack is false.
		/// </summary>
		public static void EmitBranchFalse(this ILGenerator gen, Label label)
		{
			gen.Emit(OpCodes.Brfalse, label);
		}

		#endregion

		#region Operators

		/// <summary>
		/// Sum two numbers on top of the stack.
		/// </summary>
		public static void EmitAdd(this ILGenerator gen)
		{
			gen.Emit(OpCodes.Add);
		}

		/// <summary>
		/// Subtract two numbers on top of the stack.
		/// </summary>
		public static void EmitSubtract(this ILGenerator gen)
		{
			gen.Emit(OpCodes.Sub);
		}

		/// <summary>
		/// Multiply two numbers on top of the stack.
		/// </summary>
		public static void EmitMultiply(this ILGenerator gen)
		{
			gen.Emit(OpCodes.Mul);
		}

		/// <summary>
		/// Divide two numbers on top of the stack.
		/// </summary>
		public static void EmitDivide(this ILGenerator gen)
		{
			gen.Emit(OpCodes.Div);
		}

		/// <summary>
		/// Divide two numbers on top of the stack and push the remainder.
		/// </summary>
		public static void EmitRemainder(this ILGenerator gen)
		{
			gen.Emit(OpCodes.Rem);
		}

		/// <summary>
		/// Perform a logical AND operation on the two top values on stack.
		/// </summary>
		public static void EmitAnd(this ILGenerator gen)
		{
			gen.Emit(OpCodes.And);
		}

		/// <summary>
		/// Perform a logical OR operation on the two top values on stack.
		/// </summary>
		public static void EmitOr(this ILGenerator gen)
		{
			gen.Emit(OpCodes.Or);
		}

		/// <summary>
		/// Perform a logical XOR operation on the two top values on stack.
		/// </summary>
		public static void EmitXor(this ILGenerator gen)
		{
			gen.Emit(OpCodes.Xor);
		}

		/// <summary>
		/// Shift the value X bits left.
		/// </summary>
		public static void EmitShiftLeft(this ILGenerator gen)
		{
			gen.Emit(OpCodes.Shl);
		}

		/// <summary>
		/// Shift the value X bits right.
		/// </summary>
		public static void EmitShiftRight(this ILGenerator gen)
		{
			gen.Emit(OpCodes.Shr);
		}

		/// <summary>
		/// Negates the value on the stack.
		/// </summary>
		public static void EmitNegate(this ILGenerator gen)
		{
			gen.Emit(OpCodes.Neg);
		}

		#endregion

		#region Saving and loading

		/// <summary>
		/// Loads the value of the field onto the stack.
		/// </summary>
		public static void EmitLoadField(this ILGenerator gen, FieldInfo field)
		{
			gen.Emit(OpCodes.Ldfld, field);
		}

		/// <summary>
		/// Loads the address of the field onto the stack.
		/// </summary>
		public static void EmitLoadFieldAddr(this ILGenerator gen, FieldInfo field)
		{
			gen.Emit(OpCodes.Ldflda, field);
		}

		/// <summary>
		/// Saves the value from the stack to the field.
		/// </summary>
		public static void EmitSaveField(this ILGenerator gen, FieldInfo field)
		{
			gen.Emit(OpCodes.Stfld, field);
		}

		/// <summary>
		/// Loads the value of an argument onto the stack.
		/// </summary>
		public static void EmitLoadArgument(this ILGenerator gen, int argId)
		{
			switch (argId)
			{
				case 0: gen.Emit(OpCodes.Ldarg_0); break;
				case 1: gen.Emit(OpCodes.Ldarg_1); break;
				case 2: gen.Emit(OpCodes.Ldarg_2); break;
				case 3: gen.Emit(OpCodes.Ldarg_3); break;
				default: gen.Emit(OpCodes.Ldarg, (short)argId); break;
			}
		}

		/// <summary>
		/// Loads the address of the field onto the stack.
		/// </summary>
		public static void EmitLoadArgumentAddress(this ILGenerator gen, int argId)
		{
			if(argId < 255)
				gen.Emit(OpCodes.Ldarga_S, (byte)argId);
			else
				gen.Emit(OpCodes.Ldarga, (short)argId);
		}

		/// <summary>
		/// Saves the value from the stack to the argument store.
		/// </summary>
		public static void EmitSaveArgument(this ILGenerator gen, int argId)
		{
			if(argId < 255)
				gen.Emit(OpCodes.Starg_S, (byte)argId);
			else
				gen.Emit(OpCodes.Starg, (short)argId);
		}

		/// <summary>
		/// Loads the value of a local variable onto the stack.
		/// </summary>
		public static void EmitLoadLocal(this ILGenerator gen, int varId)
		{
			switch (varId)
			{
				case 0: gen.Emit(OpCodes.Ldloc_0); break;
				case 1: gen.Emit(OpCodes.Ldloc_1); break;
				case 2: gen.Emit(OpCodes.Ldloc_2); break;
				case 3: gen.Emit(OpCodes.Ldloc_3); break;
				default: gen.Emit(OpCodes.Ldloc, (short)varId); break;
			}
		}

		public static void EmitLoadLocalAddress(this ILGenerator gen, int varId)
		{
			if (varId < 255)
				gen.Emit(OpCodes.Ldloca_S, (byte)varId);
			else
				gen.Emit(OpCodes.Ldloca, (short)varId);
		}

		/// <summary>
		/// Saves the value from the stack to a local variable.
		/// </summary>
		public static void EmitSaveLocal(this ILGenerator gen, int varId)
		{
			gen.Emit(OpCodes.Stloc, (short)varId);
		}

		/// <summary>
		/// Loads an array item of the specified type onto the stack.
		/// </summary>
		public static void EmitLoadIndex(this ILGenerator gen, Type itemType)
		{
			if(itemType == typeof(sbyte))
				gen.Emit(OpCodes.Ldelem_I1);
			else if (itemType == typeof(short))
				gen.Emit(OpCodes.Ldelem_I2);
			else if (itemType == typeof(int))
				gen.Emit(OpCodes.Ldelem_I4);
			else if (itemType == typeof(long) || itemType == typeof(ulong))
				gen.Emit(OpCodes.Ldelem_I8);
			else if (itemType == typeof(float))
				gen.Emit(OpCodes.Ldelem_R4);
			else if (itemType == typeof(double))
				gen.Emit(OpCodes.Ldelem_R8);
			else if (itemType == typeof(byte))
				gen.Emit(OpCodes.Ldelem_U1);
			else if (itemType == typeof(ushort))
				gen.Emit(OpCodes.Ldelem_U2);
			else if (itemType == typeof(uint))
				gen.Emit(OpCodes.Ldelem_U4);
			else if(itemType.IsClass || itemType.IsInterface)
				gen.Emit(OpCodes.Ldelem_Ref);
			else
				throw new InvalidOperationException("Cannot use LoadIndex on value types! Use LoadIndexAddress instead.");
		}

		/// <summary>
		/// Loads an array item address onto the stack.
		/// </summary>
		public static void EmitLoadIndexAddress(this ILGenerator gen, Type itemType)
		{
			if(itemType.IsClass || itemType.IsInterface)
				throw new InvalidOperationException("Cannot use LoadIndexAddress on ref types! Use LoadIndex instead.");

			gen.Emit(OpCodes.Ldelema, itemType);
		}

		/// <summary>
		/// Saves an item at the given array location.
		/// </summary>
		public static void EmitSaveIndex(this ILGenerator gen, Type itemType)
		{
			if (itemType == typeof (byte) || itemType == typeof (sbyte))
				gen.Emit(OpCodes.Stelem_I1);
			else if (itemType == typeof(short) || itemType == typeof(ushort))
				gen.Emit(OpCodes.Stelem_I2);
			else if (itemType == typeof(int) || itemType == typeof(uint))
				gen.Emit(OpCodes.Stelem_I4);
			else if (itemType == typeof(long) || itemType == typeof(ulong))
				gen.Emit(OpCodes.Stelem_I8);
			else if (itemType == typeof(float))
				gen.Emit(OpCodes.Stelem_R4);
			else if (itemType == typeof(double))
				gen.Emit(OpCodes.Stelem_R8);
			else if (itemType.IsClass || itemType.IsInterface)
				gen.Emit(OpCodes.Stelem_Ref);
			else
				throw new InvalidOperationException("SaveIndex cannot be used on valuetype objects!");
		}

		/// <summary>
		/// Loads the object from a given location in memory.
		/// </summary>
		public static void EmitLoadObject(this ILGenerator gen, Type itemType)
		{
			if (!itemType.IsValueType)
				throw new InvalidOperationException("LoadObject can only be used on valuetype objects!");

			gen.Emit(OpCodes.Ldobj, itemType);
		}

		/// <summary>
		/// Saves an object at the given location in memory.
		/// </summary>
		public static void EmitSaveObject(this ILGenerator gen, Type itemType)
		{
			if(!itemType.IsValueType)
				throw new InvalidOperationException("SaveObject can only be used on valuetype objects!");

			gen.Emit(OpCodes.Stobj, itemType);
		}

		#endregion

		#region Methods and constructors

		/// <summary>
		/// Creates a new object using the given constructor.
		/// </summary>
		public static void EmitCreateObject(this ILGenerator gen, ConstructorInfo ctr)
		{
			gen.Emit(OpCodes.Newobj, ctr);
		}

		/// <summary>
		/// Initializes a structure fields to nulls of appropriate types.
		/// </summary>
		public static void EmitInitObject(this ILGenerator gen, Type type)
		{
			gen.Emit(OpCodes.Initobj, type);
		}

		/// <summary>
		/// Creates a new array of the given type.
		/// Array size is to be pushed into the stack beforehand. 
		/// </summary>
		public static void EmitCreateArray(this ILGenerator gen, Type type)
		{
			gen.Emit(OpCodes.Newarr, type);
		}

		/// <summary>
		/// Calculates the size of the array.
		/// </summary>
		public static void EmitGetArrayLength(this ILGenerator gen)
		{
			gen.Emit(OpCodes.Ldlen);
		}

		/// <summary>
		/// Call a method.
		/// </summary>
		public static void EmitCall(this ILGenerator gen, MethodInfo method, bool isVirtual = false)
		{
			gen.Emit(isVirtual ? OpCodes.Callvirt : OpCodes.Call, method);
		}

		/// <summary>
		/// Returns from the method.
		/// </summary>
		public static void EmitReturn(this ILGenerator gen)
		{
			gen.Emit(OpCodes.Ret);
		}

		/// <summary>
		/// Pops an unneeded value from the top of the stack.
		/// </summary>
		public static void EmitPop(this ILGenerator gen)
		{
			gen.Emit(OpCodes.Pop);
		}

		/// <summary>
		/// Pushes an unmanaged method pointer to the stack.
		/// </summary>
		public static void EmitLoadFunctionPointer(this ILGenerator gen, MethodInfo method)
		{
			gen.Emit(OpCodes.Ldftn, method);
		}

		#endregion

		#region Conversion and boxing

		/// <summary>
		/// Cast the top value of the stack to a given primitive type.
		/// </summary>
		public static void EmitConvert(this ILGenerator gen, Type targetType)
		{
			if(targetType == typeof(byte))
				gen.Emit(OpCodes.Conv_U1);
			else if (targetType == typeof(short))
				gen.Emit(OpCodes.Conv_I2);
			else if (targetType == typeof(int))
				gen.Emit(OpCodes.Conv_I4);
			else if (targetType == typeof(long))
				gen.Emit(OpCodes.Conv_I8);
			else if (targetType == typeof(float))
				gen.Emit(OpCodes.Conv_R4);
			else if (targetType == typeof(short))
				gen.Emit(OpCodes.Conv_R8);
			else if (targetType == typeof(sbyte))
				gen.Emit(OpCodes.Conv_I1);
			else if (targetType == typeof(ushort))
				gen.Emit(OpCodes.Conv_U2);
			else if (targetType == typeof(uint))
				gen.Emit(OpCodes.Conv_U4);
			else if (targetType == typeof(ulong))
				gen.Emit(OpCodes.Conv_U8);
			else
				throw new InvalidOperationException("Incorrect primitive numeric type!");
		}

		/// <summary>
		/// Box the current valuetype on the stack.
		/// </summary>
		public static void EmitBox(this ILGenerator gen, Type type)
		{
			gen.Emit(OpCodes.Box, type);
		}

		/// <summary>
		/// Unbox the current object on the stack to a valuetype.
		/// </summary>
		public static void EmitUnbox(this ILGenerator gen, Type type)
		{
			gen.Emit(OpCodes.Box, type);
		}

		/// <summary>
		/// Casts an object to the desired type, throwing an InvalidCastException if the cast fails.
		/// </summary>
		public static void EmitCast(this ILGenerator gen, Type type, bool throwOnFail = true)
		{
			if(throwOnFail)
				gen.Emit(OpCodes.Castclass, type);
			else
				gen.Emit(OpCodes.Isinst, type);
		}
	
		#endregion

		#region Exception handling

		/// <summary>
		/// Throws the exception object that's currently on the stack.
		/// </summary>
		public static void EmitThrow(this ILGenerator gen)
		{
			gen.Emit(OpCodes.Throw);
		}

		/// <summary>
		/// Throws the exception object that's currently on the stack.
		/// </summary>
		public static void EmitRethrow(this ILGenerator gen)
		{
			gen.Emit(OpCodes.Rethrow);
		}

		/// <summary>
		/// Leaves a protected section.
		/// </summary>
		public static void EmitLeave(this ILGenerator gen, Label label)
		{
			gen.Emit(OpCodes.Leave, label);
		}

		#endregion
	}
}
