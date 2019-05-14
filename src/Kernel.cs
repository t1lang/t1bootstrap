using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

class Kernel {

	internal static XType[] T_Object = new XType[] { XType.OBJECT };
	internal static XType[] T_Bool = new XType[] { XType.BOOL };
	internal static XType[] T_Int = new XType[] { XType.INT };
	internal static XType[] T_U8 = new XType[] { XType.U8 };
	internal static XType[] T_U16 = new XType[] { XType.U16 };
	internal static XType[] T_U32 = new XType[] { XType.U32 };
	internal static XType[] T_U64 = new XType[] { XType.U64 };
	internal static XType[] T_I8 = new XType[] { XType.I8 };
	internal static XType[] T_I16 = new XType[] { XType.I16 };
	internal static XType[] T_I32 = new XType[] { XType.I32 };
	internal static XType[] T_I64 = new XType[] { XType.I64 };
	internal static XType[] T_String = new XType[] { XType.ARRAY_U8 };
	internal static XType[] T_Type = new XType[] { XType.XTYPE };

	internal static XType[] T_ObjectObject = new XType[] {
		XType.OBJECT, XType.OBJECT
	};
	internal static XType[] T_BoolBool = new XType[] {
		XType.BOOL, XType.BOOL
	};
	internal static XType[] T_IntInt = new XType[] {
		XType.INT, XType.INT
	};
	internal static XType[] T_U8U8 = new XType[] {
		XType.U8, XType.U8
	};
	internal static XType[] T_U16U16 = new XType[] {
		XType.U16, XType.U16
	};
	internal static XType[] T_U32U32 = new XType[] {
		XType.U32, XType.U32
	};
	internal static XType[] T_U64U64 = new XType[] {
		XType.U64, XType.U64
	};
	internal static XType[] T_I8I8 = new XType[] {
		XType.I8, XType.I8
	};
	internal static XType[] T_I16I16 = new XType[] {
		XType.I16, XType.I16
	};
	internal static XType[] T_I32I32 = new XType[] {
		XType.I32, XType.I32
	};
	internal static XType[] T_I64I64 = new XType[] {
		XType.I64, XType.I64
	};
	internal static XType[] T_StringString = new XType[] {
		XType.ARRAY_U8, XType.ARRAY_U8
	};

	internal static XType[] T_ArrayObject = new XType[] {
		XType.ARRAY_OBJECT
	};
	internal static XType[] T_ArrayObjectArrayObject = new XType[] {
		XType.ARRAY_OBJECT, XType.ARRAY_OBJECT
	};
	internal static XType[] T_StringBoolArrayObject = new XType[] {
		XType.ARRAY_U8, XType.BOOL, XType.ARRAY_OBJECT
	};
	internal static XType[] T_ObjectObjectObject = new XType[] {
		XType.OBJECT, XType.OBJECT, XType.OBJECT
	};
	internal static XType[] T_IntString = new XType[] {
		XType.INT, XType.ARRAY_U8
	};
	internal static XType[] T_I8U8 = new XType[] {
		XType.I8, XType.U8
	};
	internal static XType[] T_I16U16 = new XType[] {
		XType.I16, XType.U16
	};
	internal static XType[] T_I32U32 = new XType[] {
		XType.I32, XType.U32
	};
	internal static XType[] T_I64U64 = new XType[] {
		XType.I64, XType.U64
	};

	internal static void Add(string name, XType[] parameters, Function f)
	{
		Function.Register(name, parameters, f);
	}

	internal static void AddExp(
		string name, XType[] parameters, Function f)
	{
		Add(name, parameters, f);
		Interpreter.AddExportQualified(name);
	}

	internal static void AddImmediate(string name,
		FunctionSpec.NativeRun code)
	{
		Function f = new FunctionSpec(name, code);
		Function.RegisterImmediate(name, f);
	}

	internal static void AddImmediateExp(string name,
		FunctionSpec.NativeRun code)
	{
		AddImmediate(name, code);
		Interpreter.AddExportQualified(name);
	}

	internal static void AddNative(string name, XType[] parameters,
		FunctionSpec.NativeRun code)
	{
		Function f = new FunctionSpec(name, code);
		Function.Register(name, parameters, f);
	}

	internal static void AddNativeExp(string name, XType[] parameters,
		FunctionSpec.NativeRun code)
	{
		AddNative(name, parameters, code);
		Interpreter.AddExportQualified(name);
	}

	internal static void Init()
	{
		InitCore();
		InitIO();

		AddImmediateExp("std::namespace", cpu => {
			cpu.Interpreter.NativeNamespace(cpu);
		});
		AddImmediateExp("std::import", cpu => {
			cpu.Interpreter.NativeImport(cpu);
		});
		AddImmediateExp("std:::", cpu => {
			cpu.Interpreter.StartFunction(cpu);
		});
		AddImmediateExp("std::{", cpu => {
			cpu.Interpreter.NativeSimpleLocals(cpu);
		});
		AddImmediateExp("std::->", cpu => {
			cpu.Interpreter.NativeTo(cpu);
		});

		AddImmediateExp("std::ret", cpu => {
			cpu.Interpreter.NativeRet(cpu);
		});
		AddImmediateExp("std::ahead", cpu => {
			cpu.Interpreter.NativeAhead(cpu);
		});
		AddImmediateExp("std::if", cpu => {
			cpu.Interpreter.NativeIf(cpu);
		});
		AddImmediateExp("std::ifnot", cpu => {
			cpu.Interpreter.NativeIfNot(cpu);
		});
		AddImmediateExp("std::then", cpu => {
			cpu.Interpreter.NativeThen(cpu);
		});
		AddImmediateExp("std::begin", cpu => {
			cpu.Interpreter.NativeBegin(cpu);
		});
		AddImmediateExp("std::again", cpu => {
			cpu.Interpreter.NativeAgain(cpu);
		});
		AddImmediateExp("std::else", cpu => {
			cpu.Interpreter.NativeElse(cpu);
		});
		AddImmediateExp("std::while", cpu => {
			cpu.Interpreter.NativeWhile(cpu);
		});
		AddImmediateExp("std::repeat", cpu => {
			cpu.Interpreter.NativeRepeat(cpu);
		});
		AddImmediateExp("std::'", cpu => {
			cpu.Interpreter.NativeQuote(cpu);
		});

		AddNative("std::next-token", XType.ZERO, cpu => {
			cpu.Interpreter.NativeNextToken(cpu);
		});
		AddNative("std::to-complete-name", T_String, cpu => {
			cpu.Interpreter.NativeToCompleteName(cpu);
		});
		AddNative("std::start-struct", T_String, cpu => {
			cpu.Interpreter.NativeStartStruct(cpu);
		});
		AddNative("std::end-struct", T_Bool, cpu => {
			cpu.Interpreter.NativeEndStruct(cpu);
		});
		AddNative("std::to-type-element", T_String, cpu => {
			cpu.Interpreter.NativeToTypeElement(cpu);
		});
		AddNative("std::check-type-elements", T_ArrayObject, cpu => {
			cpu.Interpreter.NativeCheckTypeElements(cpu);
		});
		AddNative("std::concat", T_ArrayObjectArrayObject, cpu => {
			cpu.Interpreter.NativeConcatArrayObject(cpu);
		});
		AddNative("std::add-struct-extend", T_Type, cpu => {
			cpu.Interpreter.NativeAddStructExtend(cpu);
		});
		AddNative("std::add-struct-element",
			T_StringBoolArrayObject, cpu => {
			cpu.Interpreter.NativeAddStructElement(cpu);
		});
		AddNativeExp("std::literal", T_Object, cpu => {
			cpu.Interpreter.NativeLiteral(cpu);
		});
		AddNative("std::run-interpreter", T_Object, cpu => {
			cpu.Interpreter.NativeRunInterpreter(cpu);
		});
		AddNative("std::define-typed-local",
			T_StringBoolArrayObject, cpu => {
			cpu.Interpreter.NativeAddTypedLocal(cpu);
		});

		// DEBUG
		AddNativeExp("std::DEBUG", XType.ZERO, cpu => {
			Console.Write("STACK:");
			for (int d = cpu.Depth - 1; d >= 0; d --) {
				XValue xv = cpu.Peek(d);
				Console.Write(" <{0}>{1}", xv.VType.Name, xv);
			}
			Console.WriteLine();
		});

		using (TextReader tr = new StreamReader(
			Assembly.GetExecutingAssembly()
			.GetManifestResourceStream("t1-kernel")))
		{
			Interpreter ip = new Interpreter(tr);
			ip.Run();
		}
	}

	static void InitCore()
	{
		AddExp("std::=", T_ObjectObject, new NativeObjectEquals());
		AddExp("std::<>", T_ObjectObject, new NativeObjectNotEquals());
		AddExp("std::dup", T_Object, new NativeObjectDup());
		AddExp("std::drop", T_Object, new NativeObjectDrop());
		AddExp("std::swap", T_ObjectObject, new NativeObjectSwap());
		AddExp("std::over", T_ObjectObject, new NativeObjectOver());
		AddExp("std::rot", T_ObjectObjectObject, new NativeObjectRot());
		AddExp("std::-rot", T_ObjectObjectObject,
			new NativeObjectNRot());

		AddExp("std::and", T_BoolBool, new NativeBoolAnd());
		AddExp("std::or", T_BoolBool, new NativeBoolOr());
		AddExp("std::xor", T_BoolBool, new NativeBoolXor());
		AddExp("std::eqv", T_BoolBool, new NativeBoolEqv());
		AddExp("std::not", T_Bool, new NativeBoolNot());

		AddExp("std::+", T_U8U8, new NativeU8Add());
		AddExp("std::-", T_U8U8, new NativeU8Sub());
		AddExp("std::*", T_U8U8, new NativeU8Mul());
		AddExp("std::/", T_U8U8, new NativeU8Div());
		AddExp("std::%", T_U8U8, new NativeU8Mod());
		AddExp("std::/%", T_U8U8, new NativeU8DivMod());
		AddExp("std::neg", T_U8, new NativeU8Neg());
		AddExp("std::++", T_U8, new NativeU8Inc());
		AddExp("std::--", T_U8, new NativeU8Dec());
		AddExp("std::and", T_U8U8, new NativeU8And());
		AddExp("std::or", T_U8U8, new NativeU8Or());
		AddExp("std::xor", T_U8U8, new NativeU8Xor());
		AddExp("std::eqv", T_U8U8, new NativeU8Eqv());
		AddExp("std::not", T_U8, new NativeU8Not());
		AddExp("std::<", T_U8U8, new NativeU8LT());
		AddExp("std::<=", T_U8U8, new NativeU8LEQ());
		AddExp("std::>", T_U8U8, new NativeU8GT());
		AddExp("std::>=", T_U8U8, new NativeU8GEQ());
		AddExp("std::*x", T_U8U8, new NativeU8MulX());
		AddExp("std::*h", T_U8U8, new NativeU8MulH());
		AddExp("std::*hl", T_U8U8, new NativeU8MulHL());
		AddExp("std::^u8", T_U8, new NativeU8ToU8());
		AddExp("std::^u16", T_U8, new NativeU8ToU16());
		AddExp("std::^u32", T_U8, new NativeU8ToU32());
		AddExp("std::^u64", T_U8, new NativeU8ToU64());
		AddExp("std::^i8", T_U8, new NativeU8ToI8());
		AddExp("std::^i16", T_U8, new NativeU8ToI16());
		AddExp("std::^i32", T_U8, new NativeU8ToI32());
		AddExp("std::^i64", T_U8, new NativeU8ToI64());
		AddExp("std::^int", T_U8, new NativeU8ToInt());

		AddExp("std::+", T_U16U16, new NativeU16Add());
		AddExp("std::-", T_U16U16, new NativeU16Sub());
		AddExp("std::*", T_U16U16, new NativeU16Mul());
		AddExp("std::/", T_U16U16, new NativeU16Div());
		AddExp("std::%", T_U16U16, new NativeU16Mod());
		AddExp("std::/%", T_U16U16, new NativeU16DivMod());
		AddExp("std::neg", T_U16, new NativeU16Neg());
		AddExp("std::++", T_U16, new NativeU16Inc());
		AddExp("std::--", T_U16, new NativeU16Dec());
		AddExp("std::and", T_U16U16, new NativeU16And());
		AddExp("std::or", T_U16U16, new NativeU16Or());
		AddExp("std::xor", T_U16U16, new NativeU16Xor());
		AddExp("std::eqv", T_U16U16, new NativeU16Eqv());
		AddExp("std::not", T_U16, new NativeU16Not());
		AddExp("std::<", T_U16U16, new NativeU16LT());
		AddExp("std::<=", T_U16U16, new NativeU16LEQ());
		AddExp("std::>", T_U16U16, new NativeU16GT());
		AddExp("std::>=", T_U16U16, new NativeU16GEQ());
		AddExp("std::*x", T_U16U16, new NativeU16MulX());
		AddExp("std::*h", T_U16U16, new NativeU16MulH());
		AddExp("std::*hl", T_U16U16, new NativeU16MulHL());
		AddExp("std::^u8", T_U16, new NativeU16ToU8());
		AddExp("std::^u16", T_U16, new NativeU16ToU16());
		AddExp("std::^u32", T_U16, new NativeU16ToU32());
		AddExp("std::^u64", T_U16, new NativeU16ToU64());
		AddExp("std::^i8", T_U16, new NativeU16ToI8());
		AddExp("std::^i16", T_U16, new NativeU16ToI16());
		AddExp("std::^i32", T_U16, new NativeU16ToI32());
		AddExp("std::^i64", T_U16, new NativeU16ToI64());
		AddExp("std::^int", T_U16, new NativeU16ToInt());

		AddExp("std::+", T_U32U32, new NativeU32Add());
		AddExp("std::-", T_U32U32, new NativeU32Sub());
		AddExp("std::*", T_U32U32, new NativeU32Mul());
		AddExp("std::/", T_U32U32, new NativeU32Div());
		AddExp("std::%", T_U32U32, new NativeU32Mod());
		AddExp("std::/%", T_U32U32, new NativeU32DivMod());
		AddExp("std::neg", T_U32, new NativeU32Neg());
		AddExp("std::++", T_U32, new NativeU32Inc());
		AddExp("std::--", T_U32, new NativeU32Dec());
		AddExp("std::and", T_U32U32, new NativeU32And());
		AddExp("std::or", T_U32U32, new NativeU32Or());
		AddExp("std::xor", T_U32U32, new NativeU32Xor());
		AddExp("std::eqv", T_U32U32, new NativeU32Eqv());
		AddExp("std::not", T_U32, new NativeU32Not());
		AddExp("std::<", T_U32U32, new NativeU32LT());
		AddExp("std::<=", T_U32U32, new NativeU32LEQ());
		AddExp("std::>", T_U32U32, new NativeU32GT());
		AddExp("std::>=", T_U32U32, new NativeU32GEQ());
		AddExp("std::*x", T_U32U32, new NativeU32MulX());
		AddExp("std::*h", T_U32U32, new NativeU32MulH());
		AddExp("std::*hl", T_U32U32, new NativeU32MulHL());
		AddExp("std::^u8", T_U32, new NativeU32ToU8());
		AddExp("std::^u16", T_U32, new NativeU32ToU16());
		AddExp("std::^u32", T_U32, new NativeU32ToU32());
		AddExp("std::^u64", T_U32, new NativeU32ToU64());
		AddExp("std::^i8", T_U32, new NativeU32ToI8());
		AddExp("std::^i16", T_U32, new NativeU32ToI16());
		AddExp("std::^i32", T_U32, new NativeU32ToI32());
		AddExp("std::^i64", T_U32, new NativeU32ToI64());
		AddExp("std::^int", T_U32, new NativeU32ToInt());

		AddExp("std::+", T_U64U64, new NativeU64Add());
		AddExp("std::-", T_U64U64, new NativeU64Sub());
		AddExp("std::*", T_U64U64, new NativeU64Mul());
		AddExp("std::/", T_U64U64, new NativeU64Div());
		AddExp("std::%", T_U64U64, new NativeU64Mod());
		AddExp("std::/%", T_U64U64, new NativeU64DivMod());
		AddExp("std::neg", T_U64, new NativeU64Neg());
		AddExp("std::++", T_U64, new NativeU64Inc());
		AddExp("std::--", T_U64, new NativeU64Dec());
		AddExp("std::and", T_U64U64, new NativeU64And());
		AddExp("std::or", T_U64U64, new NativeU64Or());
		AddExp("std::xor", T_U64U64, new NativeU64Xor());
		AddExp("std::eqv", T_U64U64, new NativeU64Eqv());
		AddExp("std::not", T_U64, new NativeU64Not());
		AddExp("std::<", T_U64U64, new NativeU64LT());
		AddExp("std::<=", T_U64U64, new NativeU64LEQ());
		AddExp("std::>", T_U64U64, new NativeU64GT());
		AddExp("std::>=", T_U64U64, new NativeU64GEQ());
		AddExp("std::*h", T_U64U64, new NativeU64MulH());
		AddExp("std::*hl", T_U64U64, new NativeU64MulHL());
		AddExp("std::^u8", T_U64, new NativeU64ToU8());
		AddExp("std::^u16", T_U64, new NativeU64ToU16());
		AddExp("std::^u32", T_U64, new NativeU64ToU32());
		AddExp("std::^u64", T_U64, new NativeU64ToU64());
		AddExp("std::^i8", T_U64, new NativeU64ToI8());
		AddExp("std::^i16", T_U64, new NativeU64ToI16());
		AddExp("std::^i32", T_U64, new NativeU64ToI32());
		AddExp("std::^i64", T_U64, new NativeU64ToI64());
		AddExp("std::^int", T_U64, new NativeU64ToInt());

		AddExp("std::+", T_I8I8, new NativeI8Add());
		AddExp("std::-", T_I8I8, new NativeI8Sub());
		AddExp("std::*", T_I8I8, new NativeI8Mul());
		AddExp("std::/", T_I8I8, new NativeI8Div());
		AddExp("std::%", T_I8I8, new NativeI8Mod());
		AddExp("std::/%", T_I8I8, new NativeI8DivMod());
		AddExp("std::neg", T_I8, new NativeI8Neg());
		AddExp("std::++", T_I8, new NativeI8Inc());
		AddExp("std::--", T_I8, new NativeI8Dec());
		AddExp("std::and", T_I8I8, new NativeI8And());
		AddExp("std::or", T_I8I8, new NativeI8Or());
		AddExp("std::xor", T_I8I8, new NativeI8Xor());
		AddExp("std::eqv", T_I8I8, new NativeI8Eqv());
		AddExp("std::not", T_I8, new NativeI8Not());
		AddExp("std::<", T_I8I8, new NativeI8LT());
		AddExp("std::<=", T_I8I8, new NativeI8LEQ());
		AddExp("std::>", T_I8I8, new NativeI8GT());
		AddExp("std::>=", T_I8I8, new NativeI8GEQ());
		AddExp("std::*x", T_I8I8, new NativeI8MulX());
		AddExp("std::*h", T_I8I8, new NativeI8MulH());
		AddExp("std::*hl", T_I8I8, new NativeI8MulHL());
		AddExp("std::^u8", T_I8, new NativeI8ToU8());
		AddExp("std::^u16", T_I8, new NativeI8ToU16());
		AddExp("std::^u32", T_I8, new NativeI8ToU32());
		AddExp("std::^u64", T_I8, new NativeI8ToU64());
		AddExp("std::^i8", T_I8, new NativeI8ToI8());
		AddExp("std::^i16", T_I8, new NativeI8ToI16());
		AddExp("std::^i32", T_I8, new NativeI8ToI32());
		AddExp("std::^i64", T_I8, new NativeI8ToI64());
		AddExp("std::^int", T_I8, new NativeI8ToInt());

		AddExp("std::+", T_I16I16, new NativeI16Add());
		AddExp("std::-", T_I16I16, new NativeI16Sub());
		AddExp("std::*", T_I16I16, new NativeI16Mul());
		AddExp("std::/", T_I16I16, new NativeI16Div());
		AddExp("std::%", T_I16I16, new NativeI16Mod());
		AddExp("std::/%", T_I16I16, new NativeI16DivMod());
		AddExp("std::neg", T_I16, new NativeI16Neg());
		AddExp("std::++", T_I16, new NativeI16Inc());
		AddExp("std::--", T_I16, new NativeI16Dec());
		AddExp("std::and", T_I16I16, new NativeI16And());
		AddExp("std::or", T_I16I16, new NativeI16Or());
		AddExp("std::xor", T_I16I16, new NativeI16Xor());
		AddExp("std::eqv", T_I16I16, new NativeI16Eqv());
		AddExp("std::not", T_I16, new NativeI16Not());
		AddExp("std::<", T_I16I16, new NativeI16LT());
		AddExp("std::<=", T_I16I16, new NativeI16LEQ());
		AddExp("std::>", T_I16I16, new NativeI16GT());
		AddExp("std::>=", T_I16I16, new NativeI16GEQ());
		AddExp("std::*x", T_I16I16, new NativeI16MulX());
		AddExp("std::*h", T_I16I16, new NativeI16MulH());
		AddExp("std::*hl", T_I16I16, new NativeI16MulHL());
		AddExp("std::^u8", T_I16, new NativeI16ToU8());
		AddExp("std::^u16", T_I16, new NativeI16ToU16());
		AddExp("std::^u32", T_I16, new NativeI16ToU32());
		AddExp("std::^u64", T_I16, new NativeI16ToU64());
		AddExp("std::^i8", T_I16, new NativeI16ToI8());
		AddExp("std::^i16", T_I16, new NativeI16ToI16());
		AddExp("std::^i32", T_I16, new NativeI16ToI32());
		AddExp("std::^i64", T_I16, new NativeI16ToI64());
		AddExp("std::^int", T_I16, new NativeI16ToInt());

		AddExp("std::+", T_I32I32, new NativeI32Add());
		AddExp("std::-", T_I32I32, new NativeI32Sub());
		AddExp("std::*", T_I32I32, new NativeI32Mul());
		AddExp("std::/", T_I32I32, new NativeI32Div());
		AddExp("std::%", T_I32I32, new NativeI32Mod());
		AddExp("std::/%", T_I32I32, new NativeI32DivMod());
		AddExp("std::neg", T_I32, new NativeI32Neg());
		AddExp("std::++", T_I32, new NativeI32Inc());
		AddExp("std::--", T_I32, new NativeI32Dec());
		AddExp("std::and", T_I32I32, new NativeI32And());
		AddExp("std::or", T_I32I32, new NativeI32Or());
		AddExp("std::xor", T_I32I32, new NativeI32Xor());
		AddExp("std::eqv", T_I32I32, new NativeI32Eqv());
		AddExp("std::not", T_I32, new NativeI32Not());
		AddExp("std::<", T_I32I32, new NativeI32LT());
		AddExp("std::<=", T_I32I32, new NativeI32LEQ());
		AddExp("std::>", T_I32I32, new NativeI32GT());
		AddExp("std::>=", T_I32I32, new NativeI32GEQ());
		AddExp("std::*x", T_I32I32, new NativeI32MulX());
		AddExp("std::*h", T_I32I32, new NativeI32MulH());
		AddExp("std::*hl", T_I32I32, new NativeI32MulHL());
		AddExp("std::^u8", T_I32, new NativeI32ToU8());
		AddExp("std::^u16", T_I32, new NativeI32ToU16());
		AddExp("std::^u32", T_I32, new NativeI32ToU32());
		AddExp("std::^u64", T_I32, new NativeI32ToU64());
		AddExp("std::^i8", T_I32, new NativeI32ToI8());
		AddExp("std::^i16", T_I32, new NativeI32ToI16());
		AddExp("std::^i32", T_I32, new NativeI32ToI32());
		AddExp("std::^i64", T_I32, new NativeI32ToI64());
		AddExp("std::^int", T_I32, new NativeI32ToInt());

		AddExp("std::+", T_I64I64, new NativeI64Add());
		AddExp("std::-", T_I64I64, new NativeI64Sub());
		AddExp("std::*", T_I64I64, new NativeI64Mul());
		AddExp("std::/", T_I64I64, new NativeI64Div());
		AddExp("std::%", T_I64I64, new NativeI64Mod());
		AddExp("std::/%", T_I64I64, new NativeI64DivMod());
		AddExp("std::neg", T_I64, new NativeI64Neg());
		AddExp("std::++", T_I64, new NativeI64Inc());
		AddExp("std::--", T_I64, new NativeI64Dec());
		AddExp("std::and", T_I64I64, new NativeI64And());
		AddExp("std::or", T_I64I64, new NativeI64Or());
		AddExp("std::xor", T_I64I64, new NativeI64Xor());
		AddExp("std::eqv", T_I64I64, new NativeI64Eqv());
		AddExp("std::not", T_I64, new NativeI64Not());
		AddExp("std::<", T_I64I64, new NativeI64LT());
		AddExp("std::<=", T_I64I64, new NativeI64LEQ());
		AddExp("std::>", T_I64I64, new NativeI64GT());
		AddExp("std::>=", T_I64I64, new NativeI64GEQ());
		AddExp("std::*h", T_I64I64, new NativeI64MulH());
		AddExp("std::*hl", T_I64I64, new NativeI64MulHL());
		AddExp("std::^u8", T_I64, new NativeI64ToU8());
		AddExp("std::^u16", T_I64, new NativeI64ToU16());
		AddExp("std::^u32", T_I64, new NativeI64ToU32());
		AddExp("std::^u64", T_I64, new NativeI64ToU64());
		AddExp("std::^i8", T_I64, new NativeI64ToI8());
		AddExp("std::^i16", T_I64, new NativeI64ToI16());
		AddExp("std::^i32", T_I64, new NativeI64ToI32());
		AddExp("std::^i64", T_I64, new NativeI64ToI64());
		AddExp("std::^int", T_I64, new NativeI64ToInt());

		AddExp("std::+", T_IntInt, new NativeIntAdd());
		AddExp("std::-", T_IntInt, new NativeIntSub());
		AddExp("std::*", T_IntInt, new NativeIntMul());
		AddExp("std::/", T_IntInt, new NativeIntDiv());
		AddExp("std::%", T_IntInt, new NativeIntMod());
		AddExp("std::/%", T_IntInt, new NativeIntDivMod());
		AddExp("std::neg", T_Int, new NativeIntNeg());
		AddExp("std::++", T_Int, new NativeIntInc());
		AddExp("std::--", T_Int, new NativeIntDec());
		AddExp("std::and", T_IntInt, new NativeIntAnd());
		AddExp("std::or", T_IntInt, new NativeIntOr());
		AddExp("std::xor", T_IntInt, new NativeIntXor());
		AddExp("std::eqv", T_IntInt, new NativeIntEqv());
		AddExp("std::not", T_Int, new NativeIntNot());
		AddExp("std::<", T_IntInt, new NativeIntLT());
		AddExp("std::<=", T_IntInt, new NativeIntLEQ());
		AddExp("std::>", T_IntInt, new NativeIntGT());
		AddExp("std::>=", T_IntInt, new NativeIntGEQ());
		AddExp("std::^u8", T_Int, new NativeIntToU8());
		AddExp("std::^u16", T_Int, new NativeIntToU16());
		AddExp("std::^u32", T_Int, new NativeIntToU32());
		AddExp("std::^u64", T_Int, new NativeIntToU64());
		AddExp("std::^i8", T_Int, new NativeIntToI8());
		AddExp("std::^i16", T_Int, new NativeIntToI16());
		AddExp("std::^i32", T_Int, new NativeIntToI32());
		AddExp("std::^i64", T_Int, new NativeIntToI64());
		AddExp("std::^int", T_Int, new NativeIntToInt());

		/* obsolete
		AddExp("std::=", T_StringString, new NativeStringEquals());
		AddExp("std::<>", T_StringString, new NativeStringNotEquals());
		AddExp("std::length", T_String, new NativeStringLength());
		AddExp("std::@", T_IntString, new NativeStringAt());
		*/
		AddExp("std::+", T_StringString, new NativeStringConcat());
		AddExp("std::fail", T_String, new NativeStringFail());

		AddExp("std::new", T_Type, new NativeTypeNew());

		AddExp("std::name", T_Type, new NativeTypeName());
		AddExp("std::array", T_Type, new NativeArrayRef());
		AddExp("std::array&", T_Type, new NativeArrayEmbed());
	}

	static void InitIO()
	{
		AddExp("std.io::print", T_Object, new NativePrint());
		AddExp("std.io::println", T_Object, new NativePrintln());
	}

	internal static ulong Umulh64(ulong a, ulong b)
	{
		ulong ah = a >> 32;
		ulong al = a & 0xFFFFFFFF;
		ulong bh = b >> 32;
		ulong bl = b & 0xFFFFFFFF;
		ulong t1 = al * bh + ((al * bl) >> 32);
		ulong t2 = ah * bl;
		ulong z = ah * bh + (t1 >> 32) + (t2 >> 32);
		return z + (((t1 & 0xFFFFFFFF) + (t2 & 0xFFFFFFFF)) >> 32);
	}

	internal static long Smulh64(long a, long b)
	{
		/*
		 * 'sa' is the sign of 'a' (0xFFF..F is a < 0, 0 otherwise).
		 * We want to compute the unsigned 128-bit product of two
		 * unsigned 128-bit values:
		 *    ((sa << 64) + a) * ((sb << 64) + b)  mod 2^128
		 *  = ((sa * b + sb * a) << 64) + a * b    mod 2^128
		 *
		 * Umulh64() helps computing a*b over 128 bits. The
		 * sa*b+sb*a value only needs to be computed over 64 bits.
		 * Moreover:
		 *
		 *   if sa = 0, then sa*b = 0
		 *   if sa = 0xFFF..F, then sa*b = -b mod 2^64
		 *
		 * We can thus compute sa*b without using multiplications.
		 */
		ulong sa = (ulong)(a >> 63);
		ulong sb = (ulong)(b >> 63);
		ulong t = Umulh64((ulong)a, (ulong)b);
		t += (sa & (ulong)-b) + (sb & (ulong)-a);
		return (long)t;
	}

	internal static void PushInt(CPU cpu, long x)
	{
		if (x < (long)Int32.MinValue || x > (long)Int32.MaxValue) {
			throw new Exception("overflow on std::int");
		}
		cpu.Push(XValue.MakeInt((int)x));
	}
}

/* ======================================================================= */
/* std::object */

class NativeObjectEquals : FunctionNative {

	internal NativeObjectEquals()
		: base("std::= (std::object)",
			Kernel.T_ObjectObject, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		XValue b = cpu.Pop();
		XValue a = cpu.Pop();
		cpu.Push(a == b);
	}
}

class NativeObjectNotEquals : FunctionNative {

	internal NativeObjectNotEquals()
		: base("std::<> (std::object)",
			Kernel.T_ObjectObject, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		XValue b = cpu.Pop();
		XValue a = cpu.Pop();
		cpu.Push(a != b);
	}
}

class NativeObjectDup : Function {

	internal NativeObjectDup()
		: base("std::dup (std::object)")
	{
	}

	internal override void Run(CPU cpu)
	{
		cpu.Push(cpu.Peek(0));
	}

	internal override CCNode Enter(CCStack stack,
		CCNodeEntry parent, CCNode next)
	{
		CCNode node = new CCNodeNativeDup(this, next);
		node.MergeStack(stack);
		return node;
	}
}

class NativeObjectDrop : FunctionNative {

	internal NativeObjectDrop()
		: base("std::drop (std::object)", Kernel.T_Object, XType.ZERO)
	{
	}

	internal override void Run(CPU cpu)
	{
		cpu.Pop();
	}
}

class NativeObjectSwap : Function {

	internal NativeObjectSwap()
		: base("std::swap (std::object)")
	{
	}

	internal override void Run(CPU cpu)
	{
		XValue b = cpu.Pop();
		XValue a = cpu.Pop();
		cpu.Push(b);
		cpu.Push(a);
	}

	internal override CCNode Enter(CCStack stack,
		CCNodeEntry parent, CCNode next)
	{
		CCNode node = new CCNodeNativeSwap(next);
		node.MergeStack(stack);
		return node;
	}
}

class NativeObjectOver : Function {

	internal NativeObjectOver()
		: base("std::over (std::object)")
	{
	}

	internal override void Run(CPU cpu)
	{
		cpu.Push(cpu.Peek(1));
	}

	internal override CCNode Enter(CCStack stack,
		CCNodeEntry parent, CCNode next)
	{
		CCNode node = new CCNodeNativeOver(next);
		node.MergeStack(stack);
		return node;
	}
}

class NativeObjectRot : Function {

	internal NativeObjectRot()
		: base("std::rot (std::object)")
	{
	}

	internal override void Run(CPU cpu)
	{
		cpu.Rot(2);
	}

	internal override CCNode Enter(CCStack stack,
		CCNodeEntry parent, CCNode next)
	{
		CCNode node = new CCNodeNativeRot(next);
		node.MergeStack(stack);
		return node;
	}
}

class NativeObjectNRot : Function {

	internal NativeObjectNRot()
		: base("std::-rot (std::object)")
	{
	}

	internal override void Run(CPU cpu)
	{
		cpu.NRot(2);
	}

	internal override CCNode Enter(CCStack stack,
		CCNodeEntry parent, CCNode next)
	{
		CCNode node = new CCNodeNativeNRot(next);
		node.MergeStack(stack);
		return node;
	}
}

/* ======================================================================= */
/* std::bool */

class NativeBoolAnd : FunctionNative {

	internal NativeBoolAnd()
		: base("std::and (std::bool)", Kernel.T_BoolBool, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		XValue b = cpu.Pop();
		XValue a = cpu.Pop();
		cpu.Push(a.Bool & b.Bool);
	}
}

class NativeBoolOr : FunctionNative {

	internal NativeBoolOr()
		: base("std::or (std::bool)", Kernel.T_BoolBool, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		XValue b = cpu.Pop();
		XValue a = cpu.Pop();
		cpu.Push(a.Bool | b.Bool);
	}
}

class NativeBoolXor : FunctionNative {

	internal NativeBoolXor()
		: base("std::xor (std::bool)", Kernel.T_BoolBool, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		XValue b = cpu.Pop();
		XValue a = cpu.Pop();
		cpu.Push(a.Bool ^ b.Bool);
	}
}

class NativeBoolEqv : FunctionNative {

	internal NativeBoolEqv()
		: base("std::eqv (std::bool)", Kernel.T_BoolBool, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		XValue b = cpu.Pop();
		XValue a = cpu.Pop();
		cpu.Push(!(a.Bool ^ b.Bool));
	}
}

class NativeBoolNot : FunctionNative {

	internal NativeBoolNot()
		: base("std::not (std::bool)", Kernel.T_Bool, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		XValue a = cpu.Pop();
		cpu.Push(!a.Bool);
	}
}

/* ======================================================================= */
/* std::u8 */

class NativeU8Add : FunctionNative {

	internal NativeU8Add()
		: base("std::+ (std::u8)", Kernel.T_U8U8, Kernel.T_U8)
	{
	}

	internal override void Run(CPU cpu)
	{
		byte b = cpu.Pop();
		byte a = cpu.Pop();
		cpu.Push((byte)(a + b));
	}
}

class NativeU8Sub : FunctionNative {

	internal NativeU8Sub()
		: base("std::- (std::u8)", Kernel.T_U8U8, Kernel.T_U8)
	{
	}

	internal override void Run(CPU cpu)
	{
		byte b = cpu.Pop();
		byte a = cpu.Pop();
		cpu.Push((byte)(a - b));
	}
}

class NativeU8Mul : FunctionNative {

	internal NativeU8Mul()
		: base("std::* (std::u8)", Kernel.T_U8U8, Kernel.T_U8)
	{
	}

	internal override void Run(CPU cpu)
	{
		byte b = cpu.Pop();
		byte a = cpu.Pop();
		cpu.Push((byte)(a * b));
	}
}

class NativeU8Div : FunctionNative {

	internal NativeU8Div()
		: base("std::/ (std::u8)", Kernel.T_U8U8, Kernel.T_U8)
	{
	}

	internal override void Run(CPU cpu)
	{
		byte b = cpu.Pop();
		byte a = cpu.Pop();
		if (b == 0) {
			throw new Exception("division by zero");
		}
		cpu.Push((byte)(a / b));
	}
}

class NativeU8Mod : FunctionNative {

	internal NativeU8Mod()
		: base("std::% (std::u8)", Kernel.T_U8U8, Kernel.T_U8)
	{
	}

	internal override void Run(CPU cpu)
	{
		byte b = cpu.Pop();
		byte a = cpu.Pop();
		if (b == 0) {
			throw new Exception("division by zero");
		}
		cpu.Push((byte)(a % b));
	}
}

class NativeU8DivMod : FunctionNative {

	internal NativeU8DivMod()
		: base("std::/% (std::u8)", Kernel.T_U8U8, Kernel.T_U8U8)
	{
	}

	internal override void Run(CPU cpu)
	{
		byte b = cpu.Pop();
		byte a = cpu.Pop();
		if (b == 0) {
			throw new Exception("division by zero");
		}
		cpu.Push((byte)(a / b));
		cpu.Push((byte)(a % b));
	}
}

class NativeU8Neg : FunctionNative {

	internal NativeU8Neg()
		: base("std::neg (std::u8)", Kernel.T_U8, Kernel.T_U8)
	{
	}

	internal override void Run(CPU cpu)
	{
		byte a = cpu.Pop();
		cpu.Push((byte)-(long)a);
	}
}

class NativeU8Inc : FunctionNative {

	internal NativeU8Inc()
		: base("std::++ (std::u8)", Kernel.T_U8, Kernel.T_U8)
	{
	}

	internal override void Run(CPU cpu)
	{
		byte a = cpu.Pop();
		cpu.Push((byte)(a + 1));
	}
}

class NativeU8Dec : FunctionNative {

	internal NativeU8Dec()
		: base("std::-- (std::u8)", Kernel.T_U8, Kernel.T_U8)
	{
	}

	internal override void Run(CPU cpu)
	{
		byte a = cpu.Pop();
		cpu.Push((byte)(a - 1));
	}
}

class NativeU8And : FunctionNative {

	internal NativeU8And()
		: base("std::and (std::u8)", Kernel.T_U8U8, Kernel.T_U8)
	{
	}

	internal override void Run(CPU cpu)
	{
		byte b = cpu.Pop();
		byte a = cpu.Pop();
		cpu.Push((byte)(a & b));
	}
}

class NativeU8Or : FunctionNative {

	internal NativeU8Or()
		: base("std::or (std::u8)", Kernel.T_U8U8, Kernel.T_U8)
	{
	}

	internal override void Run(CPU cpu)
	{
		byte b = cpu.Pop();
		byte a = cpu.Pop();
		cpu.Push((byte)(a | b));
	}
}

class NativeU8Xor : FunctionNative {

	internal NativeU8Xor()
		: base("std::xor (std::u8)", Kernel.T_U8U8, Kernel.T_U8)
	{
	}

	internal override void Run(CPU cpu)
	{
		byte b = cpu.Pop();
		byte a = cpu.Pop();
		cpu.Push((byte)(a ^ b));
	}
}

class NativeU8Eqv : FunctionNative {

	internal NativeU8Eqv()
		: base("std::eqv (std::u8)", Kernel.T_U8U8, Kernel.T_U8)
	{
	}

	internal override void Run(CPU cpu)
	{
		byte b = cpu.Pop();
		byte a = cpu.Pop();
		cpu.Push((byte)~(a ^ b));
	}
}

class NativeU8Not : FunctionNative {

	internal NativeU8Not()
		: base("std::not (std::u8)", Kernel.T_U8, Kernel.T_U8)
	{
	}

	internal override void Run(CPU cpu)
	{
		byte a = cpu.Pop();
		cpu.Push((byte)~a);
	}
}

class NativeU8LT : FunctionNative {

	internal NativeU8LT()
		: base("std::< (std::u8)", Kernel.T_U8U8, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		byte b = cpu.Pop();
		byte a = cpu.Pop();
		cpu.Push(a < b);
	}
}

class NativeU8LEQ : FunctionNative {

	internal NativeU8LEQ()
		: base("std::<= (std::u8)", Kernel.T_U8U8, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		byte b = cpu.Pop();
		byte a = cpu.Pop();
		cpu.Push(a <= b);
	}
}

class NativeU8GT : FunctionNative {

	internal NativeU8GT()
		: base("std::> (std::u8)", Kernel.T_U8U8, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		byte b = cpu.Pop();
		byte a = cpu.Pop();
		cpu.Push(a > b);
	}
}

class NativeU8GEQ : FunctionNative {

	internal NativeU8GEQ()
		: base("std::>= (std::u8)", Kernel.T_U8U8, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		byte b = cpu.Pop();
		byte a = cpu.Pop();
		cpu.Push(a >= b);
	}
}

class NativeU8MulX : FunctionNative {

	internal NativeU8MulX()
		: base("std::*x (std::u8)", Kernel.T_U8U8, Kernel.T_U16)
	{
	}

	internal override void Run(CPU cpu)
	{
		byte b = cpu.Pop();
		byte a = cpu.Pop();
		cpu.Push((ushort)((ushort)a * (ushort)b));
	}
}

class NativeU8MulH : FunctionNative {

	internal NativeU8MulH()
		: base("std::*h (std::u8)", Kernel.T_U8U8, Kernel.T_U8)
	{
	}

	internal override void Run(CPU cpu)
	{
		byte b = cpu.Pop();
		byte a = cpu.Pop();
		ushort r = (ushort)((ushort)a * (ushort)b);
		cpu.Push((byte)(r >> 8));
	}
}

class NativeU8MulHL : FunctionNative {

	internal NativeU8MulHL()
		: base("std::*hl (std::u8)", Kernel.T_U8U8, Kernel.T_U8U8)
	{
	}

	internal override void Run(CPU cpu)
	{
		byte b = cpu.Pop();
		byte a = cpu.Pop();
		ushort r = (ushort)((ushort)a * (ushort)b);
		cpu.Push((byte)(r >> 8));
		cpu.Push((byte)r);
	}
}

class NativeU8ToU8 : FunctionNative {

	internal NativeU8ToU8()
		: base("std::^u8 (std::u8)", Kernel.T_U8, Kernel.T_U8)
	{
	}

	internal override void Run(CPU cpu)
	{
		byte a = cpu.Pop();
		cpu.Push((byte)a);
	}
}

class NativeU8ToU16 : FunctionNative {

	internal NativeU8ToU16()
		: base("std::^u16 (std::u8)", Kernel.T_U8, Kernel.T_U16)
	{
	}

	internal override void Run(CPU cpu)
	{
		byte a = cpu.Pop();
		cpu.Push((ushort)a);
	}
}

class NativeU8ToU32 : FunctionNative {

	internal NativeU8ToU32()
		: base("std::^u32 (std::u8)", Kernel.T_U8, Kernel.T_U32)
	{
	}

	internal override void Run(CPU cpu)
	{
		byte a = cpu.Pop();
		cpu.Push((uint)a);
	}
}

class NativeU8ToU64 : FunctionNative {

	internal NativeU8ToU64()
		: base("std::^u64 (std::u8)", Kernel.T_U8, Kernel.T_U64)
	{
	}

	internal override void Run(CPU cpu)
	{
		byte a = cpu.Pop();
		cpu.Push((ulong)a);
	}
}

class NativeU8ToI8 : FunctionNative {

	internal NativeU8ToI8()
		: base("std::^i8 (std::u8)", Kernel.T_U8, Kernel.T_I8)
	{
	}

	internal override void Run(CPU cpu)
	{
		byte a = cpu.Pop();
		cpu.Push((sbyte)a);
	}
}

class NativeU8ToI16 : FunctionNative {

	internal NativeU8ToI16()
		: base("std::^i16 (std::u8)", Kernel.T_U8, Kernel.T_I16)
	{
	}

	internal override void Run(CPU cpu)
	{
		byte a = cpu.Pop();
		cpu.Push((short)a);
	}
}

class NativeU8ToI32 : FunctionNative {

	internal NativeU8ToI32()
		: base("std::^i32 (std::u8)", Kernel.T_U8, Kernel.T_I32)
	{
	}

	internal override void Run(CPU cpu)
	{
		byte a = cpu.Pop();
		cpu.Push((int)a);
	}
}

class NativeU8ToI64 : FunctionNative {

	internal NativeU8ToI64()
		: base("std::^i64 (std::u8)", Kernel.T_U8, Kernel.T_I64)
	{
	}

	internal override void Run(CPU cpu)
	{
		byte a = cpu.Pop();
		cpu.Push((long)a);
	}
}

class NativeU8ToInt : FunctionNative {

	internal NativeU8ToInt()
		: base("std::^int (std::u8)", Kernel.T_U8, Kernel.T_U8)
	{
	}

	internal override void Run(CPU cpu)
	{
		byte a = cpu.Pop();
		cpu.Push(XValue.MakeInt((int)a));
	}
}

/* ======================================================================= */
/* std::u16 */

class NativeU16Add : FunctionNative {

	internal NativeU16Add()
		: base("std::+ (std::u16)", Kernel.T_U16U16, Kernel.T_U16)
	{
	}

	internal override void Run(CPU cpu)
	{
		ushort b = cpu.Pop();
		ushort a = cpu.Pop();
		cpu.Push((ushort)(a + b));
	}
}

class NativeU16Sub : FunctionNative {

	internal NativeU16Sub()
		: base("std::- (std::u16)", Kernel.T_U16U16, Kernel.T_U16)
	{
	}

	internal override void Run(CPU cpu)
	{
		ushort b = cpu.Pop();
		ushort a = cpu.Pop();
		cpu.Push((ushort)(a - b));
	}
}

class NativeU16Mul : FunctionNative {

	internal NativeU16Mul()
		: base("std::* (std::u16)", Kernel.T_U16U16, Kernel.T_U16)
	{
	}

	internal override void Run(CPU cpu)
	{
		ushort b = cpu.Pop();
		ushort a = cpu.Pop();
		cpu.Push((ushort)(a * b));
	}
}

class NativeU16Div : FunctionNative {

	internal NativeU16Div()
		: base("std::/ (std::u16)", Kernel.T_U16U16, Kernel.T_U16)
	{
	}

	internal override void Run(CPU cpu)
	{
		ushort b = cpu.Pop();
		ushort a = cpu.Pop();
		if (b == 0) {
			throw new Exception("division by zero");
		}
		cpu.Push((ushort)(a / b));
	}
}

class NativeU16Mod : FunctionNative {

	internal NativeU16Mod()
		: base("std::% (std::u16)", Kernel.T_U16U16, Kernel.T_U16)
	{
	}

	internal override void Run(CPU cpu)
	{
		ushort b = cpu.Pop();
		ushort a = cpu.Pop();
		if (b == 0) {
			throw new Exception("division by zero");
		}
		cpu.Push((ushort)(a % b));
	}
}

class NativeU16DivMod : FunctionNative {

	internal NativeU16DivMod()
		: base("std::/% (std::u16)", Kernel.T_U16U16, Kernel.T_U16U16)
	{
	}

	internal override void Run(CPU cpu)
	{
		ushort b = cpu.Pop();
		ushort a = cpu.Pop();
		if (b == 0) {
			throw new Exception("division by zero");
		}
		cpu.Push((ushort)(a / b));
		cpu.Push((ushort)(a % b));
	}
}

class NativeU16Neg : FunctionNative {

	internal NativeU16Neg()
		: base("std::neg (std::u16)", Kernel.T_U16, Kernel.T_U16)
	{
	}

	internal override void Run(CPU cpu)
	{
		ushort a = cpu.Pop();
		cpu.Push((ushort)-(long)a);
	}
}

class NativeU16Inc : FunctionNative {

	internal NativeU16Inc()
		: base("std::++ (std::u16)", Kernel.T_U16, Kernel.T_U16)
	{
	}

	internal override void Run(CPU cpu)
	{
		ushort a = cpu.Pop();
		cpu.Push((ushort)(a + 1));
	}
}

class NativeU16Dec : FunctionNative {

	internal NativeU16Dec()
		: base("std::-- (std::u16)", Kernel.T_U16, Kernel.T_U16)
	{
	}

	internal override void Run(CPU cpu)
	{
		ushort a = cpu.Pop();
		cpu.Push((ushort)(a - 1));
	}
}

class NativeU16And : FunctionNative {

	internal NativeU16And()
		: base("std::and (std::u16)", Kernel.T_U16U16, Kernel.T_U16)
	{
	}

	internal override void Run(CPU cpu)
	{
		ushort b = cpu.Pop();
		ushort a = cpu.Pop();
		cpu.Push((ushort)(a & b));
	}
}

class NativeU16Or : FunctionNative {

	internal NativeU16Or()
		: base("std::or (std::u16)", Kernel.T_U16U16, Kernel.T_U16)
	{
	}

	internal override void Run(CPU cpu)
	{
		ushort b = cpu.Pop();
		ushort a = cpu.Pop();
		cpu.Push((ushort)(a | b));
	}
}

class NativeU16Xor : FunctionNative {

	internal NativeU16Xor()
		: base("std::xor (std::u16)", Kernel.T_U16U16, Kernel.T_U16)
	{
	}

	internal override void Run(CPU cpu)
	{
		ushort b = cpu.Pop();
		ushort a = cpu.Pop();
		cpu.Push((ushort)(a ^ b));
	}
}

class NativeU16Eqv : FunctionNative {

	internal NativeU16Eqv()
		: base("std::eqv (std::u16)", Kernel.T_U16U16, Kernel.T_U16)
	{
	}

	internal override void Run(CPU cpu)
	{
		ushort b = cpu.Pop();
		ushort a = cpu.Pop();
		cpu.Push((ushort)~(a ^ b));
	}
}

class NativeU16Not : FunctionNative {

	internal NativeU16Not()
		: base("std::not (std::u16)", Kernel.T_U16, Kernel.T_U16)
	{
	}

	internal override void Run(CPU cpu)
	{
		ushort a = cpu.Pop();
		cpu.Push((ushort)~a);
	}
}

class NativeU16LT : FunctionNative {

	internal NativeU16LT()
		: base("std::< (std::u16)", Kernel.T_U16U16, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		ushort b = cpu.Pop();
		ushort a = cpu.Pop();
		cpu.Push(a < b);
	}
}

class NativeU16LEQ : FunctionNative {

	internal NativeU16LEQ()
		: base("std::<= (std::u16)", Kernel.T_U16U16, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		ushort b = cpu.Pop();
		ushort a = cpu.Pop();
		cpu.Push(a <= b);
	}
}

class NativeU16GT : FunctionNative {

	internal NativeU16GT()
		: base("std::> (std::u16)", Kernel.T_U16U16, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		ushort b = cpu.Pop();
		ushort a = cpu.Pop();
		cpu.Push(a > b);
	}
}

class NativeU16GEQ : FunctionNative {

	internal NativeU16GEQ()
		: base("std::>= (std::u16)", Kernel.T_U16U16, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		ushort b = cpu.Pop();
		ushort a = cpu.Pop();
		cpu.Push(a >= b);
	}
}

class NativeU16MulX : FunctionNative {

	internal NativeU16MulX()
		: base("std::*x (std::u16)", Kernel.T_U16U16, Kernel.T_U32)
	{
	}

	internal override void Run(CPU cpu)
	{
		ushort b = cpu.Pop();
		ushort a = cpu.Pop();
		cpu.Push((uint)((uint)a * (uint)b));
	}
}

class NativeU16MulH : FunctionNative {

	internal NativeU16MulH()
		: base("std::*h (std::u16)", Kernel.T_U16U16, Kernel.T_U16)
	{
	}

	internal override void Run(CPU cpu)
	{
		ushort b = cpu.Pop();
		ushort a = cpu.Pop();
		uint r = (uint)((uint)a * (uint)b);
		cpu.Push((ushort)(r >> 16));
	}
}

class NativeU16MulHL : FunctionNative {

	internal NativeU16MulHL()
		: base("std::*hl (std::u16)", Kernel.T_U16U16, Kernel.T_U16U16)
	{
	}

	internal override void Run(CPU cpu)
	{
		ushort b = cpu.Pop();
		ushort a = cpu.Pop();
		uint r = (uint)((uint)a * (uint)b);
		cpu.Push((ushort)(r >> 16));
		cpu.Push((ushort)r);
	}
}

class NativeU16ToU8 : FunctionNative {

	internal NativeU16ToU8()
		: base("std::^u8 (std::u16)", Kernel.T_U16, Kernel.T_U8)
	{
	}

	internal override void Run(CPU cpu)
	{
		ushort a = cpu.Pop();
		cpu.Push((byte)a);
	}
}

class NativeU16ToU16 : FunctionNative {

	internal NativeU16ToU16()
		: base("std::^u16 (std::u16)", Kernel.T_U16, Kernel.T_U16)
	{
	}

	internal override void Run(CPU cpu)
	{
		ushort a = cpu.Pop();
		cpu.Push((ushort)a);
	}
}

class NativeU16ToU32 : FunctionNative {

	internal NativeU16ToU32()
		: base("std::^u32 (std::u16)", Kernel.T_U16, Kernel.T_U32)
	{
	}

	internal override void Run(CPU cpu)
	{
		ushort a = cpu.Pop();
		cpu.Push((uint)a);
	}
}

class NativeU16ToU64 : FunctionNative {

	internal NativeU16ToU64()
		: base("std::^u64 (std::u16)", Kernel.T_U16, Kernel.T_U64)
	{
	}

	internal override void Run(CPU cpu)
	{
		ushort a = cpu.Pop();
		cpu.Push((ulong)a);
	}
}

class NativeU16ToI8 : FunctionNative {

	internal NativeU16ToI8()
		: base("std::^i8 (std::u16)", Kernel.T_U16, Kernel.T_I8)
	{
	}

	internal override void Run(CPU cpu)
	{
		ushort a = cpu.Pop();
		cpu.Push((sbyte)a);
	}
}

class NativeU16ToI16 : FunctionNative {

	internal NativeU16ToI16()
		: base("std::^i16 (std::u16)", Kernel.T_U16, Kernel.T_I16)
	{
	}

	internal override void Run(CPU cpu)
	{
		ushort a = cpu.Pop();
		cpu.Push((short)a);
	}
}

class NativeU16ToI32 : FunctionNative {

	internal NativeU16ToI32()
		: base("std::^i32 (std::u16)", Kernel.T_U16, Kernel.T_I32)
	{
	}

	internal override void Run(CPU cpu)
	{
		ushort a = cpu.Pop();
		cpu.Push((int)a);
	}
}

class NativeU16ToI64 : FunctionNative {

	internal NativeU16ToI64()
		: base("std::^i64 (std::u16)", Kernel.T_U16, Kernel.T_I64)
	{
	}

	internal override void Run(CPU cpu)
	{
		ushort a = cpu.Pop();
		cpu.Push((long)a);
	}
}

class NativeU16ToInt : FunctionNative {

	internal NativeU16ToInt()
		: base("std::^int (std::u16)", Kernel.T_U16, Kernel.T_Int)
	{
	}

	internal override void Run(CPU cpu)
	{
		ushort a = cpu.Pop();
		cpu.Push(XValue.MakeInt((int)a));
	}
}

/* ======================================================================= */
/* std::u32 */

class NativeU32Add : FunctionNative {

	internal NativeU32Add()
		: base("std::+ (std::u32)", Kernel.T_U32U32, Kernel.T_U32)
	{
	}

	internal override void Run(CPU cpu)
	{
		uint b = cpu.Pop();
		uint a = cpu.Pop();
		cpu.Push((uint)(a + b));
	}
}

class NativeU32Sub : FunctionNative {

	internal NativeU32Sub()
		: base("std::- (std::u32)", Kernel.T_U32U32, Kernel.T_U32)
	{
	}

	internal override void Run(CPU cpu)
	{
		uint b = cpu.Pop();
		uint a = cpu.Pop();
		cpu.Push((uint)(a - b));
	}
}

class NativeU32Mul : FunctionNative {

	internal NativeU32Mul()
		: base("std::* (std::u32)", Kernel.T_U32U32, Kernel.T_U32)
	{
	}

	internal override void Run(CPU cpu)
	{
		uint b = cpu.Pop();
		uint a = cpu.Pop();
		cpu.Push((uint)(a * b));
	}
}

class NativeU32Div : FunctionNative {

	internal NativeU32Div()
		: base("std::/ (std::u32)", Kernel.T_U32U32, Kernel.T_U32)
	{
	}

	internal override void Run(CPU cpu)
	{
		uint b = cpu.Pop();
		uint a = cpu.Pop();
		if (b == 0) {
			throw new Exception("division by zero");
		}
		cpu.Push((uint)(a / b));
	}
}

class NativeU32Mod : FunctionNative {

	internal NativeU32Mod()
		: base("std::% (std::u32)", Kernel.T_U32U32, Kernel.T_U32)
	{
	}

	internal override void Run(CPU cpu)
	{
		uint b = cpu.Pop();
		uint a = cpu.Pop();
		if (b == 0) {
			throw new Exception("division by zero");
		}
		cpu.Push((uint)(a % b));
	}
}

class NativeU32DivMod : FunctionNative {

	internal NativeU32DivMod()
		: base("std::/% (std::u32)", Kernel.T_U32U32, Kernel.T_U32U32)
	{
	}

	internal override void Run(CPU cpu)
	{
		uint b = cpu.Pop();
		uint a = cpu.Pop();
		if (b == 0) {
			throw new Exception("division by zero");
		}
		cpu.Push((uint)(a / b));
		cpu.Push((uint)(a % b));
	}
}

class NativeU32Neg : FunctionNative {

	internal NativeU32Neg()
		: base("std::neg (std::u32)", Kernel.T_U32, Kernel.T_U32)
	{
	}

	internal override void Run(CPU cpu)
	{
		uint a = cpu.Pop();
		cpu.Push((uint)-(long)a);
	}
}

class NativeU32Inc : FunctionNative {

	internal NativeU32Inc()
		: base("std::++ (std::u32)", Kernel.T_U32, Kernel.T_U32)
	{
	}

	internal override void Run(CPU cpu)
	{
		uint a = cpu.Pop();
		cpu.Push((uint)(a + 1));
	}
}

class NativeU32Dec : FunctionNative {

	internal NativeU32Dec()
		: base("std::-- (std::u32)", Kernel.T_U32, Kernel.T_U32)
	{
	}

	internal override void Run(CPU cpu)
	{
		uint a = cpu.Pop();
		cpu.Push((uint)(a - 1));
	}
}

class NativeU32And : FunctionNative {

	internal NativeU32And()
		: base("std::and (std::u32)", Kernel.T_U32U32, Kernel.T_U32)
	{
	}

	internal override void Run(CPU cpu)
	{
		uint b = cpu.Pop();
		uint a = cpu.Pop();
		cpu.Push((uint)(a & b));
	}
}

class NativeU32Or : FunctionNative {

	internal NativeU32Or()
		: base("std::or (std::u32)", Kernel.T_U32U32, Kernel.T_U32)
	{
	}

	internal override void Run(CPU cpu)
	{
		uint b = cpu.Pop();
		uint a = cpu.Pop();
		cpu.Push((uint)(a | b));
	}
}

class NativeU32Xor : FunctionNative {

	internal NativeU32Xor()
		: base("std::xor (std::u32)", Kernel.T_U32U32, Kernel.T_U32)
	{
	}

	internal override void Run(CPU cpu)
	{
		uint b = cpu.Pop();
		uint a = cpu.Pop();
		cpu.Push((uint)(a ^ b));
	}
}

class NativeU32Eqv : FunctionNative {

	internal NativeU32Eqv()
		: base("std::eqv (std::u32)", Kernel.T_U32U32, Kernel.T_U32)
	{
	}

	internal override void Run(CPU cpu)
	{
		uint b = cpu.Pop();
		uint a = cpu.Pop();
		cpu.Push((uint)~(a ^ b));
	}
}

class NativeU32Not : FunctionNative {

	internal NativeU32Not()
		: base("std::not (std::u32)", Kernel.T_U32, Kernel.T_U32)
	{
	}

	internal override void Run(CPU cpu)
	{
		uint a = cpu.Pop();
		cpu.Push((uint)~a);
	}
}

class NativeU32LT : FunctionNative {

	internal NativeU32LT()
		: base("std::< (std::u32)", Kernel.T_U32U32, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		uint b = cpu.Pop();
		uint a = cpu.Pop();
		cpu.Push(a < b);
	}
}

class NativeU32LEQ : FunctionNative {

	internal NativeU32LEQ()
		: base("std::<= (std::u32)", Kernel.T_U32U32, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		uint b = cpu.Pop();
		uint a = cpu.Pop();
		cpu.Push(a <= b);
	}
}

class NativeU32GT : FunctionNative {

	internal NativeU32GT()
		: base("std::> (std::u32)", Kernel.T_U32U32, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		uint b = cpu.Pop();
		uint a = cpu.Pop();
		cpu.Push(a > b);
	}
}

class NativeU32GEQ : FunctionNative {

	internal NativeU32GEQ()
		: base("std::>= (std::u32)", Kernel.T_U32U32, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		uint b = cpu.Pop();
		uint a = cpu.Pop();
		cpu.Push(a >= b);
	}
}

class NativeU32MulX : FunctionNative {

	internal NativeU32MulX()
		: base("std::*x (std::u32)", Kernel.T_U32U32, Kernel.T_U64)
	{
	}

	internal override void Run(CPU cpu)
	{
		uint b = cpu.Pop();
		uint a = cpu.Pop();
		cpu.Push((ulong)((ulong)a * (ulong)b));
	}
}

class NativeU32MulH : FunctionNative {

	internal NativeU32MulH()
		: base("std::*h (std::u32)", Kernel.T_U32U32, Kernel.T_U32)
	{
	}

	internal override void Run(CPU cpu)
	{
		uint b = cpu.Pop();
		uint a = cpu.Pop();
		ulong r = (ulong)((ulong)a * (ulong)b);
		cpu.Push((uint)(r >> 32));
	}
}

class NativeU32MulHL : FunctionNative {

	internal NativeU32MulHL()
		: base("std::*hl (std::u32)", Kernel.T_U32U32, Kernel.T_U32U32)
	{
	}

	internal override void Run(CPU cpu)
	{
		uint b = cpu.Pop();
		uint a = cpu.Pop();
		ulong r = (ulong)((ulong)a * (ulong)b);
		cpu.Push((uint)(r >> 32));
		cpu.Push((uint)r);
	}
}

class NativeU32ToU8 : FunctionNative {

	internal NativeU32ToU8()
		: base("std::^u8 (std::u32)", Kernel.T_U32, Kernel.T_U8)
	{
	}

	internal override void Run(CPU cpu)
	{
		uint a = cpu.Pop();
		cpu.Push((byte)a);
	}
}

class NativeU32ToU16 : FunctionNative {

	internal NativeU32ToU16()
		: base("std::^u16 (std::u32)", Kernel.T_U32, Kernel.T_U16)
	{
	}

	internal override void Run(CPU cpu)
	{
		uint a = cpu.Pop();
		cpu.Push((ushort)a);
	}
}

class NativeU32ToU32 : FunctionNative {

	internal NativeU32ToU32()
		: base("std::^u32 (std::u32)", Kernel.T_U32, Kernel.T_U32)
	{
	}

	internal override void Run(CPU cpu)
	{
		uint a = cpu.Pop();
		cpu.Push((uint)a);
	}
}

class NativeU32ToU64 : FunctionNative {

	internal NativeU32ToU64()
		: base("std::^u64 (std::u32)", Kernel.T_U32, Kernel.T_U64)
	{
	}

	internal override void Run(CPU cpu)
	{
		uint a = cpu.Pop();
		cpu.Push((ulong)a);
	}
}

class NativeU32ToI8 : FunctionNative {

	internal NativeU32ToI8()
		: base("std::^i8 (std::u32)", Kernel.T_U32, Kernel.T_I8)
	{
	}

	internal override void Run(CPU cpu)
	{
		uint a = cpu.Pop();
		cpu.Push((sbyte)a);
	}
}

class NativeU32ToI16 : FunctionNative {

	internal NativeU32ToI16()
		: base("std::^i16 (std::u32)", Kernel.T_U32, Kernel.T_I16)
	{
	}

	internal override void Run(CPU cpu)
	{
		uint a = cpu.Pop();
		cpu.Push((short)a);
	}
}

class NativeU32ToI32 : FunctionNative {

	internal NativeU32ToI32()
		: base("std::^i32 (std::u32)", Kernel.T_U32, Kernel.T_I32)
	{
	}

	internal override void Run(CPU cpu)
	{
		uint a = cpu.Pop();
		cpu.Push((int)a);
	}
}

class NativeU32ToI64 : FunctionNative {

	internal NativeU32ToI64()
		: base("std::^i64 (std::u32)", Kernel.T_U32, Kernel.T_I64)
	{
	}

	internal override void Run(CPU cpu)
	{
		uint a = cpu.Pop();
		cpu.Push((long)a);
	}
}

class NativeU32ToInt : FunctionNative {

	internal NativeU32ToInt()
		: base("std::^int (std::u32)", Kernel.T_U32, Kernel.T_Int)
	{
	}

	internal override void Run(CPU cpu)
	{
		uint a = cpu.Pop();
		if (a > (uint)Int32.MaxValue) {
			throw new Exception("overflow in u32 -> int conversion");
		}
		cpu.Push(XValue.MakeInt((int)a));
	}
}

/* ======================================================================= */
/* std::u64 */

class NativeU64Add : FunctionNative {

	internal NativeU64Add()
		: base("std::+ (std::u64)", Kernel.T_U64U64, Kernel.T_U64)
	{
	}

	internal override void Run(CPU cpu)
	{
		ulong b = cpu.Pop();
		ulong a = cpu.Pop();
		cpu.Push((ulong)(a + b));
	}
}

class NativeU64Sub : FunctionNative {

	internal NativeU64Sub()
		: base("std::- (std::u64)", Kernel.T_U64U64, Kernel.T_U64)
	{
	}

	internal override void Run(CPU cpu)
	{
		ulong b = cpu.Pop();
		ulong a = cpu.Pop();
		cpu.Push((ulong)(a - b));
	}
}

class NativeU64Mul : FunctionNative {

	internal NativeU64Mul()
		: base("std::* (std::u64)", Kernel.T_U64U64, Kernel.T_U64)
	{
	}

	internal override void Run(CPU cpu)
	{
		ulong b = cpu.Pop();
		ulong a = cpu.Pop();
		cpu.Push((ulong)(a * b));
	}
}

class NativeU64Div : FunctionNative {

	internal NativeU64Div()
		: base("std::/ (std::u64)", Kernel.T_U64U64, Kernel.T_U64)
	{
	}

	internal override void Run(CPU cpu)
	{
		ulong b = cpu.Pop();
		ulong a = cpu.Pop();
		if (b == 0) {
			throw new Exception("division by zero");
		}
		cpu.Push((ulong)(a / b));
	}
}

class NativeU64Mod : FunctionNative {

	internal NativeU64Mod()
		: base("std::% (std::u64)", Kernel.T_U64U64, Kernel.T_U64)
	{
	}

	internal override void Run(CPU cpu)
	{
		ulong b = cpu.Pop();
		ulong a = cpu.Pop();
		if (b == 0) {
			throw new Exception("division by zero");
		}
		cpu.Push((ulong)(a % b));
	}
}

class NativeU64DivMod : FunctionNative {

	internal NativeU64DivMod()
		: base("std::/% (std::u64)", Kernel.T_U64U64, Kernel.T_U64U64)
	{
	}

	internal override void Run(CPU cpu)
	{
		ulong b = cpu.Pop();
		ulong a = cpu.Pop();
		if (b == 0) {
			throw new Exception("division by zero");
		}
		cpu.Push((ulong)(a / b));
		cpu.Push((ulong)(a % b));
	}
}

class NativeU64Neg : FunctionNative {

	internal NativeU64Neg()
		: base("std::neg (std::u64)", Kernel.T_U64, Kernel.T_U64)
	{
	}

	internal override void Run(CPU cpu)
	{
		ulong a = cpu.Pop();
		cpu.Push((ulong)-(long)a);
	}
}

class NativeU64Inc : FunctionNative {

	internal NativeU64Inc()
		: base("std::++ (std::u64)", Kernel.T_U64, Kernel.T_U64)
	{
	}

	internal override void Run(CPU cpu)
	{
		ulong a = cpu.Pop();
		cpu.Push((ulong)(a + 1));
	}
}

class NativeU64Dec : FunctionNative {

	internal NativeU64Dec()
		: base("std::-- (std::u64)", Kernel.T_U64, Kernel.T_U64)
	{
	}

	internal override void Run(CPU cpu)
	{
		ulong a = cpu.Pop();
		cpu.Push((ulong)(a - 1));
	}
}

class NativeU64And : FunctionNative {

	internal NativeU64And()
		: base("std::and (std::u64)", Kernel.T_U64U64, Kernel.T_U64)
	{
	}

	internal override void Run(CPU cpu)
	{
		ulong b = cpu.Pop();
		ulong a = cpu.Pop();
		cpu.Push((ulong)(a & b));
	}
}

class NativeU64Or : FunctionNative {

	internal NativeU64Or()
		: base("std::or (std::u64)", Kernel.T_U64U64, Kernel.T_U64)
	{
	}

	internal override void Run(CPU cpu)
	{
		ulong b = cpu.Pop();
		ulong a = cpu.Pop();
		cpu.Push((ulong)(a | b));
	}
}

class NativeU64Xor : FunctionNative {

	internal NativeU64Xor()
		: base("std::xor (std::u64)", Kernel.T_U64U64, Kernel.T_U64)
	{
	}

	internal override void Run(CPU cpu)
	{
		ulong b = cpu.Pop();
		ulong a = cpu.Pop();
		cpu.Push((ulong)(a ^ b));
	}
}

class NativeU64Eqv : FunctionNative {

	internal NativeU64Eqv()
		: base("std::eqv (std::u64)", Kernel.T_U64U64, Kernel.T_U64)
	{
	}

	internal override void Run(CPU cpu)
	{
		ulong b = cpu.Pop();
		ulong a = cpu.Pop();
		cpu.Push((ulong)~(a ^ b));
	}
}

class NativeU64Not : FunctionNative {

	internal NativeU64Not()
		: base("std::not (std::u64)", Kernel.T_U64, Kernel.T_U64)
	{
	}

	internal override void Run(CPU cpu)
	{
		ulong a = cpu.Pop();
		cpu.Push((ulong)~a);
	}
}

class NativeU64LT : FunctionNative {

	internal NativeU64LT()
		: base("std::< (std::u64)", Kernel.T_U64U64, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		ulong b = cpu.Pop();
		ulong a = cpu.Pop();
		cpu.Push(a < b);
	}
}

class NativeU64LEQ : FunctionNative {

	internal NativeU64LEQ()
		: base("std::<= (std::u64)", Kernel.T_U64U64, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		ulong b = cpu.Pop();
		ulong a = cpu.Pop();
		cpu.Push(a <= b);
	}
}

class NativeU64GT : FunctionNative {

	internal NativeU64GT()
		: base("std::> (std::u64)", Kernel.T_U64U64, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		ulong b = cpu.Pop();
		ulong a = cpu.Pop();
		cpu.Push(a > b);
	}
}

class NativeU64GEQ : FunctionNative {

	internal NativeU64GEQ()
		: base("std::>= (std::u64)", Kernel.T_U64U64, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		ulong b = cpu.Pop();
		ulong a = cpu.Pop();
		cpu.Push(a >= b);
	}
}

class NativeU64MulH : FunctionNative {

	internal NativeU64MulH()
		: base("std::*h (std::u64)", Kernel.T_U64U64, Kernel.T_U64)
	{
	}

	internal override void Run(CPU cpu)
	{
		ulong b = cpu.Pop();
		ulong a = cpu.Pop();
		cpu.Push(Kernel.Umulh64(a, b));
	}
}

class NativeU64MulHL : FunctionNative {

	internal NativeU64MulHL()
		: base("std::*hl (std::u64)", Kernel.T_U64U64, Kernel.T_U64U64)
	{
	}

	internal override void Run(CPU cpu)
	{
		ulong b = cpu.Pop();
		ulong a = cpu.Pop();
		cpu.Push(Kernel.Umulh64(a, b));
		cpu.Push((ulong)(a * b));
	}
}

class NativeU64ToU8 : FunctionNative {

	internal NativeU64ToU8()
		: base("std::^u8 (std::u64)", Kernel.T_U64, Kernel.T_U8)
	{
	}

	internal override void Run(CPU cpu)
	{
		ulong a = cpu.Pop();
		cpu.Push((byte)a);
	}
}

class NativeU64ToU16 : FunctionNative {

	internal NativeU64ToU16()
		: base("std::^u16 (std::u64)", Kernel.T_U64, Kernel.T_U16)
	{
	}

	internal override void Run(CPU cpu)
	{
		ulong a = cpu.Pop();
		cpu.Push((ushort)a);
	}
}

class NativeU64ToU32 : FunctionNative {

	internal NativeU64ToU32()
		: base("std::^u32 (std::u64)", Kernel.T_U64, Kernel.T_U32)
	{
	}

	internal override void Run(CPU cpu)
	{
		ulong a = cpu.Pop();
		cpu.Push((uint)a);
	}
}

class NativeU64ToU64 : FunctionNative {

	internal NativeU64ToU64()
		: base("std::^u64 (std::u64)", Kernel.T_U64, Kernel.T_U64)
	{
	}

	internal override void Run(CPU cpu)
	{
		ulong a = cpu.Pop();
		cpu.Push((ulong)a);
	}
}

class NativeU64ToI8 : FunctionNative {

	internal NativeU64ToI8()
		: base("std::^i8 (std::u64)", Kernel.T_U64, Kernel.T_I8)
	{
	}

	internal override void Run(CPU cpu)
	{
		ulong a = cpu.Pop();
		cpu.Push((sbyte)a);
	}
}

class NativeU64ToI16 : FunctionNative {

	internal NativeU64ToI16()
		: base("std::^i16 (std::u64)", Kernel.T_U64, Kernel.T_I16)
	{
	}

	internal override void Run(CPU cpu)
	{
		ulong a = cpu.Pop();
		cpu.Push((short)a);
	}
}

class NativeU64ToI32 : FunctionNative {

	internal NativeU64ToI32()
		: base("std::^i32 (std::u64)", Kernel.T_U64, Kernel.T_I32)
	{
	}

	internal override void Run(CPU cpu)
	{
		ulong a = cpu.Pop();
		cpu.Push((int)a);
	}
}

class NativeU64ToI64 : FunctionNative {

	internal NativeU64ToI64()
		: base("std::^i64 (std::u64)", Kernel.T_U64, Kernel.T_I64)
	{
	}

	internal override void Run(CPU cpu)
	{
		ulong a = cpu.Pop();
		cpu.Push((long)a);
	}
}

class NativeU64ToInt : FunctionNative {

	internal NativeU64ToInt()
		: base("std::^int (std::u64)", Kernel.T_U64, Kernel.T_Int)
	{
	}

	internal override void Run(CPU cpu)
	{
		ulong a = cpu.Pop();
		if (a > (ulong)Int32.MaxValue) {
			throw new Exception("overflow in u64 -> int conversion");
		}
		cpu.Push(XValue.MakeInt((int)a));
	}
}

/* ======================================================================= */
/* std::i8 */

class NativeI8Add : FunctionNative {

	internal NativeI8Add()
		: base("std::+ (std::i8)", Kernel.T_I8I8, Kernel.T_I8)
	{
	}

	internal override void Run(CPU cpu)
	{
		sbyte b = cpu.Pop();
		sbyte a = cpu.Pop();
		cpu.Push((sbyte)(a + b));
	}
}

class NativeI8Sub : FunctionNative {

	internal NativeI8Sub()
		: base("std::- (std::i8)", Kernel.T_I8I8, Kernel.T_I8)
	{
	}

	internal override void Run(CPU cpu)
	{
		sbyte b = cpu.Pop();
		sbyte a = cpu.Pop();
		cpu.Push((sbyte)(a - b));
	}
}

class NativeI8Mul : FunctionNative {

	internal NativeI8Mul()
		: base("std::* (std::i8)", Kernel.T_I8I8, Kernel.T_I8)
	{
	}

	internal override void Run(CPU cpu)
	{
		sbyte b = cpu.Pop();
		sbyte a = cpu.Pop();
		cpu.Push((sbyte)(a * b));
	}
}

class NativeI8Div : FunctionNative {

	internal NativeI8Div()
		: base("std::/ (std::i8)", Kernel.T_I8I8, Kernel.T_I8)
	{
	}

	internal override void Run(CPU cpu)
	{
		sbyte b = cpu.Pop();
		sbyte a = cpu.Pop();
		if (b == 0) {
			throw new Exception("division by zero");
		}
		if (b == -1 && a == SByte.MinValue) {
			throw new Exception("division overflow");
		}
		cpu.Push((sbyte)(a / b));
	}
}

class NativeI8Mod : FunctionNative {

	internal NativeI8Mod()
		: base("std::% (std::i8)", Kernel.T_I8I8, Kernel.T_I8)
	{
	}

	internal override void Run(CPU cpu)
	{
		sbyte b = cpu.Pop();
		sbyte a = cpu.Pop();
		if (b == 0) {
			throw new Exception("division by zero");
		}
		if (b == -1 && a == SByte.MinValue) {
			throw new Exception("division overflow");
		}
		cpu.Push((sbyte)(a % b));
	}
}

class NativeI8DivMod : FunctionNative {

	internal NativeI8DivMod()
		: base("std::/% (std::i8)", Kernel.T_I8I8, Kernel.T_I8I8)
	{
	}

	internal override void Run(CPU cpu)
	{
		sbyte b = cpu.Pop();
		sbyte a = cpu.Pop();
		if (b == 0) {
			throw new Exception("division by zero");
		}
		if (b == -1 && a == SByte.MinValue) {
			throw new Exception("division overflow");
		}
		cpu.Push((sbyte)(a / b));
		cpu.Push((sbyte)(a % b));
	}
}

class NativeI8Neg : FunctionNative {

	internal NativeI8Neg()
		: base("std::neg (std::i8)", Kernel.T_I8, Kernel.T_I8)
	{
	}

	internal override void Run(CPU cpu)
	{
		sbyte a = cpu.Pop();
		cpu.Push((sbyte)-a);
	}
}

class NativeI8Inc : FunctionNative {

	internal NativeI8Inc()
		: base("std::++ (std::i8)", Kernel.T_I8, Kernel.T_I8)
	{
	}

	internal override void Run(CPU cpu)
	{
		sbyte a = cpu.Pop();
		cpu.Push((sbyte)(a + 1));
	}
}

class NativeI8Dec : FunctionNative {

	internal NativeI8Dec()
		: base("std::-- (std::i8)", Kernel.T_I8, Kernel.T_I8)
	{
	}

	internal override void Run(CPU cpu)
	{
		sbyte a = cpu.Pop();
		cpu.Push((sbyte)(a - 1));
	}
}

class NativeI8And : FunctionNative {

	internal NativeI8And()
		: base("std::and (std::i8)", Kernel.T_I8I8, Kernel.T_I8)
	{
	}

	internal override void Run(CPU cpu)
	{
		sbyte b = cpu.Pop();
		sbyte a = cpu.Pop();
		cpu.Push((sbyte)((ulong)a & (ulong)b));
	}
}

class NativeI8Or : FunctionNative {

	internal NativeI8Or()
		: base("std::or (std::i8)", Kernel.T_I8I8, Kernel.T_I8)
	{
	}

	internal override void Run(CPU cpu)
	{
		sbyte b = cpu.Pop();
		sbyte a = cpu.Pop();
		cpu.Push((sbyte)((ulong)a | (ulong)b));
	}
}

class NativeI8Xor : FunctionNative {

	internal NativeI8Xor()
		: base("std::xor (std::i8)", Kernel.T_I8I8, Kernel.T_I8)
	{
	}

	internal override void Run(CPU cpu)
	{
		sbyte b = cpu.Pop();
		sbyte a = cpu.Pop();
		cpu.Push((sbyte)((ulong)a ^ (ulong)b));
	}
}

class NativeI8Eqv : FunctionNative {

	internal NativeI8Eqv()
		: base("std::eqv (std::i8)", Kernel.T_I8I8, Kernel.T_I8)
	{
	}

	internal override void Run(CPU cpu)
	{
		sbyte b = cpu.Pop();
		sbyte a = cpu.Pop();
		cpu.Push((sbyte)~((ulong)a ^ (ulong)b));
	}
}

class NativeI8Not : FunctionNative {

	internal NativeI8Not()
		: base("std::not (std::i8)", Kernel.T_I8, Kernel.T_I8)
	{
	}

	internal override void Run(CPU cpu)
	{
		sbyte a = cpu.Pop();
		cpu.Push((sbyte)~(ulong)a);
	}
}

class NativeI8LT : FunctionNative {

	internal NativeI8LT()
		: base("std::< (std::i8)", Kernel.T_I8I8, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		sbyte b = cpu.Pop();
		sbyte a = cpu.Pop();
		cpu.Push(a < b);
	}
}

class NativeI8LEQ : FunctionNative {

	internal NativeI8LEQ()
		: base("std::<= (std::i8)", Kernel.T_I8I8, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		sbyte b = cpu.Pop();
		sbyte a = cpu.Pop();
		cpu.Push(a <= b);
	}
}

class NativeI8GT : FunctionNative {

	internal NativeI8GT()
		: base("std::> (std::i8)", Kernel.T_I8I8, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		sbyte b = cpu.Pop();
		sbyte a = cpu.Pop();
		cpu.Push(a > b);
	}
}

class NativeI8GEQ : FunctionNative {

	internal NativeI8GEQ()
		: base("std::>= (std::i8)", Kernel.T_I8I8, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		sbyte b = cpu.Pop();
		sbyte a = cpu.Pop();
		cpu.Push(a >= b);
	}
}

class NativeI8MulX : FunctionNative {

	internal NativeI8MulX()
		: base("std::*x (std::i8)", Kernel.T_I8I8, Kernel.T_I16)
	{
	}

	internal override void Run(CPU cpu)
	{
		sbyte b = cpu.Pop();
		sbyte a = cpu.Pop();
		cpu.Push((short)((short)a * (short)b));
	}
}

class NativeI8MulH : FunctionNative {

	internal NativeI8MulH()
		: base("std::*h (std::i8)", Kernel.T_I8I8, Kernel.T_I8)
	{
	}

	internal override void Run(CPU cpu)
	{
		sbyte b = cpu.Pop();
		sbyte a = cpu.Pop();
		short r = (short)((short)a * (short)b);
		cpu.Push((sbyte)(r >> 8));
	}
}

class NativeI8MulHL : FunctionNative {

	internal NativeI8MulHL()
		: base("std::*hl (std::i8)", Kernel.T_I8I8, Kernel.T_I8U8)
	{
	}

	internal override void Run(CPU cpu)
	{
		sbyte b = cpu.Pop();
		sbyte a = cpu.Pop();
		short r = (short)((short)a * (short)b);
		cpu.Push((sbyte)(r >> 8));
		cpu.Push((byte)r);
	}
}

class NativeI8ToU8 : FunctionNative {

	internal NativeI8ToU8()
		: base("std::^u8 (std::i8)", Kernel.T_I8, Kernel.T_U8)
	{
	}

	internal override void Run(CPU cpu)
	{
		sbyte a = cpu.Pop();
		cpu.Push((byte)a);
	}
}

class NativeI8ToU16 : FunctionNative {

	internal NativeI8ToU16()
		: base("std::^u16 (std::i8)", Kernel.T_I8, Kernel.T_U16)
	{
	}

	internal override void Run(CPU cpu)
	{
		sbyte a = cpu.Pop();
		cpu.Push((ushort)a);
	}
}

class NativeI8ToU32 : FunctionNative {

	internal NativeI8ToU32()
		: base("std::^u32 (std::i8)", Kernel.T_I8, Kernel.T_U32)
	{
	}

	internal override void Run(CPU cpu)
	{
		sbyte a = cpu.Pop();
		cpu.Push((uint)a);
	}
}

class NativeI8ToU64 : FunctionNative {

	internal NativeI8ToU64()
		: base("std::^u64 (std::i8)", Kernel.T_I8, Kernel.T_U64)
	{
	}

	internal override void Run(CPU cpu)
	{
		sbyte a = cpu.Pop();
		cpu.Push((ulong)a);
	}
}

class NativeI8ToI8 : FunctionNative {

	internal NativeI8ToI8()
		: base("std::^i8 (std::i8)", Kernel.T_I8, Kernel.T_I8)
	{
	}

	internal override void Run(CPU cpu)
	{
		sbyte a = cpu.Pop();
		cpu.Push((sbyte)a);
	}
}

class NativeI8ToI16 : FunctionNative {

	internal NativeI8ToI16()
		: base("std::^i16 (std::i8)", Kernel.T_I8, Kernel.T_I16)
	{
	}

	internal override void Run(CPU cpu)
	{
		sbyte a = cpu.Pop();
		cpu.Push((short)a);
	}
}

class NativeI8ToI32 : FunctionNative {

	internal NativeI8ToI32()
		: base("std::^i32 (std::i8)", Kernel.T_I8, Kernel.T_I32)
	{
	}

	internal override void Run(CPU cpu)
	{
		sbyte a = cpu.Pop();
		cpu.Push((int)a);
	}
}

class NativeI8ToI64 : FunctionNative {

	internal NativeI8ToI64()
		: base("std::^i64 (std::i8)", Kernel.T_I8, Kernel.T_I64)
	{
	}

	internal override void Run(CPU cpu)
	{
		sbyte a = cpu.Pop();
		cpu.Push((long)a);
	}
}

class NativeI8ToInt : FunctionNative {

	internal NativeI8ToInt()
		: base("std::^int (std::i8)", Kernel.T_I8, Kernel.T_Int)
	{
	}

	internal override void Run(CPU cpu)
	{
		sbyte a = cpu.Pop();
		cpu.Push(XValue.MakeInt((int)a));
	}
}

/* ======================================================================= */
/* std::i16 */

class NativeI16Add : FunctionNative {

	internal NativeI16Add()
		: base("std::+ (std::i16)", Kernel.T_I16I16, Kernel.T_I16)
	{
	}

	internal override void Run(CPU cpu)
	{
		short b = cpu.Pop();
		short a = cpu.Pop();
		cpu.Push((short)(a + b));
	}
}

class NativeI16Sub : FunctionNative {

	internal NativeI16Sub()
		: base("std::- (std::i16)", Kernel.T_I16I16, Kernel.T_I16)
	{
	}

	internal override void Run(CPU cpu)
	{
		short b = cpu.Pop();
		short a = cpu.Pop();
		cpu.Push((short)(a - b));
	}
}

class NativeI16Mul : FunctionNative {

	internal NativeI16Mul()
		: base("std::* (std::i16)", Kernel.T_I16I16, Kernel.T_I16)
	{
	}

	internal override void Run(CPU cpu)
	{
		short b = cpu.Pop();
		short a = cpu.Pop();
		cpu.Push((short)(a * b));
	}
}

class NativeI16Div : FunctionNative {

	internal NativeI16Div()
		: base("std::/ (std::i16)", Kernel.T_I16I16, Kernel.T_I16)
	{
	}

	internal override void Run(CPU cpu)
	{
		short b = cpu.Pop();
		short a = cpu.Pop();
		if (b == 0) {
			throw new Exception("division by zero");
		}
		if (b == -1 && a == Int16.MinValue) {
			throw new Exception("division overflow");
		}
		cpu.Push((short)(a / b));
	}
}

class NativeI16Mod : FunctionNative {

	internal NativeI16Mod()
		: base("std::% (std::i16)", Kernel.T_I16I16, Kernel.T_I16)
	{
	}

	internal override void Run(CPU cpu)
	{
		short b = cpu.Pop();
		short a = cpu.Pop();
		if (b == 0) {
			throw new Exception("division by zero");
		}
		if (b == -1 && a == Int16.MinValue) {
			throw new Exception("division overflow");
		}
		cpu.Push((short)(a % b));
	}
}

class NativeI16DivMod : FunctionNative {

	internal NativeI16DivMod()
		: base("std::/% (std::i16)", Kernel.T_I16I16, Kernel.T_I16I16)
	{
	}

	internal override void Run(CPU cpu)
	{
		short b = cpu.Pop();
		short a = cpu.Pop();
		if (b == 0) {
			throw new Exception("division by zero");
		}
		if (b == -1 && a == Int16.MinValue) {
			throw new Exception("division overflow");
		}
		cpu.Push((short)(a / b));
		cpu.Push((short)(a % b));
	}
}

class NativeI16Neg : FunctionNative {

	internal NativeI16Neg()
		: base("std::neg (std::i16)", Kernel.T_I16, Kernel.T_I16)
	{
	}

	internal override void Run(CPU cpu)
	{
		short a = cpu.Pop();
		cpu.Push((short)-a);
	}
}

class NativeI16Inc : FunctionNative {

	internal NativeI16Inc()
		: base("std::++ (std::i16)", Kernel.T_I16, Kernel.T_I16)
	{
	}

	internal override void Run(CPU cpu)
	{
		short a = cpu.Pop();
		cpu.Push((short)(a + 1));
	}
}

class NativeI16Dec : FunctionNative {

	internal NativeI16Dec()
		: base("std::-- (std::i16)", Kernel.T_I16, Kernel.T_I16)
	{
	}

	internal override void Run(CPU cpu)
	{
		short a = cpu.Pop();
		cpu.Push((short)(a - 1));
	}
}

class NativeI16And : FunctionNative {

	internal NativeI16And()
		: base("std::and (std::i16)", Kernel.T_I16I16, Kernel.T_I16)
	{
	}

	internal override void Run(CPU cpu)
	{
		short b = cpu.Pop();
		short a = cpu.Pop();
		cpu.Push((short)((ulong)a & (ulong)b));
	}
}

class NativeI16Or : FunctionNative {

	internal NativeI16Or()
		: base("std::or (std::i16)", Kernel.T_I16I16, Kernel.T_I16)
	{
	}

	internal override void Run(CPU cpu)
	{
		short b = cpu.Pop();
		short a = cpu.Pop();
		cpu.Push((short)((ulong)a | (ulong)b));
	}
}

class NativeI16Xor : FunctionNative {

	internal NativeI16Xor()
		: base("std::xor (std::i16)", Kernel.T_I16I16, Kernel.T_I16)
	{
	}

	internal override void Run(CPU cpu)
	{
		short b = cpu.Pop();
		short a = cpu.Pop();
		cpu.Push((short)((ulong)a ^ (ulong)b));
	}
}

class NativeI16Eqv : FunctionNative {

	internal NativeI16Eqv()
		: base("std::eqv (std::i16)", Kernel.T_I16I16, Kernel.T_I16)
	{
	}

	internal override void Run(CPU cpu)
	{
		short b = cpu.Pop();
		short a = cpu.Pop();
		cpu.Push((short)~((ulong)a ^ (ulong)b));
	}
}

class NativeI16Not : FunctionNative {

	internal NativeI16Not()
		: base("std::not (std::i16)", Kernel.T_I16, Kernel.T_I16)
	{
	}

	internal override void Run(CPU cpu)
	{
		short a = cpu.Pop();
		cpu.Push((short)~(ulong)a);
	}
}

class NativeI16LT : FunctionNative {

	internal NativeI16LT()
		: base("std::< (std::i16)", Kernel.T_I16I16, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		short b = cpu.Pop();
		short a = cpu.Pop();
		cpu.Push(a < b);
	}
}

class NativeI16LEQ : FunctionNative {

	internal NativeI16LEQ()
		: base("std::<= (std::i16)", Kernel.T_I16I16, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		short b = cpu.Pop();
		short a = cpu.Pop();
		cpu.Push(a <= b);
	}
}

class NativeI16GT : FunctionNative {

	internal NativeI16GT()
		: base("std::> (std::i16)", Kernel.T_I16I16, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		short b = cpu.Pop();
		short a = cpu.Pop();
		cpu.Push(a > b);
	}
}

class NativeI16GEQ : FunctionNative {

	internal NativeI16GEQ()
		: base("std::>= (std::i16)", Kernel.T_I16I16, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		short b = cpu.Pop();
		short a = cpu.Pop();
		cpu.Push(a >= b);
	}
}

class NativeI16MulX : FunctionNative {

	internal NativeI16MulX()
		: base("std::*x (std::i16)", Kernel.T_I16I16, Kernel.T_I32)
	{
	}

	internal override void Run(CPU cpu)
	{
		short b = cpu.Pop();
		short a = cpu.Pop();
		cpu.Push((int)((int)a * (int)b));
	}
}

class NativeI16MulH : FunctionNative {

	internal NativeI16MulH()
		: base("std::*h (std::i16)", Kernel.T_I16I16, Kernel.T_I16)
	{
	}

	internal override void Run(CPU cpu)
	{
		short b = cpu.Pop();
		short a = cpu.Pop();
		int r = (int)((int)a * (int)b);
		cpu.Push((short)(r >> 16));
	}
}

class NativeI16MulHL : FunctionNative {

	internal NativeI16MulHL()
		: base("std::*hl (std::i16)", Kernel.T_I16I16, Kernel.T_I16U16)
	{
	}

	internal override void Run(CPU cpu)
	{
		short b = cpu.Pop();
		short a = cpu.Pop();
		int r = (int)((int)a * (int)b);
		cpu.Push((short)(r >> 16));
		cpu.Push((ushort)r);
	}
}

class NativeI16ToU8 : FunctionNative {

	internal NativeI16ToU8()
		: base("std::^u8 (std::i16)", Kernel.T_I16, Kernel.T_U8)
	{
	}

	internal override void Run(CPU cpu)
	{
		short a = cpu.Pop();
		cpu.Push((byte)a);
	}
}

class NativeI16ToU16 : FunctionNative {

	internal NativeI16ToU16()
		: base("std::^u16 (std::i16)", Kernel.T_I16, Kernel.T_U16)
	{
	}

	internal override void Run(CPU cpu)
	{
		short a = cpu.Pop();
		cpu.Push((ushort)a);
	}
}

class NativeI16ToU32 : FunctionNative {

	internal NativeI16ToU32()
		: base("std::^u32 (std::i16)", Kernel.T_I16, Kernel.T_U32)
	{
	}

	internal override void Run(CPU cpu)
	{
		short a = cpu.Pop();
		cpu.Push((uint)a);
	}
}

class NativeI16ToU64 : FunctionNative {

	internal NativeI16ToU64()
		: base("std::^u64 (std::i16)", Kernel.T_I16, Kernel.T_U64)
	{
	}

	internal override void Run(CPU cpu)
	{
		short a = cpu.Pop();
		cpu.Push((ulong)a);
	}
}

class NativeI16ToI8 : FunctionNative {

	internal NativeI16ToI8()
		: base("std::^i8 (std::i16)", Kernel.T_I16, Kernel.T_I8)
	{
	}

	internal override void Run(CPU cpu)
	{
		short a = cpu.Pop();
		cpu.Push((sbyte)a);
	}
}

class NativeI16ToI16 : FunctionNative {

	internal NativeI16ToI16()
		: base("std::^i16 (std::i16)", Kernel.T_I16, Kernel.T_I16)
	{
	}

	internal override void Run(CPU cpu)
	{
		short a = cpu.Pop();
		cpu.Push((short)a);
	}
}

class NativeI16ToI32 : FunctionNative {

	internal NativeI16ToI32()
		: base("std::^i32 (std::i16)", Kernel.T_I16, Kernel.T_I32)
	{
	}

	internal override void Run(CPU cpu)
	{
		short a = cpu.Pop();
		cpu.Push((int)a);
	}
}

class NativeI16ToI64 : FunctionNative {

	internal NativeI16ToI64()
		: base("std::^i64 (std::i16)", Kernel.T_I16, Kernel.T_I64)
	{
	}

	internal override void Run(CPU cpu)
	{
		short a = cpu.Pop();
		cpu.Push((long)a);
	}
}

class NativeI16ToInt : FunctionNative {

	internal NativeI16ToInt()
		: base("std::^int (std::i16)", Kernel.T_I16, Kernel.T_Int)
	{
	}

	internal override void Run(CPU cpu)
	{
		short a = cpu.Pop();
		cpu.Push(XValue.MakeInt((int)a));
	}
}

/* ======================================================================= */
/* std::i32 */

class NativeI32Add : FunctionNative {

	internal NativeI32Add()
		: base("std::+ (std::i32)", Kernel.T_I32I32, Kernel.T_I32)
	{
	}

	internal override void Run(CPU cpu)
	{
		int b = cpu.Pop();
		int a = cpu.Pop();
		cpu.Push((int)(a + b));
	}
}

class NativeI32Sub : FunctionNative {

	internal NativeI32Sub()
		: base("std::- (std::i32)", Kernel.T_I32I32, Kernel.T_I32)
	{
	}

	internal override void Run(CPU cpu)
	{
		int b = cpu.Pop();
		int a = cpu.Pop();
		cpu.Push((int)(a - b));
	}
}

class NativeI32Mul : FunctionNative {

	internal NativeI32Mul()
		: base("std::* (std::i32)", Kernel.T_I32I32, Kernel.T_I32)
	{
	}

	internal override void Run(CPU cpu)
	{
		int b = cpu.Pop();
		int a = cpu.Pop();
		cpu.Push((int)(a * b));
	}
}

class NativeI32Div : FunctionNative {

	internal NativeI32Div()
		: base("std::/ (std::i32)", Kernel.T_I32I32, Kernel.T_I32)
	{
	}

	internal override void Run(CPU cpu)
	{
		int b = cpu.Pop();
		int a = cpu.Pop();
		if (b == 0) {
			throw new Exception("division by zero");
		}
		if (b == -1 && a == Int32.MinValue) {
			throw new Exception("division overflow");
		}
		cpu.Push((int)(a / b));
	}
}

class NativeI32Mod : FunctionNative {

	internal NativeI32Mod()
		: base("std::% (std::i32)", Kernel.T_I32I32, Kernel.T_I32)
	{
	}

	internal override void Run(CPU cpu)
	{
		int b = cpu.Pop();
		int a = cpu.Pop();
		if (b == 0) {
			throw new Exception("division by zero");
		}
		if (b == -1 && a == Int32.MinValue) {
			throw new Exception("division overflow");
		}
		cpu.Push((int)(a % b));
	}
}

class NativeI32DivMod : FunctionNative {

	internal NativeI32DivMod()
		: base("std::/% (std::i32)", Kernel.T_I32I32, Kernel.T_I32I32)
	{
	}

	internal override void Run(CPU cpu)
	{
		int b = cpu.Pop();
		int a = cpu.Pop();
		if (b == 0) {
			throw new Exception("division by zero");
		}
		if (b == -1 && a == Int32.MinValue) {
			throw new Exception("division overflow");
		}
		cpu.Push((int)(a / b));
		cpu.Push((int)(a % b));
	}
}

class NativeI32Neg : FunctionNative {

	internal NativeI32Neg()
		: base("std::neg (std::i32)", Kernel.T_I32, Kernel.T_I32)
	{
	}

	internal override void Run(CPU cpu)
	{
		int a = cpu.Pop();
		cpu.Push((int)-a);
	}
}

class NativeI32Inc : FunctionNative {

	internal NativeI32Inc()
		: base("std::++ (std::i32)", Kernel.T_I32, Kernel.T_I32)
	{
	}

	internal override void Run(CPU cpu)
	{
		int a = cpu.Pop();
		cpu.Push((int)(a + 1));
	}
}

class NativeI32Dec : FunctionNative {

	internal NativeI32Dec()
		: base("std::-- (std::i32)", Kernel.T_I32, Kernel.T_I32)
	{
	}

	internal override void Run(CPU cpu)
	{
		int a = cpu.Pop();
		cpu.Push((int)(a - 1));
	}
}

class NativeI32And : FunctionNative {

	internal NativeI32And()
		: base("std::and (std::i32)", Kernel.T_I32I32, Kernel.T_I32)
	{
	}

	internal override void Run(CPU cpu)
	{
		int b = cpu.Pop();
		int a = cpu.Pop();
		cpu.Push((int)((ulong)a & (ulong)b));
	}
}

class NativeI32Or : FunctionNative {

	internal NativeI32Or()
		: base("std::or (std::i32)", Kernel.T_I32I32, Kernel.T_I32)
	{
	}

	internal override void Run(CPU cpu)
	{
		int b = cpu.Pop();
		int a = cpu.Pop();
		cpu.Push((int)((ulong)a | (ulong)b));
	}
}

class NativeI32Xor : FunctionNative {

	internal NativeI32Xor()
		: base("std::xor (std::i32)", Kernel.T_I32I32, Kernel.T_I32)
	{
	}

	internal override void Run(CPU cpu)
	{
		int b = cpu.Pop();
		int a = cpu.Pop();
		cpu.Push((int)((ulong)a ^ (ulong)b));
	}
}

class NativeI32Eqv : FunctionNative {

	internal NativeI32Eqv()
		: base("std::eqv (std::i32)", Kernel.T_I32I32, Kernel.T_I32)
	{
	}

	internal override void Run(CPU cpu)
	{
		int b = cpu.Pop();
		int a = cpu.Pop();
		cpu.Push((int)~((ulong)a ^ (ulong)b));
	}
}

class NativeI32Not : FunctionNative {

	internal NativeI32Not()
		: base("std::not (std::i32)", Kernel.T_I32, Kernel.T_I32)
	{
	}

	internal override void Run(CPU cpu)
	{
		int a = cpu.Pop();
		cpu.Push((int)~(ulong)a);
	}
}

class NativeI32LT : FunctionNative {

	internal NativeI32LT()
		: base("std::< (std::i32)", Kernel.T_I32I32, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		int b = cpu.Pop();
		int a = cpu.Pop();
		cpu.Push(a < b);
	}
}

class NativeI32LEQ : FunctionNative {

	internal NativeI32LEQ()
		: base("std::<= (std::i32)", Kernel.T_I32I32, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		int b = cpu.Pop();
		int a = cpu.Pop();
		cpu.Push(a <= b);
	}
}

class NativeI32GT : FunctionNative {

	internal NativeI32GT()
		: base("std::> (std::i32)", Kernel.T_I32I32, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		int b = cpu.Pop();
		int a = cpu.Pop();
		cpu.Push(a > b);
	}
}

class NativeI32GEQ : FunctionNative {

	internal NativeI32GEQ()
		: base("std::>= (std::i32)", Kernel.T_I32I32, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		int b = cpu.Pop();
		int a = cpu.Pop();
		cpu.Push(a >= b);
	}
}

class NativeI32MulX : FunctionNative {

	internal NativeI32MulX()
		: base("std::*x (std::i32)", Kernel.T_I32I32, Kernel.T_I64)
	{
	}

	internal override void Run(CPU cpu)
	{
		int b = cpu.Pop();
		int a = cpu.Pop();
		cpu.Push((long)((long)a * (long)b));
	}
}

class NativeI32MulH : FunctionNative {

	internal NativeI32MulH()
		: base("std::*h (std::i32)", Kernel.T_I32I32, Kernel.T_I32)
	{
	}

	internal override void Run(CPU cpu)
	{
		int b = cpu.Pop();
		int a = cpu.Pop();
		long r = (long)((long)a * (long)b);
		cpu.Push((int)(r >> 32));
	}
}

class NativeI32MulHL : FunctionNative {

	internal NativeI32MulHL()
		: base("std::*hl (std::i32)", Kernel.T_I32I32, Kernel.T_I32U32)
	{
	}

	internal override void Run(CPU cpu)
	{
		int b = cpu.Pop();
		int a = cpu.Pop();
		long r = (long)((long)a * (long)b);
		cpu.Push((int)(r >> 32));
		cpu.Push((uint)r);
	}
}

class NativeI32ToU8 : FunctionNative {

	internal NativeI32ToU8()
		: base("std::^u8 (std::i32)", Kernel.T_I32, Kernel.T_U8)
	{
	}

	internal override void Run(CPU cpu)
	{
		int a = cpu.Pop();
		cpu.Push((byte)a);
	}
}

class NativeI32ToU16 : FunctionNative {

	internal NativeI32ToU16()
		: base("std::^u16 (std::i32)", Kernel.T_I32, Kernel.T_U16)
	{
	}

	internal override void Run(CPU cpu)
	{
		int a = cpu.Pop();
		cpu.Push((ushort)a);
	}
}

class NativeI32ToU32 : FunctionNative {

	internal NativeI32ToU32()
		: base("std::^u32 (std::i32)", Kernel.T_I32, Kernel.T_U32)
	{
	}

	internal override void Run(CPU cpu)
	{
		int a = cpu.Pop();
		cpu.Push((uint)a);
	}
}

class NativeI32ToU64 : FunctionNative {

	internal NativeI32ToU64()
		: base("std::^u64 (std::i32)", Kernel.T_I32, Kernel.T_U64)
	{
	}

	internal override void Run(CPU cpu)
	{
		int a = cpu.Pop();
		cpu.Push((ulong)a);
	}
}

class NativeI32ToI8 : FunctionNative {

	internal NativeI32ToI8()
		: base("std::^i8 (std::i32)", Kernel.T_I32, Kernel.T_I8)
	{
	}

	internal override void Run(CPU cpu)
	{
		int a = cpu.Pop();
		cpu.Push((sbyte)a);
	}
}

class NativeI32ToI16 : FunctionNative {

	internal NativeI32ToI16()
		: base("std::^i16 (std::i32)", Kernel.T_I32, Kernel.T_I16)
	{
	}

	internal override void Run(CPU cpu)
	{
		int a = cpu.Pop();
		cpu.Push((short)a);
	}
}

class NativeI32ToI32 : FunctionNative {

	internal NativeI32ToI32()
		: base("std::^i32 (std::i32)", Kernel.T_I32, Kernel.T_I32)
	{
	}

	internal override void Run(CPU cpu)
	{
		int a = cpu.Pop();
		cpu.Push((int)a);
	}
}

class NativeI32ToI64 : FunctionNative {

	internal NativeI32ToI64()
		: base("std::^i64 (std::i32)", Kernel.T_I32, Kernel.T_I64)
	{
	}

	internal override void Run(CPU cpu)
	{
		int a = cpu.Pop();
		cpu.Push((long)a);
	}
}

class NativeI32ToInt : FunctionNative {

	internal NativeI32ToInt()
		: base("std::^int (std::i32)", Kernel.T_I32, Kernel.T_Int)
	{
	}

	internal override void Run(CPU cpu)
	{
		int a = cpu.Pop();
		cpu.Push(XValue.MakeInt((int)a));
	}
}

/* ======================================================================= */
/* std::i64 */

class NativeI64Add : FunctionNative {

	internal NativeI64Add()
		: base("std::+ (std::i64)", Kernel.T_I64I64, Kernel.T_I64)
	{
	}

	internal override void Run(CPU cpu)
	{
		long b = cpu.Pop();
		long a = cpu.Pop();
		cpu.Push((long)(a + b));
	}
}

class NativeI64Sub : FunctionNative {

	internal NativeI64Sub()
		: base("std::- (std::i64)", Kernel.T_I64I64, Kernel.T_I64)
	{
	}

	internal override void Run(CPU cpu)
	{
		long b = cpu.Pop();
		long a = cpu.Pop();
		cpu.Push((long)(a - b));
	}
}

class NativeI64Mul : FunctionNative {

	internal NativeI64Mul()
		: base("std::* (std::i64)", Kernel.T_I64I64, Kernel.T_I64)
	{
	}

	internal override void Run(CPU cpu)
	{
		long b = cpu.Pop();
		long a = cpu.Pop();
		cpu.Push((long)(a * b));
	}
}

class NativeI64Div : FunctionNative {

	internal NativeI64Div()
		: base("std::/ (std::i64)", Kernel.T_I64I64, Kernel.T_I64)
	{
	}

	internal override void Run(CPU cpu)
	{
		long b = cpu.Pop();
		long a = cpu.Pop();
		if (b == 0) {
			throw new Exception("division by zero");
		}
		if (b == -1 && a == Int64.MinValue) {
			throw new Exception("division overflow");
		}
		cpu.Push((long)(a / b));
	}
}

class NativeI64Mod : FunctionNative {

	internal NativeI64Mod()
		: base("std::% (std::i64)", Kernel.T_I64I64, Kernel.T_I64)
	{
	}

	internal override void Run(CPU cpu)
	{
		long b = cpu.Pop();
		long a = cpu.Pop();
		if (b == 0) {
			throw new Exception("division by zero");
		}
		if (b == -1 && a == Int64.MinValue) {
			throw new Exception("division overflow");
		}
		cpu.Push((long)(a % b));
	}
}

class NativeI64DivMod : FunctionNative {

	internal NativeI64DivMod()
		: base("std::/% (std::i64)", Kernel.T_I64I64, Kernel.T_I64I64)
	{
	}

	internal override void Run(CPU cpu)
	{
		long b = cpu.Pop();
		long a = cpu.Pop();
		if (b == 0) {
			throw new Exception("division by zero");
		}
		if (b == -1 && a == Int64.MinValue) {
			throw new Exception("division overflow");
		}
		cpu.Push((long)(a / b));
		cpu.Push((long)(a % b));
	}
}

class NativeI64Neg : FunctionNative {

	internal NativeI64Neg()
		: base("std::neg (std::i64)", Kernel.T_I64, Kernel.T_I64)
	{
	}

	internal override void Run(CPU cpu)
	{
		long a = cpu.Pop();
		cpu.Push((long)-a);
	}
}

class NativeI64Inc : FunctionNative {

	internal NativeI64Inc()
		: base("std::++ (std::i64)", Kernel.T_I64, Kernel.T_I64)
	{
	}

	internal override void Run(CPU cpu)
	{
		long a = cpu.Pop();
		cpu.Push((long)(a + 1));
	}
}

class NativeI64Dec : FunctionNative {

	internal NativeI64Dec()
		: base("std::-- (std::i64)", Kernel.T_I64, Kernel.T_I64)
	{
	}

	internal override void Run(CPU cpu)
	{
		long a = cpu.Pop();
		cpu.Push((long)(a - 1));
	}
}

class NativeI64And : FunctionNative {

	internal NativeI64And()
		: base("std::and (std::i64)", Kernel.T_I64I64, Kernel.T_I64)
	{
	}

	internal override void Run(CPU cpu)
	{
		long b = cpu.Pop();
		long a = cpu.Pop();
		cpu.Push((long)((ulong)a & (ulong)b));
	}
}

class NativeI64Or : FunctionNative {

	internal NativeI64Or()
		: base("std::or (std::i64)", Kernel.T_I64I64, Kernel.T_I64)
	{
	}

	internal override void Run(CPU cpu)
	{
		long b = cpu.Pop();
		long a = cpu.Pop();
		cpu.Push((long)((ulong)a | (ulong)b));
	}
}

class NativeI64Xor : FunctionNative {

	internal NativeI64Xor()
		: base("std::xor (std::i64)", Kernel.T_I64I64, Kernel.T_I64)
	{
	}

	internal override void Run(CPU cpu)
	{
		long b = cpu.Pop();
		long a = cpu.Pop();
		cpu.Push((long)((ulong)a ^ (ulong)b));
	}
}

class NativeI64Eqv : FunctionNative {

	internal NativeI64Eqv()
		: base("std::eqv (std::i64)", Kernel.T_I64I64, Kernel.T_I64)
	{
	}

	internal override void Run(CPU cpu)
	{
		long b = cpu.Pop();
		long a = cpu.Pop();
		cpu.Push((long)~((ulong)a ^ (ulong)b));
	}
}

class NativeI64Not : FunctionNative {

	internal NativeI64Not()
		: base("std::not (std::i64)", Kernel.T_I64, Kernel.T_I64)
	{
	}

	internal override void Run(CPU cpu)
	{
		long a = cpu.Pop();
		cpu.Push((long)~(ulong)a);
	}
}

class NativeI64LT : FunctionNative {

	internal NativeI64LT()
		: base("std::< (std::i64)", Kernel.T_I64I64, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		long b = cpu.Pop();
		long a = cpu.Pop();
		cpu.Push(a < b);
	}
}

class NativeI64LEQ : FunctionNative {

	internal NativeI64LEQ()
		: base("std::<= (std::i64)", Kernel.T_I64I64, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		long b = cpu.Pop();
		long a = cpu.Pop();
		cpu.Push(a <= b);
	}
}

class NativeI64GT : FunctionNative {

	internal NativeI64GT()
		: base("std::> (std::i64)", Kernel.T_I64I64, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		long b = cpu.Pop();
		long a = cpu.Pop();
		cpu.Push(a > b);
	}
}

class NativeI64GEQ : FunctionNative {

	internal NativeI64GEQ()
		: base("std::>= (std::i64)", Kernel.T_I64I64, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		long b = cpu.Pop();
		long a = cpu.Pop();
		cpu.Push(a >= b);
	}
}

class NativeI64MulH : FunctionNative {

	internal NativeI64MulH()
		: base("std::*h (std::i64)", Kernel.T_I64I64, Kernel.T_I64)
	{
	}

	internal override void Run(CPU cpu)
	{
		long b = cpu.Pop();
		long a = cpu.Pop();
		cpu.Push(Kernel.Smulh64(a, b));
	}
}

class NativeI64MulHL : FunctionNative {

	internal NativeI64MulHL()
		: base("std::*hl (std::i64)", Kernel.T_I64I64, Kernel.T_I64U64)
	{
	}

	internal override void Run(CPU cpu)
	{
		long b = cpu.Pop();
		long a = cpu.Pop();
		cpu.Push(Kernel.Smulh64(a, b));
		cpu.Push((ulong)(a * b));
	}
}

class NativeI64ToU8 : FunctionNative {

	internal NativeI64ToU8()
		: base("std::^u8 (std::i64)", Kernel.T_I64, Kernel.T_U8)
	{
	}

	internal override void Run(CPU cpu)
	{
		long a = cpu.Pop();
		cpu.Push((byte)a);
	}
}

class NativeI64ToU16 : FunctionNative {

	internal NativeI64ToU16()
		: base("std::^u16 (std::i64)", Kernel.T_I64, Kernel.T_U16)
	{
	}

	internal override void Run(CPU cpu)
	{
		long a = cpu.Pop();
		cpu.Push((ushort)a);
	}
}

class NativeI64ToU32 : FunctionNative {

	internal NativeI64ToU32()
		: base("std::^u32 (std::i64)", Kernel.T_I64, Kernel.T_U32)
	{
	}

	internal override void Run(CPU cpu)
	{
		long a = cpu.Pop();
		cpu.Push((uint)a);
	}
}

class NativeI64ToU64 : FunctionNative {

	internal NativeI64ToU64()
		: base("std::^u64 (std::i64)", Kernel.T_I64, Kernel.T_U64)
	{
	}

	internal override void Run(CPU cpu)
	{
		long a = cpu.Pop();
		cpu.Push((ulong)a);
	}
}

class NativeI64ToI8 : FunctionNative {

	internal NativeI64ToI8()
		: base("std::^i8 (std::i64)", Kernel.T_I64, Kernel.T_I8)
	{
	}

	internal override void Run(CPU cpu)
	{
		long a = cpu.Pop();
		cpu.Push((sbyte)a);
	}
}

class NativeI64ToI16 : FunctionNative {

	internal NativeI64ToI16()
		: base("std::^i16 (std::i64)", Kernel.T_I64, Kernel.T_I16)
	{
	}

	internal override void Run(CPU cpu)
	{
		long a = cpu.Pop();
		cpu.Push((short)a);
	}
}

class NativeI64ToI32 : FunctionNative {

	internal NativeI64ToI32()
		: base("std::^i32 (std::i64)", Kernel.T_I64, Kernel.T_I32)
	{
	}

	internal override void Run(CPU cpu)
	{
		long a = cpu.Pop();
		cpu.Push((int)a);
	}
}

class NativeI64ToI64 : FunctionNative {

	internal NativeI64ToI64()
		: base("std::^i64 (std::i64)", Kernel.T_I64, Kernel.T_I64)
	{
	}

	internal override void Run(CPU cpu)
	{
		long a = cpu.Pop();
		cpu.Push((long)a);
	}
}

class NativeI64ToInt : FunctionNative {

	internal NativeI64ToInt()
		: base("std::^int (std::i64)", Kernel.T_I64, Kernel.T_Int)
	{
	}

	internal override void Run(CPU cpu)
	{
		long a = cpu.Pop();
		if (a < (long)Int32.MinValue || a > (long)Int32.MaxValue) {
			throw new Exception("overflow in i64 -> int conversion");
		}
		cpu.Push(XValue.MakeInt((int)a));
	}
}

/* ======================================================================= */
/* std::int */

class NativeIntAdd : FunctionNative {

	internal NativeIntAdd()
		: base("std::+ (std::int)", Kernel.T_IntInt, Kernel.T_Int)
	{
	}

	internal override void Run(CPU cpu)
	{
		long b = cpu.Pop().Int;
		long a = cpu.Pop().Int;
		Kernel.PushInt(cpu, a + b);
	}
}

class NativeIntSub : FunctionNative {

	internal NativeIntSub()
		: base("std::- (std::int)", Kernel.T_IntInt, Kernel.T_Int)
	{
	}

	internal override void Run(CPU cpu)
	{
		long b = cpu.Pop().Int;
		long a = cpu.Pop().Int;
		Kernel.PushInt(cpu, a - b);
	}
}

class NativeIntMul : FunctionNative {

	internal NativeIntMul()
		: base("std::* (std::int)", Kernel.T_IntInt, Kernel.T_Int)
	{
	}

	internal override void Run(CPU cpu)
	{
		long b = cpu.Pop().Int;
		long a = cpu.Pop().Int;
		Kernel.PushInt(cpu, a * b);
	}
}

class NativeIntDiv : FunctionNative {

	internal NativeIntDiv()
		: base("std::/ (std::int)", Kernel.T_IntInt, Kernel.T_Int)
	{
	}

	internal override void Run(CPU cpu)
	{
		long b = cpu.Pop().Int;
		long a = cpu.Pop().Int;
		if (b == 0) {
			throw new Exception("division by zero");
		}
		Kernel.PushInt(cpu, a / b);
	}
}

class NativeIntMod : FunctionNative {

	internal NativeIntMod()
		: base("std::% (std::int)", Kernel.T_IntInt, Kernel.T_Int)
	{
	}

	internal override void Run(CPU cpu)
	{
		long b = cpu.Pop().Int;
		long a = cpu.Pop().Int;
		if (b == 0) {
			throw new Exception("division by zero");
		}
		Kernel.PushInt(cpu, a % b);
	}
}

class NativeIntDivMod : FunctionNative {

	internal NativeIntDivMod()
		: base("std::/% (std::int)", Kernel.T_IntInt, Kernel.T_IntInt)
	{
	}

	internal override void Run(CPU cpu)
	{
		long b = cpu.Pop().Int;
		long a = cpu.Pop().Int;
		if (b == 0) {
			throw new Exception("division by zero");
		}
		Kernel.PushInt(cpu, a / b);
		Kernel.PushInt(cpu, a % b);
	}
}

class NativeIntNeg : FunctionNative {

	internal NativeIntNeg()
		: base("std::neg (std::int)", Kernel.T_Int, Kernel.T_Int)
	{
	}

	internal override void Run(CPU cpu)
	{
		long a = cpu.Pop().Int;
		Kernel.PushInt(cpu, -a);
	}
}

class NativeIntInc : FunctionNative {

	internal NativeIntInc()
		: base("std::++ (std::int)", Kernel.T_Int, Kernel.T_Int)
	{
	}

	internal override void Run(CPU cpu)
	{
		long a = cpu.Pop().Int;
		Kernel.PushInt(cpu, a + 1);
	}
}

class NativeIntDec : FunctionNative {

	internal NativeIntDec()
		: base("std::-- (std::int)", Kernel.T_Int, Kernel.T_Int)
	{
	}

	internal override void Run(CPU cpu)
	{
		long a = cpu.Pop().Int;
		Kernel.PushInt(cpu, a - 1);
	}
}

class NativeIntAnd : FunctionNative {

	internal NativeIntAnd()
		: base("std::and (std::int)", Kernel.T_IntInt, Kernel.T_Int)
	{
	}

	internal override void Run(CPU cpu)
	{
		int b = cpu.Pop().Int;
		int a = cpu.Pop().Int;
		cpu.Push((int)((ulong)a & (ulong)b));
	}
}

class NativeIntOr : FunctionNative {

	internal NativeIntOr()
		: base("std::or (std::int)", Kernel.T_IntInt, Kernel.T_Int)
	{
	}

	internal override void Run(CPU cpu)
	{
		int b = cpu.Pop().Int;
		int a = cpu.Pop().Int;
		cpu.Push((int)((ulong)a | (ulong)b));
	}
}

class NativeIntXor : FunctionNative {

	internal NativeIntXor()
		: base("std::xor (std::int)", Kernel.T_IntInt, Kernel.T_Int)
	{
	}

	internal override void Run(CPU cpu)
	{
		int b = cpu.Pop().Int;
		int a = cpu.Pop().Int;
		cpu.Push((int)((ulong)a ^ (ulong)b));
	}
}

class NativeIntEqv : FunctionNative {

	internal NativeIntEqv()
		: base("std::eqv (std::int)", Kernel.T_IntInt, Kernel.T_Int)
	{
	}

	internal override void Run(CPU cpu)
	{
		int b = cpu.Pop().Int;
		int a = cpu.Pop().Int;
		cpu.Push((int)~((ulong)a ^ (ulong)b));
	}
}

class NativeIntNot : FunctionNative {

	internal NativeIntNot()
		: base("std::not (std::int)", Kernel.T_Int, Kernel.T_Int)
	{
	}

	internal override void Run(CPU cpu)
	{
		int a = cpu.Pop().Int;
		cpu.Push((int)~(ulong)a);
	}
}

class NativeIntLT : FunctionNative {

	internal NativeIntLT()
		: base("std::< (std::int)", Kernel.T_IntInt, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		int b = cpu.Pop().Int;
		int a = cpu.Pop().Int;
		cpu.Push(a < b);
	}
}

class NativeIntLEQ : FunctionNative {

	internal NativeIntLEQ()
		: base("std::<= (std::int)", Kernel.T_IntInt, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		int b = cpu.Pop().Int;
		int a = cpu.Pop().Int;
		cpu.Push(a <= b);
	}
}

class NativeIntGT : FunctionNative {

	internal NativeIntGT()
		: base("std::> (std::int)", Kernel.T_IntInt, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		int b = cpu.Pop().Int;
		int a = cpu.Pop().Int;
		cpu.Push(a > b);
	}
}

class NativeIntGEQ : FunctionNative {

	internal NativeIntGEQ()
		: base("std::>= (std::int)", Kernel.T_IntInt, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		int b = cpu.Pop().Int;
		int a = cpu.Pop().Int;
		cpu.Push(a >= b);
	}
}

class NativeIntToU8 : FunctionNative {

	internal NativeIntToU8()
		: base("std::^u8 (std::int)", Kernel.T_Int, Kernel.T_U8)
	{
	}

	internal override void Run(CPU cpu)
	{
		int a = cpu.Pop().Int;
		cpu.Push((byte)a);
	}
}

class NativeIntToU16 : FunctionNative {

	internal NativeIntToU16()
		: base("std::^u16 (std::int)", Kernel.T_Int, Kernel.T_U16)
	{
	}

	internal override void Run(CPU cpu)
	{
		int a = cpu.Pop().Int;
		cpu.Push((ushort)a);
	}
}

class NativeIntToU32 : FunctionNative {

	internal NativeIntToU32()
		: base("std::^u32 (std::int)", Kernel.T_Int, Kernel.T_U32)
	{
	}

	internal override void Run(CPU cpu)
	{
		int a = cpu.Pop().Int;
		cpu.Push((uint)a);
	}
}

class NativeIntToU64 : FunctionNative {

	internal NativeIntToU64()
		: base("std::^u64 (std::int)", Kernel.T_Int, Kernel.T_U64)
	{
	}

	internal override void Run(CPU cpu)
	{
		int a = cpu.Pop().Int;
		cpu.Push((ulong)a);
	}
}

class NativeIntToI8 : FunctionNative {

	internal NativeIntToI8()
		: base("std::^i8 (std::int)", Kernel.T_Int, Kernel.T_I8)
	{
	}

	internal override void Run(CPU cpu)
	{
		int a = cpu.Pop().Int;
		cpu.Push((sbyte)a);
	}
}

class NativeIntToI16 : FunctionNative {

	internal NativeIntToI16()
		: base("std::^i16 (std::int)", Kernel.T_Int, Kernel.T_I16)
	{
	}

	internal override void Run(CPU cpu)
	{
		int a = cpu.Pop().Int;
		cpu.Push((short)a);
	}
}

class NativeIntToI32 : FunctionNative {

	internal NativeIntToI32()
		: base("std::^i32 (std::int)", Kernel.T_Int, Kernel.T_I32)
	{
	}

	internal override void Run(CPU cpu)
	{
		int a = cpu.Pop().Int;
		cpu.Push((int)a);
	}
}

class NativeIntToI64 : FunctionNative {

	internal NativeIntToI64()
		: base("std::^i64 (std::int)", Kernel.T_Int, Kernel.T_I64)
	{
	}

	internal override void Run(CPU cpu)
	{
		int a = cpu.Pop().Int;
		cpu.Push((long)a);
	}
}

class NativeIntToInt : FunctionNative {

	internal NativeIntToInt()
		: base("std::^int (std::int)", Kernel.T_Int, Kernel.T_Int)
	{
	}

	internal override void Run(CPU cpu)
	{
		int a = cpu.Pop().Int;
		cpu.Push(XValue.MakeInt((int)a));
	}
}

/* ======================================================================= */
/* std::string */

/* obsolete
class NativeStringEquals : FunctionNative {

	internal NativeStringEquals()
		: base("std::= (std::string)",
			Kernel.T_StringString, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		XString b = cpu.Pop().XString;
		XString a = cpu.Pop().XString;
		cpu.Push(a == b);
	}
}
*/

/* obsolete
class NativeStringNotEquals : FunctionNative {

	internal NativeStringNotEquals()
		: base("std::<> (std::string)",
			Kernel.T_StringString, Kernel.T_Bool)
	{
	}

	internal override void Run(CPU cpu)
	{
		XString b = cpu.Pop().XString;
		XString a = cpu.Pop().XString;
		cpu.Push(a != b);
	}
}
*/

/* obsolete
class NativeStringLength : FunctionNative {

	internal NativeStringLength()
		: base("std::length (std::string)",
			Kernel.T_String, Kernel.T_Int)
	{
	}

	internal override void Run(CPU cpu)
	{
		XString a = cpu.Pop().XString;
		cpu.Push(a.Length);
	}
}
*/

/* obsolete
class NativeStringAt : FunctionNative {

	internal NativeStringAt()
		: base("std::@ (std::string)",
			Kernel.T_IntString, Kernel.T_Int)
	{
	}

	internal override void Run(CPU cpu)
	{
		XString a = cpu.Pop().XString;
		int k = cpu.Pop().Int;
		cpu.Push(XValue.MakeInt(a[k]));
	}
}
*/

class NativeStringConcat : FunctionNative {

	internal NativeStringConcat()
		: base("std::+ (std::string)",
			Kernel.T_StringString, Kernel.T_String)
	{
	}

	internal override void Run(CPU cpu)
	{
		XArrayGeneric xag2 = cpu.Pop().XObject as XArrayGeneric;
		XArrayGeneric xag1 = cpu.Pop().XObject as XArrayGeneric;
		if (xag1 == null || xag2 == null
			|| xag1.ObjectType != XType.ARRAY_U8
			|| xag2.ObjectType != XType.ARRAY_U8)
		{
			throw new Exception("not true std::string");
		}
		XArrayGeneric xag3 =
			XArrayGeneric.Concat(XType.ARRAY_U8, xag1, xag2);
		cpu.Push(new XValue(xag3));
	}
}

class NativeStringFail : FunctionNative {

	internal NativeStringFail()
		: base("std::fail (std::string)",
			Kernel.T_String, XType.ZERO)
	{
	}

	internal override void Run(CPU cpu)
	{
		throw new Exception(string.Format("FAIL: {0}",
			cpu.Pop().String));
	}
}

/* ======================================================================= */
/* std::type */

class NativeType : Function {

	XType xt;

	internal NativeType(XType xt)
		: base(xt.Name)
	{
		this.xt = xt;
	}

	internal override void Run(CPU cpu)
	{
		cpu.Push(new XValue(xt));
	}

	internal override CCNode Enter(CCStack stack,
		CCNodeEntry parent, CCNode next)
	{
		CCNode node = new CCNodeNativeXType(xt, next);
		node.MergeStack(stack);
		return node;
	}
}

class NativeTypeName : FunctionNative {

	internal NativeTypeName()
		: base("std::name (std::type)", Kernel.T_Type, Kernel.T_String)
	{
	}

	internal override void Run(CPU cpu)
	{
		XType a = cpu.Pop().XTypeInstance;
		cpu.Push(new XValue(a.Name));
	}
}

class NativeTypeNew : Function {

	internal NativeTypeNew()
		: base("std::new (std::type)")
	{
	}

	internal override void Run(CPU cpu)
	{
		XType a = cpu.Pop().XTypeInstance;
		cpu.Push(new XValue(a.NewInstance()));
	}

	internal override CCNode Enter(CCStack stack,
		CCNodeEntry parent, CCNode next)
	{
		CCNode node = new CCNodeNativeNew(next);
		node.MergeStack(stack);
		return node;
	}
}

class NativeArrayRef : Function {

	internal NativeArrayRef()
		: base("std::array (std::type)")
	{
	}

	internal override void Run(CPU cpu)
	{
		XType a = cpu.Pop().XTypeInstance;
		cpu.Push(new XValue(XType.LookupArray(a, false)));
	}
}

class NativeArrayEmbed : Function {

	internal NativeArrayEmbed()
		: base("std::array& (std::type)")
	{
	}

	internal override void Run(CPU cpu)
	{
		XType a = cpu.Pop().XTypeInstance;
		cpu.Push(new XValue(XType.LookupArray(a, true)));
	}
}

/* ======================================================================= */
/* std.io */

class NativePrint : Function {

	internal NativePrint()
		: base("std.io::print (std::object)")
	{
	}

	internal override void Run(CPU cpu)
	{
		XValue xv = cpu.Pop();
		Console.Write(xv.ToString());
	}
}

class NativePrintln : Function {

	internal NativePrintln()
		: base("std.io::println (std::object)")
	{
	}

	internal override void Run(CPU cpu)
	{
		XValue xv = cpu.Pop();
		Console.WriteLine(xv.ToString());
	}
}

/* ======================================================================= */
/* Accessors for instance fields and embedded sub-structures. */

abstract class NativeInstanceAccessor : Function {

	internal XType owner;
	internal XType ltype;
	internal string eltName;

	internal NativeInstanceAccessor(XType owner, string eltName,
		XType ltype, string accessorName)
		: base(accessorName + " (" + ltype.Name + ")")
	{
		this.owner = owner;
		this.ltype = ltype;
		this.eltName = eltName;
	}

	internal string FullElementName(XObjectGen instance)
	{
		XType xt = instance.ObjectType;
		string fn = string.Format("{0} from object type {1}",
			eltName, owner.Name);
		if (xt != owner) {
			fn = string.Format("{0} (extended by {1})",
				fn, xt.Name);
		}
		return fn;
	}
}

class NativeFieldGet : NativeInstanceAccessor {

	int off;

	internal NativeFieldGet(XType owner, string name, XType ltype,
		int off, string accessorName)
		: base(owner, name, ltype, accessorName)
	{
		this.off = off;
	}

	internal override void Run(CPU cpu)
	{
		XObjectGen xo = cpu.Pop().XObjectGen;
		XObjectGen xo2 = XType.FindExtendedGen(xo, owner);
		XValue xv = xo2.fields[off];
		if (xv.IsUninitialized) {
			throw new Exception(string.Format("reading uninitialized field {0}", FullElementName(xo)));
		}
		cpu.Push(xv);
	}

	internal override CCNode Enter(CCStack stack,
		CCNodeEntry parent, CCNode next)
	{
		CCNode node = new CCNodeNativeFieldGet(this, owner, off, next);
		node.MergeStack(stack);
		return node;
	}
}

class NativeFieldPut : NativeInstanceAccessor {

	int off;

	internal NativeFieldPut(XType owner, string name, XType ltype,
		int off, string accessorName)
		: base(owner, name, ltype, accessorName)
	{
		this.off = off;
	}

	internal override void Run(CPU cpu)
	{
		XObjectGen xo = cpu.Pop().XObjectGen;
		XObjectGen xo2 = XType.FindExtendedGen(xo, owner);
		xo2.fields[off] = cpu.Pop();
	}

	internal override CCNode Enter(CCStack stack,
		CCNodeEntry parent, CCNode next)
	{
		CCNode node = new CCNodeNativeFieldPut(this, owner, off, next);
		node.MergeStack(stack);
		return node;
	}
}

class NativeFieldClear : NativeInstanceAccessor {

	int off;

	internal NativeFieldClear(XType owner, string name, XType ltype,
		int off, string accessorName)
		: base(owner, name, ltype, accessorName)
	{
		this.off = off;
	}

	internal override void Run(CPU cpu)
	{
		XObjectGen xo = cpu.Pop().XObjectGen;
		XObjectGen xo2 = XType.FindExtendedGen(xo, owner);
		xo2.fields[off].Clear(ltype);
	}

	internal override CCNode Enter(CCStack stack,
		CCNodeEntry parent, CCNode next)
	{
		CCNode node = new CCNodeNativeFieldClear(this, owner, next);
		node.MergeStack(stack);
		return node;
	}
}

class NativeFieldTest : NativeInstanceAccessor {

	int off;

	internal NativeFieldTest(XType owner, string name, XType ltype,
		int off, string accessorName)
		: base(owner, name, ltype, accessorName)
	{
		this.off = off;
	}

	internal override void Run(CPU cpu)
	{
		XObjectGen xo = cpu.Pop().XObjectGen;
		XObjectGen xo2 = XType.FindExtendedGen(xo, owner);
		cpu.Push(!xo2.fields[off].IsUninitialized);
	}

	internal override CCNode Enter(CCStack stack,
		CCNodeEntry parent, CCNode next)
	{
		CCNode node = new CCNodeNativeFieldTest(this, owner, next);
		node.MergeStack(stack);
		return node;
	}
}

class NativeEmbedRef : NativeInstanceAccessor {

	int off;

	internal NativeEmbedRef(XType owner, string name, XType ltype,
		int off, string accessorName)
		: base(owner, name, ltype, accessorName)
	{
		this.off = off;
	}

	internal override void Run(CPU cpu)
	{
		XObjectGen xo = cpu.Pop().XObjectGen;
		XObjectGen xo2 = XType.FindExtendedGen(xo, owner);
		cpu.Push(new XValue(xo2.embeds[off]));
	}

	internal override CCNode Enter(CCStack stack,
		CCNodeEntry parent, CCNode next)
	{
		CCNode node = new CCNodeNativeEmbedRef(
			this, owner, ltype, next);
		node.MergeStack(stack);
		return node;
	}
}

abstract class NativeInstanceAccessorIndexed : NativeInstanceAccessor {

	internal int off;
	int len;

	internal NativeInstanceAccessorIndexed(XType owner, string eltName,
		XType ltype, int off, int len, string accessorName)
		: base(owner, eltName, ltype, accessorName)
	{
		this.off = off;
		this.len = len;
	}

	internal int PopIndex(CPU cpu)
	{
		int k = cpu.Pop().Int;
		if (k < 0 || k >= len) {
			throw new Exception(string.Format("index out of bounds for element {0}: {1} (max: {2})", eltName, k, len));
		}
		return off + k;
	}
}

class NativeFieldArrayGet : NativeInstanceAccessorIndexed {

	internal NativeFieldArrayGet(XType owner, string name, XType ltype,
		int off, int len, string accessorName)
		: base(owner, name, ltype, off, len, accessorName)
	{
	}

	internal override void Run(CPU cpu)
	{
		XObjectGen xo = cpu.Pop().XObjectGen;
		XObjectGen xo2 = XType.FindExtendedGen(xo, owner);
		int k = PopIndex(cpu);
		XValue xv = xo2.fields[k];
		if (xv.IsUninitialized) {
			throw new Exception(string.Format("reading uninitialized field {0} (at offset {1})", FullElementName(xo), k));
		}
		cpu.Push(xv);
	}

	internal override CCNode Enter(CCStack stack,
		CCNodeEntry parent, CCNode next)
	{
		CCNode node = new CCNodeNativeFieldArrayGet(
			this, owner, off, next);
		node.MergeStack(stack);
		return node;
	}
}

class NativeFieldArrayPut : NativeInstanceAccessorIndexed {

	internal NativeFieldArrayPut(XType owner, string name, XType ltype,
		int off, int len, string accessorName)
		: base(owner, name, ltype, off, len, accessorName)
	{
	}

	internal override void Run(CPU cpu)
	{
		XObjectGen xo = cpu.Pop().XObjectGen;
		XObjectGen xo2 = XType.FindExtendedGen(xo, owner);
		int k = PopIndex(cpu);
		xo2.fields[k] = cpu.Pop();
	}

	internal override CCNode Enter(CCStack stack,
		CCNodeEntry parent, CCNode next)
	{
		CCNode node = new CCNodeNativeFieldArrayPut(
			this, owner, off, next);
		node.MergeStack(stack);
		return node;
	}
}

class NativeFieldArrayClear : NativeInstanceAccessorIndexed {

	internal NativeFieldArrayClear(XType owner, string name, XType ltype,
		int off, int len, string accessorName)
		: base(owner, name, ltype, off, len, accessorName)
	{
	}

	internal override void Run(CPU cpu)
	{
		XObjectGen xo = cpu.Pop().XObjectGen;
		XObjectGen xo2 = XType.FindExtendedGen(xo, owner);
		int k = PopIndex(cpu);
		xo2.fields[k].Clear(ltype);
	}

	internal override CCNode Enter(CCStack stack,
		CCNodeEntry parent, CCNode next)
	{
		CCNode node = new CCNodeNativeFieldArrayClear(
			this, owner, next);
		node.MergeStack(stack);
		return node;
	}
}

class NativeFieldArrayTest : NativeInstanceAccessorIndexed {

	internal NativeFieldArrayTest(XType owner, string name, XType ltype,
		int off, int len, string accessorName)
		: base(owner, name, ltype, off, len, accessorName)
	{
	}

	internal override void Run(CPU cpu)
	{
		XObjectGen xo = cpu.Pop().XObjectGen;
		XObjectGen xo2 = XType.FindExtendedGen(xo, owner);
		int k = PopIndex(cpu);
		cpu.Push(!xo2.fields[k].IsUninitialized);
	}

	internal override CCNode Enter(CCStack stack,
		CCNodeEntry parent, CCNode next)
	{
		CCNode node = new CCNodeNativeFieldArrayTest(
			this, owner, next);
		node.MergeStack(stack);
		return node;
	}
}

class NativeEmbedArrayRef : NativeInstanceAccessorIndexed {

	internal NativeEmbedArrayRef(XType owner, string name, XType ltype,
		int off, int len, string accessorName)
		: base(owner, name, ltype, off, len, accessorName)
	{
	}

	internal override void Run(CPU cpu)
	{
		XObjectGen xo = cpu.Pop().XObjectGen;
		XObjectGen xo2 = XType.FindExtendedGen(xo, owner);
		int k = PopIndex(cpu);
		cpu.Push(new XValue(xo2.embeds[k]));
	}

	internal override CCNode Enter(CCStack stack,
		CCNodeEntry parent, CCNode next)
	{
		CCNode node = new CCNodeNativeEmbedArrayRef(
			this, owner, ltype, next);
		node.MergeStack(stack);
		return node;
	}
}

/* ======================================================================= */
/* Accessors for generic array types. */

abstract class NativeArrayAccessor : Function {

	internal XType owner;

	internal NativeArrayAccessor(XType owner, string debugName)
		: base(debugName + " " + owner.Name)
	{
		this.owner = owner;
	}

	/*
	 * Pop a reference from the stack and find the proper array
	 * instance in it, following embeddings if necessary.
	 */
	internal XArrayGeneric PopInstance(CPU cpu)
	{
		XObject xo = cpu.Pop().XObject;
		xo = XType.FindExtended(xo, owner);
		XArrayGeneric a = xo as XArrayGeneric;
		if (a == null) {
			throw new Exception(string.Format("array accessor invoked on an instance of {0}, not {1}", xo.ObjectType, owner.Name));
		}
		return a;
	}
}

class NativeArrayAccessorMakeRef : NativeArrayAccessor {

	internal NativeArrayAccessorMakeRef(XType owner)
		: base(owner, "std::make")
	{
	}

	internal override void Run(CPU cpu)
	{
		XArrayGeneric a = PopInstance(cpu);
		int len = cpu.Pop().Int;
		if (len < 0) {
			throw new Exception(string.Format("cannot create array {0} with negative length {1}", owner.Name, len));
		}
		a.Init(len);
		cpu.Push(new XValue(a));
	}

	internal override CCNode Enter(CCStack stack,
		CCNodeEntry parent, CCNode next)
	{
		CCNode node = new CCNodeNativeArrayMakeRef(this, owner, next);
		node.MergeStack(stack);
		return node;
	}
}

class NativeArrayAccessorMakeEmbed : NativeArrayAccessor {

	internal NativeArrayAccessorMakeEmbed(XType owner)
		: base(owner, "std::make")
	{
	}

	internal override void Run(CPU cpu)
	{
		XArrayGeneric a = PopInstance(cpu);
		int len = cpu.Pop().Int;
		if (len < 0) {
			throw new Exception(string.Format("cannot create array {0} with negative length {1}", owner.Name, len));
		}
		a.Init(len);
		XType eltType = owner.GetArrayElementType();
		for (int i = 0; i < len; i ++) {
			a[i] = new XValue(eltType.NewInstance());
		}
		cpu.Push(new XValue(a));
	}

	internal override CCNode Enter(CCStack stack,
		CCNodeEntry parent, CCNode next)
	{
		CCNode node = new CCNodeNativeArrayMakeEmbed(this, owner, next);
		node.MergeStack(stack);
		return node;
	}
}

class NativeArrayAccessorSub : NativeArrayAccessor {

	internal NativeArrayAccessorSub(XType owner)
		: base(owner, "std::sub")
	{
	}

	internal override void Run(CPU cpu)
	{
		XArrayGeneric d = PopInstance(cpu);
		XArrayGeneric s = PopInstance(cpu);
		int len = cpu.Pop().Int;
		int off = cpu.Pop().Int;
		d.InitSub(s, off, len);
	}

	internal override CCNode Enter(CCStack stack,
		CCNodeEntry parent, CCNode next)
	{
		CCNode node = new CCNodeNativeArraySub(this, owner, next);
		node.MergeStack(stack);
		return node;
	}
}

class NativeArrayAccessorSubSelf : NativeArrayAccessor {

	internal NativeArrayAccessorSubSelf(XType owner)
		: base(owner, "std::subself")
	{
	}

	internal override void Run(CPU cpu)
	{
		XArrayGeneric a = PopInstance(cpu);
		int len = cpu.Pop().Int;
		int off = cpu.Pop().Int;
		a.InitSub(a, off, len);
	}

	internal override CCNode Enter(CCStack stack,
		CCNodeEntry parent, CCNode next)
	{
		CCNode node = new CCNodeNativeArraySubSelf(this, owner, next);
		node.MergeStack(stack);
		return node;
	}
}

class NativeArrayAccessorIsInit : NativeArrayAccessor {

	internal NativeArrayAccessorIsInit(XType owner)
		: base(owner, "std::init?")
	{
	}

	internal override void Run(CPU cpu)
	{
		XArrayGeneric a = PopInstance(cpu);
		cpu.Push(!a.IsUninitialized);
	}

	internal override CCNode Enter(CCStack stack,
		CCNodeEntry parent, CCNode next)
	{
		CCNode node = new CCNodeNativeArrayIsInit(this, owner, next);
		node.MergeStack(stack);
		return node;
	}
}

class NativeArrayAccessorLength : NativeArrayAccessor {

	internal NativeArrayAccessorLength(XType owner)
		: base(owner, "std::length")
	{
	}

	internal override void Run(CPU cpu)
	{
		XArrayGeneric a = PopInstance(cpu);
		cpu.Push(XValue.MakeInt(a.Length));
	}

	internal override CCNode Enter(CCStack stack,
		CCNodeEntry parent, CCNode next)
	{
		CCNode node = new CCNodeNativeArrayLength(this, owner, next);
		node.MergeStack(stack);
		return node;
	}
}

class NativeArrayAccessorGet : NativeArrayAccessor {

	internal NativeArrayAccessorGet(XType owner)
		: base(owner, "std::@")
	{
	}

	internal override void Run(CPU cpu)
	{
		XArrayGeneric a = PopInstance(cpu);
		int k = cpu.Pop().Int;
		cpu.Push(a[k]);
	}

	internal override CCNode Enter(CCStack stack,
		CCNodeEntry parent, CCNode next)
	{
		CCNode node = new CCNodeNativeArrayGet(this, owner, next);
		node.MergeStack(stack);
		return node;
	}
}

class NativeArrayAccessorPut : NativeArrayAccessor {

	internal NativeArrayAccessorPut(XType owner)
		: base(owner, "std::->@")
	{
	}

	internal override void Run(CPU cpu)
	{
		XArrayGeneric a = PopInstance(cpu);
		int k = cpu.Pop().Int;
		XValue v = cpu.Pop();
		a[k] = v;
	}

	internal override CCNode Enter(CCStack stack,
		CCNodeEntry parent, CCNode next)
	{
		CCNode node = new CCNodeNativeArrayPut(this, owner, next);
		node.MergeStack(stack);
		return node;
	}
}

class NativeArrayAccessorClear : NativeArrayAccessor {

	internal NativeArrayAccessorClear(XType owner)
		: base(owner, "std::Z->@")
	{
	}

	internal override void Run(CPU cpu)
	{
		XArrayGeneric a = PopInstance(cpu);
		int k = cpu.Pop().Int;
		a.Clear(k);
	}

	internal override CCNode Enter(CCStack stack,
		CCNodeEntry parent, CCNode next)
	{
		CCNode node = new CCNodeNativeArrayClear(this, owner, next);
		node.MergeStack(stack);
		return node;
	}
}

class NativeArrayAccessorIsEltInit : NativeArrayAccessor {

	internal NativeArrayAccessorIsEltInit(XType owner)
		: base(owner, "std::@?")
	{
	}

	internal override void Run(CPU cpu)
	{
		XArrayGeneric a = PopInstance(cpu);
		int k = cpu.Pop().Int;
		cpu.Push(!a.IsElementUninitialized(k));
	}

	internal override CCNode Enter(CCStack stack,
		CCNodeEntry parent, CCNode next)
	{
		CCNode node = new CCNodeNativeArrayIsEltInit(this, owner, next);
		node.MergeStack(stack);
		return node;
	}
}

class NativeArrayAccessorRef : NativeArrayAccessor {

	internal NativeArrayAccessorRef(XType owner)
		: base(owner, "std::@&")
	{
	}

	internal override void Run(CPU cpu)
	{
		XArrayGeneric a = PopInstance(cpu);
		int k = cpu.Pop().Int;
		cpu.Push(a[k]);
	}

	internal override CCNode Enter(CCStack stack,
		CCNodeEntry parent, CCNode next)
	{
		CCNode node = new CCNodeNativeArrayRef(this, owner, next);
		node.MergeStack(stack);
		return node;
	}
}

/* ======================================================================= */
