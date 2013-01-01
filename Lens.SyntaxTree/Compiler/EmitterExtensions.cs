using System;
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
				case 0:  gen.Emit(OpCodes.Ldc_I4_0); break;
				case 1:  gen.Emit(OpCodes.Ldc_I4_1); break;
				case 2:  gen.Emit(OpCodes.Ldc_I4_2); break;
				case 3:  gen.Emit(OpCodes.Ldc_I4_3); break;
				case 4:  gen.Emit(OpCodes.Ldc_I4_4); break;
				case 5:  gen.Emit(OpCodes.Ldc_I4_5); break;
				case 6:  gen.Emit(OpCodes.Ldc_I4_6); break;
				case 7:  gen.Emit(OpCodes.Ldc_I4_7); break;
				case 8:  gen.Emit(OpCodes.Ldc_I4_8); break;
				case -1: gen.Emit(OpCodes.Ldc_I4_M1); break;
				default: gen.Emit(OpCodes.Ldc_I4, value); break;
			}
		}

		/// <summary>
		/// Pushes an int64 value onto the stack.
		/// </summary>
		public static void EmitConstant(this ILGenerator gen, long value)
		{
			gen.Emit(OpCodes.Ldc_R8, value);
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

		#endregion

		#region Saving and loading



		#endregion

		#region Methods and constructors

		/// <summary>
		/// Return from the method.
		/// </summary>
		public static void EmitReturn(this ILGenerator gen)
		{
			gen.Emit(OpCodes.Ret);
		}

		/// <summary>
		/// Pop an unneeded value from the top of the stack.
		/// </summary>
		public static void EmitPop(this ILGenerator gen)
		{
			gen.Emit(OpCodes.Pop);
		}

		#endregion

		#region Conversion and boxing

		/// <summary>
		/// Cast the top value of the stack to int.
		/// </summary>
		public static void EmitConvertToInt(this ILGenerator gen)
		{
			gen.Emit(OpCodes.Conv_I4);
		}

		/// <summary>
		/// Cast the top value of the stack to long.
		/// </summary>
		public static void EmitConvertToLong(this ILGenerator gen)
		{
			gen.Emit(OpCodes.Conv_I8);
		}

		/// <summary>
		/// Cast the top value of the stack to float.
		/// </summary>
		public static void EmitConvertToFloat(this ILGenerator gen)
		{
			gen.Emit(OpCodes.Conv_R4);
		}

		/// <summary>
		/// Cast the top value of the stack to double.
		/// </summary>
		public static void EmitConvertToDouble(this ILGenerator gen)
		{
			gen.Emit(OpCodes.Conv_R8);
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

		#endregion
	}
}
