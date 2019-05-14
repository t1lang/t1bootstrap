using System;
using System.Collections.Generic;

/*
 * Interpreted code is made of Opcode instances (that is, instances of
 * classes that extend the abstract class Opcode).
 */

abstract class Opcode {

	internal Opcode()
	{
	}

	/*
	 * Apply the effect of this opcode.
	 */
	internal abstract void Run(CPU cpu);

	/*
	 * If this opcode is a jump, resolve it with the provided
	 * displacement value (displacement is counted in opcodes,
	 * and added to the IP when the jump is taken; a displacement
	 * of zero makes the jump a NOP).
	 */
	internal virtual void ResolveJump(int disp)
	{
		throw new Exception("Not a jump opcode");
	}

	/*
	 * Test whether a given opcode may "fall through", i.e. be
	 * succeeded by the opcode immediately afterwards.
	 */
	internal virtual bool MayFallThrough {
		get {
			return true;
		}
	}

	/*
	 * Perform tree-building on the provided node.
	 */
	internal abstract void BuildTree(CCNodeOpcode node);

	/*
	 * Specialize the opcode for code generation.
	 */
	internal abstract GOp ToGOp(CCNodeOpcode node);

	/*
	 * Get a string representation of this opcode, assuming that
	 * it appears at the provided address within a function (this
	 * is used by branch opcodes to compute the absolute target
	 * address).
	 */
	internal virtual string ToString(int addr)
	{
		return ToString();
	}
}

/*
 * Opcode for a function call (by name).
 */

class OpcodeCall : Opcode {

	string fname;

	internal OpcodeCall(string fname)
	{
		this.fname = fname;
	}

	internal override void Run(CPU cpu)
	{
		Function.Lookup(fname, cpu).Run(cpu);
	}

	internal override void BuildTree(CCNodeOpcode node)
	{
		node.DoCall(fname);
	}

	internal override GOp ToGOp(CCNodeOpcode node)
	{
		return node.ToGOpCall(fname);
	}

	public override string ToString()
	{
		return "call " + fname;
	}
}

/*
 * Opcode for pushing a literal value on the data stack.
 */

class OpcodeConst : Opcode {

	XValue v;

	internal OpcodeConst(XValue v)
	{
		this.v = v;
	}

	internal override void Run(CPU cpu)
	{
		cpu.Push(v);
	}

	internal override void BuildTree(CCNodeOpcode node)
	{
		node.DoConst(v);
	}

	internal override GOp ToGOp(CCNodeOpcode node)
	{
		return node.ToGOpConst(v);
	}

	public override string ToString()
	{
		return "const " + v.ToString();
	}
}

/*
 * Opcode for reading a local variable: the variable contents are pushed
 * on the data stack. The local variable index in the current frame is
 * provided at construction time.
 */

class OpcodeGetLocal : Opcode {

	int num;

	internal OpcodeGetLocal(int num)
	{
		this.num = num;
	}

	internal override void Run(CPU cpu)
	{
		cpu.Push(cpu.GetLocal(num));
	}

	internal override void BuildTree(CCNodeOpcode node)
	{
		node.DoGetLocal(num);
	}

	internal override GOp ToGOp(CCNodeOpcode node)
	{
		return node.ToGOpGetLocal(num);
	}

	public override string ToString()
	{
		return "getlocal " + num;
	}
}

/*
 * Opcode for reading a local value by dynamic index. The index is
 * acquired from the stack, and verified to lie within the specified range.
 */

class OpcodeGetLocalIndexed : Opcode {

	int off, len;

	internal OpcodeGetLocalIndexed(int off, int len)
	{
		this.off = off;
		this.len = len;
	}

	internal override void Run(CPU cpu)
	{
		int k = cpu.Pop().Int;
		if (k < 0 || k >= len) {
			throw new Exception(string.Format("local index out of bounds: {0} (max: {1})", k, len));
		}
		cpu.Push(cpu.GetLocal(off + k));
	}

	internal override void BuildTree(CCNodeOpcode node)
	{
		node.DoGetLocalIndexed(off, len);
	}

	internal override GOp ToGOp(CCNodeOpcode node)
	{
		return node.ToGOpGetLocalIndexed(off, len);
	}

	public override string ToString()
	{
		return string.Format("getlocalindex ({0},{1})", off, len);
	}
}

/*
 * Common abstract class for jump opcodes.
 */

abstract class OpcodeJump : Opcode {

	internal int JumpDisp { get; private set; }

	internal OpcodeJump() : this(Int32.MinValue)
	{
	}

	internal OpcodeJump(int disp)
	{
		this.JumpDisp = disp;
	}

	internal override void Run(CPU cpu)
	{
		cpu.ipOff += JumpDisp;
	}

	internal override void ResolveJump(int disp)
	{
		if (this.JumpDisp != Int32.MinValue) {
			throw new Exception("Jump already resolved");
		}
		this.JumpDisp = disp;
	}
}

/*
 * OpcodeJumpIf reads a boolean value from the data stack, and then
 * performs a jump if and only if the boolean value is true.
 */

class OpcodeJumpIf : OpcodeJump {

	internal OpcodeJumpIf() : base()
	{
	}

	internal OpcodeJumpIf(int disp) : base(disp)
	{
	}

	internal override void Run(CPU cpu)
	{
		XValue v = cpu.Pop();
		if (v.Bool) {
			base.Run(cpu);
		}
	}

	internal override void BuildTree(CCNodeOpcode node)
	{
		node.DoJumpIf(JumpDisp);
	}

	internal override GOp ToGOp(CCNodeOpcode node)
	{
		return node.ToGOpJumpIf(JumpDisp);
	}

	public override string ToString()
	{
		if (JumpDisp == Int32.MinValue) {
			return "jumpif UNRESOLVED";
		} else {
			return "jumpif disp=" + JumpDisp;
		}
	}

	internal override string ToString(int addr)
	{
		if (JumpDisp == Int32.MinValue) {
			return "jumpif UNRESOLVED";
		} else {
			return "jumpif " + (addr + 1 + JumpDisp);
		}
	}
}

/*
 * OpcodeJumpIf reads a boolean value from the data stack, and then
 * performs a jump if and only if the boolean value is false.
 */

class OpcodeJumpIfNot : OpcodeJump {

	internal OpcodeJumpIfNot() : base()
	{
	}

	internal OpcodeJumpIfNot(int disp) : base(disp)
	{
	}

	internal override void Run(CPU cpu)
	{
		XValue v = cpu.Pop();
		if (!v.Bool) {
			base.Run(cpu);
		}
	}

	internal override void BuildTree(CCNodeOpcode node)
	{
		node.DoJumpIfNot(JumpDisp);
	}

	internal override GOp ToGOp(CCNodeOpcode node)
	{
		return node.ToGOpJumpIfNot(JumpDisp);
	}

	public override string ToString()
	{
		if (JumpDisp == Int32.MinValue) {
			return "jumpifnot UNRESOLVED";
		} else {
			return "jumpifnot disp=" + JumpDisp;
		}
	}

	internal override string ToString(int addr)
	{
		if (JumpDisp == Int32.MinValue) {
			return "jumpif UNRESOLVED";
		} else {
			return "jumpifnot " + (addr + 1 + JumpDisp);
		}
	}
}

/*
 * OpcodeJumpUncond is an unconditional jump.
 */

class OpcodeJumpUncond : OpcodeJump {

	internal OpcodeJumpUncond() : base()
	{
	}

	internal OpcodeJumpUncond(int disp) : base(disp)
	{
	}

	internal override bool MayFallThrough {
		get {
			return JumpDisp != 0;
		}
	}

	internal override void BuildTree(CCNodeOpcode node)
	{
		node.DoJumpUncond(JumpDisp);
	}

	internal override GOp ToGOp(CCNodeOpcode node)
	{
		return node.ToGOpJumpUncond(JumpDisp);
	}

	public override string ToString()
	{
		if (JumpDisp == Int32.MinValue) {
			return "jump UNRESOLVED";
		} else {
			return "jump disp=" + JumpDisp;
		}
	}

	internal override string ToString(int addr)
	{
		if (JumpDisp == Int32.MinValue) {
			return "jump UNRESOLVED";
		} else {
			return "jump " + (addr + 1 + JumpDisp);
		}
	}
}

/*
 * Opcode for writing into a local variable: the value on top of the
 * data stack is popped, and written into the variable. The local
 * variable index in the current frame is provided at construction time.
 * The local type is also provided: attempts at writing an incompatible
 * value will fail.
 */

class OpcodePutLocal : Opcode {

	int num;
	XType ltype;

	/*
	 * If ltype is null, then this has the same effect as setting
	 * it to XType.OBJECT: all values are allowed.
	 */
	internal OpcodePutLocal(int num, XType ltype)
	{
		this.num = num;
		this.ltype = ltype;
	}

	internal override void Run(CPU cpu)
	{
		XValue xv = cpu.Pop();
		if (ltype != null && !xv.VType.IsSubTypeOf(ltype)) {
			throw new Exception(string.Format("write of value of type {0} into a local variable of type {1}", xv.VType.Name, ltype.Name));
		}
		cpu.PutLocal(num, xv);
	}

	internal override void BuildTree(CCNodeOpcode node)
	{
		node.DoPutLocal(num, ltype);
	}

	internal override GOp ToGOp(CCNodeOpcode node)
	{
		return node.ToGOpPutLocal(num, ltype);
	}

	public override string ToString()
	{
		return string.Format("putlocal {0} ({1})",
			num, ltype == null ? "std::object" : ltype.Name);
	}
}

/*
 * Opcode for writing a local value by dynamic index. The index is
 * acquired from the stack, and verified to lie within the specified range.
 * The local type is also provided: attempts at writing an incompatible
 * value will fail.
 */

class OpcodePutLocalIndexed : Opcode {

	int off, len;
	XType ltype;

	/*
	 * If ltype is null, then this has the same effect as setting
	 * it to XType.OBJECT: all values are allowed.
	 */
	internal OpcodePutLocalIndexed(int off, int len, XType ltype)
	{
		this.off = off;
		this.len = len;
		this.ltype = ltype;
	}

	internal override void Run(CPU cpu)
	{
		int k = cpu.Pop().Int;
		if (k < 0 || k >= len) {
			throw new Exception(string.Format("local index out of bounds: {0} (max: {1})", k, len));
		}
		XValue xv = cpu.Pop();
		if (ltype != null && !xv.VType.IsSubTypeOf(ltype)) {
			throw new Exception(string.Format("write of value of type {0} into a local variable of type {1}", xv.VType.Name, ltype.Name));
		}
		cpu.PutLocal(off + k, xv);
	}

	internal override void BuildTree(CCNodeOpcode node)
	{
		node.DoPutLocalIndexed(off, len, ltype);
	}

	internal override GOp ToGOp(CCNodeOpcode node)
	{
		return node.ToGOpPutLocalIndexed(off, len, ltype);
	}

	public override string ToString()
	{
		return string.Format("putlocalindex ({0},{1}) ({2})",
			off, len, ltype == null ? "std::object" : ltype.Name);
	}
}

/*
 * Opcode for obtaining the address of a local embedded object.
 * The reference to the object is pushed on the data stack. The local
 * object offest is provided at construction time.
 */

class OpcodeRefLocal : Opcode {

	int num;

	internal OpcodeRefLocal(int num)
	{
		this.num = num;
	}

	internal override void Run(CPU cpu)
	{
		cpu.Push(cpu.RefLocal(num));
	}

	internal override void BuildTree(CCNodeOpcode node)
	{
		node.DoRefLocal(num);
	}

	internal override GOp ToGOp(CCNodeOpcode node)
	{
		return node.ToGOpRefLocal(num);
	}

	public override string ToString()
	{
		return "reflocal " + num;
	}
}

/*
 * Opcode for obtaining the address of a local embedded object, by
 * dynamic index; the index is verified to lie within the specified range.
 */

class OpcodeRefLocalIndexed : Opcode {

	int off, len;

	internal OpcodeRefLocalIndexed(int off, int len)
	{
		this.off = off;
		this.len = len;
	}

	internal override void Run(CPU cpu)
	{
		int k = cpu.Pop().Int;
		if (k < 0 || k >= len) {
			throw new Exception(string.Format("local index out of bounds: {0} (max: {1})", k, len));
		}
		cpu.Push(cpu.RefLocal(off + k));
	}

	internal override void BuildTree(CCNodeOpcode node)
	{
		node.DoRefLocalIndexed(off, len);
	}

	internal override GOp ToGOp(CCNodeOpcode node)
	{
		return node.ToGOpRefLocalIndexed(off, len);
	}

	public override string ToString()
	{
		return string.Format("reflocalindex ({0},{1})", off, len);
	}
}

/*
 * Opcode for returning from a function. This restores the saved IP, and
 * removes the current frame.
 */

class OpcodeRet : Opcode {

	internal OpcodeRet()
	{
	}

	internal override void Run(CPU cpu)
	{
		cpu.Exit();
	}

	public override string ToString()
	{
		return "ret";
	}

	internal override void BuildTree(CCNodeOpcode node)
	{
		node.DoRet();
	}

	internal override GOp ToGOp(CCNodeOpcode node)
	{
		return node.ToGOpRet();
	}

	internal override bool MayFallThrough {
		get {
			return false;
		}
	}
}

/*
 * Opcode for invoking a piece of "native" code (i.e. a C# routine).
 * These opcodes cannot be converted into executable code by the
 * compiler.
 */

class OpcodeSpecial : Opcode {

	internal delegate void NativeRun(CPU cpu);

	NativeRun code;

	internal OpcodeSpecial(NativeRun code)
	{
		this.code = code;
	}

	internal override void BuildTree(CCNodeOpcode node)
	{
		throw new Exception("special interpreter-only opcode encountered");
	}

	internal override GOp ToGOp(CCNodeOpcode node)
	{
		throw new Exception("special interpreter-only opcode encountered");
	}

	internal override void Run(CPU cpu)
	{
		code(cpu);
	}
}
