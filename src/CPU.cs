using System;
using System.Collections.Generic;

/*
 * A virtual CPU for interpreting code.
 */

class CPU {

	/*
	 * The current interpreter. This throws an exception if there is
	 * no current interpreter (i.e. the CPU instance was created out
	 * of an interpretation context).
	 */
	internal Interpreter Interpreter {
		get {
			if (interp == null) {
				throw new Exception("no interpreter context");
			}
			return interp;
		}
		private set {
			interp = value;
		}
	}

	Interpreter interp;

	/*
	 * Current instruction pointer. At any time, ipBuf[ipOff] should
	 * be the next instruction to execute.
	 */
	internal Opcode[] ipBuf;
	internal int ipOff;

	/*
	 * Data stack.
	 * Values on the stack are normally initialized. An uninitialized
	 * value may be pushed as a "marker" for nested contexts; trying
	 * to pop that value triggers an exception. Markers form a link
	 * list: each marker embeds the index of the previous marker in
	 * the array.
	 */
	XValue[] stackBuf;
	int stackPtr;
	int topMarker;

	/*
	 * System stack, as Frame objects; each Frame instance has a
	 * link to the previous one. The rsp field points to the current
	 * frame, i.e. the top-of-stack.
	 */
	Frame rsp;

	internal CPU()
		: this(null)
	{
	}

	internal CPU(Interpreter interp)
	{
		stackBuf = new XValue[16];
		stackPtr = -1;
		topMarker = -1;
		rsp = null;
		this.interp = interp;
	}

	/*
	 * Enter a given function. A new frame is allocated, with the
	 * provided number of local variables. The current IP is saved
	 * in the new frame, to be restored upon function exit.
	 */
	internal void Enter(Opcode[] code, int numLocalFields,
		XType[] localEmbedTypes)
	{
		Frame f = new Frame(rsp, numLocalFields, localEmbedTypes);
		rsp = f;
		f.savedIpBuf = ipBuf;
		f.savedIpOff = ipOff;
		ipBuf = code;
		ipOff = 0;
	}

	/*
	 * Exit the current function.
	 */
	internal void Exit()
	{
		ipBuf = rsp.savedIpBuf;
		ipOff = rsp.savedIpOff;
		rsp = rsp.upper;
	}

	/*
	 * Test whether execution is finished (i.e. the last frame was
	 * popped).
	 */
	internal bool IsFinished {
		get {
			return rsp == null;
		}
	}

	/*
	 * Get data stack depth (number of elements on the stack, 0 for
	 * an empty stack). The scan for the stack depth stops at the
	 * topmost marker.
	 */
	internal int Depth {
		get {
			return stackPtr - topMarker;
		}
	}

	/*
	 * Pop a value from the data stack.
	 */
	internal XValue Pop()
	{
		if (stackPtr <= topMarker) {
			throw new Exception("stack underflow");
		}
		XValue v = stackBuf[stackPtr];
		stackBuf[stackPtr].Clear();
		stackPtr --;
		return v;
	}

	/*
	 * Push a value on the data stack.
	 */
	internal void Push(XValue v)
	{
		int len = stackBuf.Length;
		if (++ stackPtr == len) {
			XValue[] nbuf = new XValue[len << 1];
			Array.Copy(stackBuf, 0, nbuf, 0, len);
			stackBuf = nbuf;
		}
		stackBuf[stackPtr] = v;
	}

	/*
	 * Peek a value at the specified depth. Depth zero is the
	 * top-of-stack.
	 */
	internal XValue Peek(int depth)
	{
		if (stackPtr <= topMarker + depth) {
			throw new Exception("stack underflow");
		}
		return stackBuf[stackPtr - depth];
	}

	/*
	 * Rotate the stack: the value at the specified depth is
	 * moved to the top-of-stack:
	 *
	 *   depth = 0     does nothing (but checks that there is a TOS)
	 *   depth = 1     swap the two top stack elements
	 *   depth = 2     a b c -> b c a  (the "rot" word in Forth)
	 */
	internal void Rot(int depth)
	{
		if (stackPtr <= (topMarker + depth) || depth < 0) {
			throw new Exception("stack underflow");
		}
		if (depth == 0) {
			return;
		}
		XValue v = stackBuf[stackPtr - depth];
		Array.Copy(stackBuf, stackPtr - (depth - 1),
			stackBuf, stackPtr - depth, depth);
		stackBuf[stackPtr] = v;
	}

	/*
	 * Inverse rotate the data stack. NRot(depth) undoes the effect
	 * of Rot(depth).
	 */
	internal void NRot(int depth)
	{
		if (stackPtr <= (topMarker + depth) || depth < 0) {
			throw new Exception("stack underflow");
		}
		if (depth == 0) {
			return;
		}
		XValue v = stackBuf[stackPtr];
		Array.Copy(stackBuf, stackPtr - depth,
			stackBuf, stackPtr - (depth - 1), depth);
		stackBuf[stackPtr - depth] = v;
	}

	/*
	 * Push a marker onto the stack.
	 */
	internal void PushMarker()
	{
		Push(XValue.MakeMarker(topMarker));
		topMarker = stackPtr;
	}

	/*
	 * Pop all stack contents, down to the topmost marker. This throws
	 * an exception if there is no marker.
	 */
	internal XValue[] PopToMarker()
	{
		if (topMarker < 0) {
			throw new Exception("no marker found in stack");
		}
		int d = Depth;
		XValue[] r = new XValue[d];
		Array.Copy(stackBuf, stackPtr - d + 1, r, 0, d);
		topMarker = stackBuf[topMarker].GetMarkerContents();
		if (topMarker == Int32.MinValue) {
			throw new Exception("internal error (stack markers)");
		}
		for (int i = 0; i < d; i ++) {
			stackBuf[stackPtr - i].Clear();
		}
		stackPtr -= d + 1;
		return r;
	}

	/*
	 * Read a local variable (by index).
	 */
	internal XValue GetLocal(int num)
	{
		XValue v = rsp.localFields[num];
		if (v.IsUninitialized) {
			throw new Exception(string.Format("reading uninitialized local value {0}", num));
		}
		return v;
	}

	/*
	 * Write a value into a local variable (by index).
	 */
	internal void PutLocal(int num, XValue v)
	{
		rsp.localFields[num] = v;
	}

	/*
	 * Get the address of a local variable (by index).
	 */
	internal XValue RefLocal(int num)
	{
		return new XValue(rsp.localEmbeds[num]);
	}

	/*
	 * A Frame instance represents the activation context of a
	 * function. It contains a reference to the upper frame, the
	 * saved value of IP, and the local variables for this frame.
	 */
	class Frame {

		internal Frame upper;
		internal Opcode[] savedIpBuf;
		internal int savedIpOff;
		internal XValue[] localFields;
		internal XObject[] localEmbeds;

		internal Frame(Frame upper,
			int numLocalFields, XType[] localEmbedTypes)
		{
			this.upper = upper;
			if (numLocalFields > 0) {
				localFields = new XValue[numLocalFields];
			}
			if (localEmbedTypes != null) {
				int n = localEmbedTypes.Length;
				if (n > 0) {
					localEmbeds = new XObject[n];
					for (int i = 0; i < n; i ++) {
						localEmbeds[i] =
							localEmbedTypes[i]
							.NewInstance();
					}
				}
			}
		}
	}
}
