using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/*
 * A CCNode instance is a node in the execution tree. Each opcode in an
 * interpreted function is a node. Each primitive function is a node.
 */

abstract class CCNode : IComparable<CCNode> {

	static ulong currentSerial = 0;

	internal ulong Serial {
		get; private set;
	}

	/*
	 * Stack element types upon entry of this node.
	 */
	internal CCStack Stack {
		get; private set;
	}

	/*
	 * Update flag: when set, the node must be analysed (again)
	 * because it is a new node, or its entry conditions have
	 * changed.
	 */
	bool update;

	internal CCNode()
	{
		Serial = currentSerial ++;
	}

	internal void MergeStack(CCStack stack2)
	{
		if (Stack == null) {
			Stack = stack2;
			MarkUpdate();
		} else {
			CCStack nstack = CCStack.Merge(Stack, stack2);
			if (nstack != Stack) {
				Stack = nstack;
				MarkUpdate();
			}
		}
	}

	/*
	 * Mark this node as needed exploration, but only if a non-null
	 * entry stack has been defined.
	 */
	internal void MarkUpdate()
	{
		if (!update && Stack != null) {
			update = true;
			TO_EXPLORE.Add(this);
		}
	}

	internal abstract void Walk();

	public int CompareTo(CCNode other)
	{
		return Serial.CompareTo(other.Serial);
	}

	public override bool Equals(object other)
	{
		CCNode ccn = other as CCNode;
		if (ccn == null) {
			return false;
		}
		return CompareTo(ccn) == 0;
	}

	public override int GetHashCode()
	{
		return (int)Serial;
	}

	public override string ToString()
	{
		return Serial.ToString();
	}

	static List<CCNode> TO_EXPLORE = new List<CCNode>();

	internal static CCNode BuildTree(Function f, params CCTypeSet[] args)
	{
		CCStack stack = new CCStack();
		foreach (CCTypeSet cts in args) {
			stack = stack.Push(cts);
		}
		CCNode start = f.Enter(stack, null, new CCNodeExit());
		for (;;) {
			int n = TO_EXPLORE.Count;
			if (n == 0) {
				break;
			}
			CCNode node = TO_EXPLORE[n - 1];
			TO_EXPLORE.RemoveAt(n - 1);
			node.update = false;
			node.Walk();
		}
		return start;
	}

	internal virtual void Print(TextWriter tw, int indent)
	{
		Compiler.Indent(tw, indent);
		tw.WriteLine(ToString());
	}

	internal virtual GFunction ToGFunction()
	{
		throw new Exception(string.Format("cannot generate a function for node {0}", this));
	}
}

/*
 * Special node for program termination.
 */

class CCNodeExit : CCNode {

	internal override void Walk()
	{
		/*
		 * Upon exit, the stack should be empty.
		 */
		int n = Stack.Depth;
		if (n != 0) {
			throw new Exception(string.Format("non-empty stack upon exit: {0} element{1}", n, (n > 1) ? "s" : ""));
		}
	}

	public override string ToString()
	{
		return string.Format("{0} EXIT", base.ToString());
	}
}

/*
 * A CCFunctionInterpreted incarnates a function execution within the
 * tree. Several CCFunctionInterpreted instances may exist for the same
 * underlying FunctionInterpreted.
 */
class CCFunctionInterpreted {

	/*
	 * The function itself.
	 */
	internal FunctionInterpreted fi;

	/*
	 * Cache on nodes for this function execution.
	 */
	internal CCNodeOpcode[] nodes;

	/*
	 * The node to which execution jumps when this function returns.
	 */
	internal CCNode next;

	internal CCNodeEntry Entry {
		get {
			return (CCNodeEntry)nodes[0];
		}
		set {
			nodes[0] = value;
		}
	}

	internal CCFunctionInterpreted(FunctionInterpreted fi, CCNode next)
	{
		this.fi = fi;
		nodes = new CCNodeOpcode[fi.CodeLength];
		this.next = next;
	}

	internal CCNodeOpcode GetNode(int addr)
	{
		return nodes[addr];
	}

	internal void SetNode(int addr, CCNodeOpcode node)
	{
		nodes[addr] = node;
	}

	internal void BuildTree(int addr, CCNodeOpcode node)
	{
		fi.BuildTree(addr, node);
	}

	internal GOp ToGOp(int addr)
	{
		CCNodeOpcode node = nodes[addr];
		if (node.Stack == null) {
			GOp g = new GOpZero();
			g.addr = addr;
			return g;
		}
		return fi.ToGOp(addr, node);
	}
}

/*
 * A CCNodeOpcode instance corresponds to an opcode in the execution of
 * an interpreted function.
 */

class CCNodeOpcode : CCNode {

	internal CCFunctionInterpreted cfi;
	internal int addr;

	/*
	 * Locals upon entry of this node.
	 */
	internal CCLocals Locals {
		get; private set;
	}

	/*
	 * Nodes to which this node may lead; initially null, and updated
	 * as the tree is walked repeatedly.
	 *
	 *   next       set to the next node in execution order
	 *   branch     target for jumps
	 *   dispatch   targets for function call
	 *   calls      mapping stack -> function
	 */
	CCNodeOpcode next;
	CCNodeOpcode branch;
	SortedDictionary<Function, CCNode> dispatch;
	List<DCall> calls;

	internal CCNodeOpcode(CCFunctionInterpreted cfi, int addr)
		: base()
	{
		this.cfi = cfi;
		this.addr = addr;
	}

	internal void MergeLocals(CCLocals locals2)
	{
		if (Locals == null) {
			Locals = locals2;
			MarkUpdate();
		} else {
			CCLocals nlocals = CCLocals.Merge(Locals, locals2);
			if (Locals != nlocals) {
				Locals = nlocals;
				MarkUpdate();
			}
		}
	}

	internal override void Walk()
	{
		cfi.BuildTree(addr, this);
	}

	internal CCNodeOpcode GetNode(int off)
	{
		CCNodeOpcode node = cfi.GetNode(off);
		if (node == null) {
			node = new CCNodeOpcode(cfi, off);
			cfi.SetNode(off, node);
		}
		return node;
	}

	internal CCNodeOpcode Propagate(int addr2, CCNodeOpcode node,
		CCStack nstack, CCLocals nlocals)
	{
		if (node == null) {
			node = GetNode(addr2);
		}
		node.MergeStack(nstack);
		node.MergeLocals(nlocals);
		return node;
	}

	internal void PropagateNext(CCStack nstack)
	{
		PropagateNext(nstack, Locals);
	}

	internal void PropagateNext(CCLocals nlocals)
	{
		PropagateNext(Stack, nlocals);
	}

	internal void PropagateNext(CCStack nstack, CCLocals nlocals)
	{
		next = Propagate(addr + 1, next, nstack, nlocals);
	}

	internal void PropagateBranch(int disp)
	{
		PropagateBranch(disp, Stack, Locals);
	}

	internal void PropagateBranch(int disp, CCStack nstack)
	{
		PropagateBranch(disp, nstack, Locals);
	}

	internal void PropagateBranch(int disp, CCLocals nlocals)
	{
		PropagateBranch(disp, Stack, nlocals);
	}

	internal void PropagateBranch(int disp,
		CCStack nstack, CCLocals nlocals)
	{
		branch = Propagate(addr + 1 + disp, branch, nstack, nlocals);
	}

	internal void DoCall(string name)
	{
		/*
		 * Make sure the next node exists. We also set its locals
		 * to the current locals (the called function cannot
		 * change our locals). Note that the stack is not set;
		 * this will be done only when (if) one of the called
		 * functions returns.
		 */
		if (next == null) {
			next = GetNode(addr + 1);
		}
		next.MergeLocals(Locals);

		/*
		 * Find all functions registered with the specified name.
		 * If none is found, then we want a specific error message
		 * (this is probably a typographic error in the function
		 * name).
		 */
		List<FunctionRegistration> r1 = Function.LookupAll(name);
		if (r1.Count == 0) {
			throw new Exception(string.Format("no such function: {0}", name));
		}

		/*
		 * Find all functions that may match the types on the
		 * stack.
		 */
		List<FunctionRegistration> r2 =
			new List<FunctionRegistration>();
		int maxNum = 0;
		foreach (FunctionRegistration fr in r1) {
			if (Stack.MayMatch(fr.types)) {
				r2.Add(fr);
				maxNum = Math.Max(maxNum, fr.types.Length);
			}
		}

		/*
		 * For all combinations of types that may occur on the
		 * stack, try to resolve the call; if any fails, then
		 * report it as an error.
		 * While doing so, we accumulate, for each reachable
		 * function, the types that may appear on entry of
		 * that function.
		 */
		CCType[] combo = new CCType[maxNum];
		CCTypeSet.ComboEnumerator ce = new CCTypeSet.ComboEnumerator(
			Stack.GetTopElements(maxNum));
		XType[] xts = new XType[maxNum];
		SortedDictionary<Function, CCStack> targets =
			new SortedDictionary<Function, CCStack>();
		CCStack root = Stack.Pop(maxNum);
		GStack gtail = GStack.Make(root);
		calls = new List<DCall>();
		for (;;) {
			if (!ce.Next(combo)) {
				break;
			}
			for (int i = 0; i < maxNum; i ++) {
				xts[i] = combo[i].xType;
			}
			Function f = Function.Resolve(name, r2, xts);
			CCStack nstack = root.Push(combo);
			CCStack ostack;
			if (targets.TryGetValue(f, out ostack)) {
				targets[f] = CCStack.Merge(ostack, nstack);
			} else {
				targets[f] = nstack;
			}
			calls.Add(new DCall(GStack.Make(xts, gtail), f));
		}

		/*
		 * We now have a complete list of reachable functions,
		 * each with a corresponding entry stack; we know that
		 * the call will always resolve to a single function. If
		 * we are updating, we reuse the previous functions from
		 * the dispatch variable if available. Note that an update
		 * can only add new functions, not remove existing ones.
		 */
		if (dispatch == null) {
			dispatch = new SortedDictionary<Function, CCNode>();
		}
		foreach (Function f in targets.Keys) {
			CCStack stack = targets[f];
			CCNode onode;
			if (dispatch.TryGetValue(f, out onode)) {
				onode.MergeStack(stack);
			} else {
				dispatch[f] = f.Enter(stack, cfi.Entry, next);
			}
		}
	}

	internal void DoConst(XValue v)
	{
		/*
		 * A constant value will point to a statically allocated
		 * instance, hence constant. However, for basic types,
		 * the instances are virtual, and we do not want to tag
		 * them with the "constant" flag to avoid duplicating
		 * the types in the analysis.
		 */
		XType xt = v.VType;
		CCType ct;
		if (xt.IsBasic) {
			ct = new CCType(xt);
		} else {
			ct = new CCType(xt, null, 0, true);
		}
		CCValues.AddValue(v);
		PropagateNext(Stack.Push(new CCTypeSet(ct)));
	}

	internal void DoGetLocal(int num)
	{
		int index = cfi.fi.fieldInverseMapping[num];
		CCStack nstack = Stack.Push(Locals.Get(index));
		PropagateNext(nstack);
	}

	internal void DoGetLocalIndexed(int off, int len)
	{
		Stack.Peek(0).CheckInt();
		int index = cfi.fi.fieldInverseMapping[off];
		CCStack nstack = Stack.Pop().Push(Locals.Get(index));
		PropagateNext(nstack);
	}

	internal void DoJumpIf(int disp)
	{
		/*
		 * Check that the top stack element is std::bool.
		 * Remove it for the successor opcodes.
		 */
		CCTypeSet cts = Stack.Peek(0);
		cts.CheckBool();
		CCStack nstack = Stack.Pop();
		PropagateNext(nstack);
		PropagateBranch(disp, nstack);
	}

	internal void DoJumpIfNot(int disp)
	{
		/*
		 * From a type analysis point of view, JumpIf and
		 * JumpIfNot are equivalent.
		 */
		DoJumpIf(disp);
	}

	internal void DoJumpUncond(int disp)
	{
		PropagateBranch(disp);
	}

	internal void DoPutLocal(int num, XType ltype)
	{
		int index = cfi.fi.fieldInverseMapping[num];
		CCTypeSet cts = Stack.Peek(0);
		if (ltype != null) {
			cts.CheckSubTypeOf(ltype);
		}
		PropagateNext(Stack.Pop(), Locals.Set(index, cts));
	}

	internal void DoPutLocalIndexed(int off, int len, XType ltype)
	{
		int index = cfi.fi.fieldInverseMapping[off];
		Stack.Peek(0).CheckInt();
		CCTypeSet cts = Stack.Peek(1);
		if (ltype != null) {
			cts.CheckSubTypeOf(ltype);
		}
		PropagateNext(Stack.Pop().Pop(), Locals.Set(index, cts));
	}

	internal void DoRefLocal(int num)
	{
		XType xt = cfi.fi.localEmbedTypes[num];
		CCType ct = new CCType(xt, cfi.Entry, num, false);
		PropagateNext(Stack.Push(new CCTypeSet(ct)));
	}

	internal void DoRefLocalIndexed(int off, int len)
	{
		Stack.Peek(0).CheckInt();
		XType xt = cfi.fi.localEmbedTypes[off];
		CCType ct = new CCType(xt, cfi.Entry, off, false);
		PropagateNext(Stack.Pop().Push(new CCTypeSet(ct)));
	}

	internal void DoRet()
	{
		/*
		 * Check that the stack does not contain any reference
		 * to an instance locally allocated in this function.
		 * TODO: when per-function stack use is computed,
		 * optimize this code to avoid scanning the whole stack.
		 */
		Stack.CheckAllocNode(cfi.Entry);

		cfi.next.MergeStack(Stack);
	}

	internal GOp ToGOpCall(string fname)
	{
		SortedDictionary<Function, GFunction> cfm =
			new SortedDictionary<Function, GFunction>();
		foreach (var kvp in dispatch) {
			Function f = kvp.Key;
			CCNode node = kvp.Value;
			cfm[f] = node.ToGFunction();
		}

		Dispatcher d = Dispatcher.Make(calls);
		if (d == null) {
			throw new Exception(string.Format("cannot make dispatcher for call to {0} from {1}", fname, cfi.fi.DebugName));
		}

		return new GOpCall(fname, d, cfm);
	}

	internal GOp ToGOpConst(XValue v)
	{
		return new GOpConst(v);
	}

	internal GOp ToGOpGetLocal(int off)
	{
		int index = cfi.fi.fieldInverseMapping[off];
		XType ltype = Locals.Get(index).GetRestricted();
		if (ltype == null) {
			ltype = XType.OBJECT;
		}
		return new GOpGetLocal(off, ltype);
	}

	internal GOp ToGOpGetLocalIndexed(int off, int len)
	{
		int index = cfi.fi.fieldInverseMapping[off];
		XType ltype = Locals.Get(index).GetRestricted();
		if (ltype == null) {
			ltype = XType.OBJECT;
		}
		return new GOpGetLocalIndexed(off, len, ltype);
	}

	internal GOp ToGOpJumpIf(int disp)
	{
		return new GOpJumpIf(disp);
	}

	internal GOp ToGOpJumpIfNot(int disp)
	{
		return new GOpJumpIfNot(disp);
	}

	internal GOp ToGOpJumpUncond(int disp)
	{
		return new GOpJumpUncond(disp);
	}

	internal GOp ToGOpPutLocal(int off, XType ltype)
	{
		/*
		 * We normalize the local type:
		 *  - if writing a restricted type, we use that type;
		 *  - otherwise, we use the specified type, or
		 *    std::object is not specified.
		 */
		XType xt = Stack.Peek(0).GetRestricted();
		if (xt == null) {
			xt = ltype;
			if (xt == null) {
				xt = XType.OBJECT;
			}
		}
		return new GOpPutLocal(off, xt);
	}

	internal GOp ToGOpPutLocalIndexed(int off, int len, XType ltype)
	{
		XType xt = Stack.Peek(1).GetRestricted();
		if (xt == null) {
			xt = ltype;
			if (xt == null) {
				xt = XType.OBJECT;
			}
		}
		return new GOpPutLocalIndexed(off, len, xt);
	}

	internal GOp ToGOpRefLocal(int off)
	{
		XType xt = cfi.fi.localEmbedTypes[off];
		return new GOpRefLocal(off, xt);
	}

	internal GOp ToGOpRefLocalIndexed(int off, int len)
	{
		XType xt = cfi.fi.localEmbedTypes[off];
		return new GOpRefLocalIndexed(off, len, xt);
	}

	internal GOp ToGOpRet()
	{
		return new GOpRet();
	}

	public override string ToString()
	{
		return string.Format("{0} ({1} {2}) {3}", base.ToString(),
			cfi.fi.DebugName, addr, cfi.fi.OpcodeToString(addr));
	}

	internal void PrintSubTrees(TextWriter tw, int indent)
	{
		if (branch != null) {
			Compiler.Indent(tw, indent + 1);
			Console.WriteLine("-> {0}", branch.Serial);
		}
		if (dispatch != null) {
			foreach (CCNode node in dispatch.Values) {
				node.Print(tw, indent + 1);
			}
		}
	}
}

/*
 * A CCNodeEntry is a special kind of node that denotes the first opcode
 * of an interpreted function (i.e. its entry point). The parent node
 * of an entry node is the entry node of the function that called it;
 * this incarnates the call tree at function granularity (on which stack
 * allocation depends).
 */

class CCNodeEntry : CCNodeOpcode {

	/*
	 * Entry node of the parent function; null for the program
	 * entry point.
	 */
	internal CCNodeEntry parent;

	internal CCNodeEntry(CCNodeEntry parent, CCFunctionInterpreted cfi)
		: base(cfi, 0)
	{
		this.parent = parent;
	}

	internal bool IsDescendentOf(CCNodeEntry other)
	{
		if (other == null) {
			return true;
		}
		for (CCNodeEntry n = this; n != null; n = n.parent) {
			if (object.ReferenceEquals(n, other)) {
				return true;
			}
		}
		return false;
	}

	internal static CCNodeEntry EnterFunction(FunctionInterpreted fi,
		CCStack stack, CCNodeEntry parent, CCNode next)
	{
		if (Compiler.PrintFunctions != null) {
			fi.Print(Compiler.PrintFunctions, 1);
		}

		CCLocals locals = new CCLocals(fi.numLocalFieldsCoalesced);
		CCFunctionInterpreted cfi = new CCFunctionInterpreted(fi, next);
		CCNodeEntry node = new CCNodeEntry(parent, cfi);
		cfi.SetNode(0, node);
		node.MergeStack(stack);
		node.MergeLocals(locals);

		/*
		 * All instances allocated locally by this function must
		 * have instantiable types.
		 */
		foreach (XType xt in fi.localEmbedTypes) {
			xt.Close();
		}

		/*
		 * Infinite recursion detection: if the same function is
		 * already present several times in the ancestry,
		 * complains. (TODO: make the threshold configurable)
		 */
		int rc = 0;
		for (CCNodeEntry e = parent; e != null; e = e.parent) {
			if (e.cfi.fi == fi) {
				if (++ rc >= 5) {
					throw new Exception(string.Format("possible infinite recursion on function {0}", fi.DebugName));
				}
			}
		}

		return node;
	}

	internal override void Print(TextWriter tw, int indent)
	{
		foreach (CCNodeOpcode node in cfi.nodes) {
			if (node.Stack == null) {
				/*
				 * Unreachable node.
				 */
				continue;
			}
			Compiler.Indent(tw, indent);
			Console.WriteLine(node.ToString());
			node.PrintSubTrees(tw, indent);
		}
	}

	internal override GFunction ToGFunction()
	{
		return GFunctionInterpreted.Add(cfi);
	}
}

/*
 * Nodes for accessors.
 */

abstract class CCNodeNativeAccessor : CCNode {

	internal Function accessor;
	internal XType owner;
	internal CCNode next;

	internal CCNodeNativeAccessor(
		Function accessor, XType owner, CCNode next)
	{
		this.accessor = accessor;
		this.owner = owner;
		this.next = next;
	}

	public override string ToString()
	{
		return string.Format("{0} ({1} [accessor])",
			base.ToString(), accessor.DebugName);
	}
}

class CCNodeNativeFieldGet : CCNodeNativeAccessor {

	int off;

	internal CCNodeNativeFieldGet(Function accessor,
		XType owner, int off, CCNode next)
		: base(accessor, owner, next)
	{
		this.off = off;
	}

	internal override void Walk()
	{
		/*
		 * For all possible types of the TOS:
		 *
		 *  1. Verify that the accessor is usable on that type.
		 *
		 *  2. Obtain the right CCStruct, and get the types
		 *     that may be thus obtained. The "right CCStruct"
		 *     is the one for the CCType that matches the
		 *     element type, and the allocation node and variant
		 *     of the type found on the stack.
		 *
		 *  3. Merge all resulting types.
		 */
		CCTypeSet cts = Stack.Peek(0);
		CCTypeSet mv = null;
		foreach (CCType tos in cts) {
			tos.xType.CheckExtends(owner);
			CCType tos2 = tos.GetEmbedded(owner);
			CCStruct cs = CCStruct.Lookup(tos2);
			CCTypeSet vt = cs.Get(owner.fieldInverseMapping[off]);
			if (vt != null) {
				if (mv == null) {
					mv = vt;
				} else {
					mv = CCTypeSet.Merge(mv, vt);
				}
			}
		}
		if (mv != null) {
			next.MergeStack(Stack.Pop().Push(mv));
		}
	}
}

class CCNodeNativeFieldPut : CCNodeNativeAccessor {

	int off;

	internal CCNodeNativeFieldPut(Function accessor,
		XType owner, int off, CCNode next)
		: base(accessor, owner, next)
	{
		this.off = off;
	}

	internal override void Walk()
	{
		CCTypeSet cts = Stack.Peek(0);
		CCTypeSet sv = Stack.Peek(1);
		foreach (CCType tos in cts) {
			tos.xType.CheckExtends(owner);
			CCType tos2 = tos.GetEmbedded(owner);
			CCStruct cs = CCStruct.Lookup(tos2);
			cs.Merge(owner.fieldInverseMapping[off], sv);
		}
		next.MergeStack(Stack.Pop(2));
	}
}

class CCNodeNativeFieldClear : CCNodeNativeAccessor {

	internal CCNodeNativeFieldClear(Function accessor,
		XType owner, CCNode next)
		: base(accessor, owner, next)
	{
	}

	internal override void Walk()
	{
		CCTypeSet cts = Stack.Peek(0);
		foreach (CCType tos in cts) {
			tos.xType.CheckExtends(owner);
		}
		next.MergeStack(Stack.Pop());
	}
}

class CCNodeNativeFieldTest : CCNodeNativeAccessor {

	internal CCNodeNativeFieldTest(Function accessor,
		XType owner, CCNode next)
		: base(accessor, owner, next)
	{
	}

	internal override void Walk()
	{
		CCTypeSet cts = Stack.Peek(0);
		foreach (CCType tos in cts) {
			tos.xType.CheckExtends(owner);
		}
		next.MergeStack(Stack.Pop().Push(CCTypeSet.BOOL));
	}
}

class CCNodeNativeEmbedRef : CCNodeNativeAccessor {

	XType eltType;

	internal CCNodeNativeEmbedRef(Function accessor,
		XType owner, XType eltType, CCNode next)
		: base(accessor, owner, next)
	{
		this.eltType = eltType;
	}

	internal override void Walk()
	{
		CCTypeSet cts = Stack.Peek(0);
		CCTypeSet mv = null;
		foreach (CCType tos in cts) {
			tos.xType.CheckExtends(owner);
			CCTypeSet sv = new CCTypeSet(tos.GetEmbedded(eltType));
			if (mv == null) {
				mv = sv;
			} else {
				mv = CCTypeSet.Merge(mv, sv);
			}
		}
		if (mv != null) {
			next.MergeStack(Stack.Pop().Push(mv));
		}
	}
}

class CCNodeNativeFieldArrayGet : CCNodeNativeAccessor {

	int off;

	internal CCNodeNativeFieldArrayGet(Function accessor,
		XType owner, int off, CCNode next)
		: base(accessor, owner, next)
	{
		this.off = off;
	}

	internal override void Walk()
	{
		CCTypeSet cts = Stack.Peek(0);
		Stack.Peek(1).CheckInt();
		CCTypeSet mv = null;
		foreach (CCType tos in cts) {
			tos.xType.CheckExtends(owner);
			CCType tos2 = tos.GetEmbedded(owner);
			CCStruct cs = CCStruct.Lookup(tos2);
			CCTypeSet vt = cs.Get(owner.fieldInverseMapping[off]);
			if (vt != null) {
				if (mv == null) {
					mv = vt;
				} else {
					mv = CCTypeSet.Merge(mv, vt);
				}
			}
		}
		if (mv != null) {
			next.MergeStack(Stack.Pop(2).Push(mv));
		}
	}
}

class CCNodeNativeFieldArrayPut : CCNodeNativeAccessor {

	int off;

	internal CCNodeNativeFieldArrayPut(Function accessor,
		XType owner, int off, CCNode next)
		: base(accessor, owner, next)
	{
		this.off = off;
	}

	internal override void Walk()
	{
		CCTypeSet cts = Stack.Peek(0);
		Stack.Peek(1).CheckInt();
		CCTypeSet sv = Stack.Peek(2);
		foreach (CCType tos in cts) {
			tos.xType.CheckExtends(owner);
			CCType tos2 = tos.GetEmbedded(owner);
			CCStruct cs = CCStruct.Lookup(tos2);
			cs.Merge(owner.fieldInverseMapping[off], sv);
		}
		next.MergeStack(Stack.Pop(3));
	}
}

class CCNodeNativeFieldArrayClear : CCNodeNativeAccessor {

	internal CCNodeNativeFieldArrayClear(Function accessor,
		XType owner, CCNode next)
		: base(accessor, owner, next)
	{
	}

	internal override void Walk()
	{
		CCTypeSet cts = Stack.Peek(0);
		Stack.Peek(1).CheckInt();
		foreach (CCType tos in cts) {
			tos.xType.CheckExtends(owner);
		}
		next.MergeStack(Stack.Pop(2));
	}
}

class CCNodeNativeFieldArrayTest : CCNodeNativeAccessor {

	internal CCNodeNativeFieldArrayTest(Function accessor,
		XType owner, CCNode next)
		: base(accessor, owner, next)
	{
	}

	internal override void Walk()
	{
		CCTypeSet cts = Stack.Peek(0);
		Stack.Peek(1).CheckInt();
		foreach (CCType tos in cts) {
			tos.xType.CheckExtends(owner);
		}
		next.MergeStack(Stack.Pop(2).Push(CCTypeSet.BOOL));
	}
}

class CCNodeNativeEmbedArrayRef : CCNodeNativeAccessor {

	XType eltType;

	internal CCNodeNativeEmbedArrayRef(Function accessor,
		XType owner, XType eltType, CCNode next)
		: base(accessor, owner, next)
	{
		this.eltType = eltType;
	}

	internal override void Walk()
	{
		CCTypeSet cts = Stack.Peek(0);
		Stack.Peek(1).CheckInt();
		CCTypeSet mv = null;
		foreach (CCType tos in cts) {
			tos.xType.CheckExtends(owner);
			CCTypeSet sv = new CCTypeSet(tos.GetEmbedded(eltType));
			if (mv == null) {
				mv = sv;
			} else {
				mv = CCTypeSet.Merge(mv, sv);
			}
		}
		if (mv != null) {
			next.MergeStack(Stack.Pop(2).Push(mv));
		}
	}
}

/*
 * Nodes for array accessors.
 */

abstract class CCNodeNativeArrayAccessor : CCNode {

	internal Function accessor;
	internal XType owner;
	internal CCNode next;

	internal CCNodeNativeArrayAccessor(
		Function accessor, XType owner, CCNode next)
	{
		this.accessor = accessor;
		this.owner = owner;
		this.next = next;
	}

	public override string ToString()
	{
		return string.Format("{0} ({1} [accessor])",
			base.ToString(), accessor.DebugName);
	}
}

class CCNodeNativeArrayMakeRef : CCNodeNativeArrayAccessor {

	internal CCNodeNativeArrayMakeRef(Function accessor,
		XType owner, CCNode next)
		: base(accessor, owner, next)
	{
	}

	internal override void Walk()
	{
		CCTypeSet cts = Stack.Peek(0);
		Stack.Peek(1).CheckInt();
		foreach (CCType tos in cts) {
			tos.xType.CheckExtends(owner);
		}
		next.MergeStack(Stack.Pop(2));
	}
}

class CCNodeNativeArrayMakeEmbed : CCNodeNativeArrayAccessor {

	internal CCNodeNativeArrayMakeEmbed(Function accessor,
		XType owner, CCNode next)
		: base(accessor, owner, next)
	{
	}

	internal override void Walk()
	{
		CCTypeSet cts = Stack.Peek(0);
		Stack.Peek(1).CheckInt();
		foreach (CCType tos in cts) {
			tos.xType.CheckExtends(owner);
		}
		next.MergeStack(Stack.Pop(2));
	}
}

class CCNodeNativeArraySub : CCNodeNativeArrayAccessor {

	internal CCNodeNativeArraySub(Function accessor,
		XType owner, CCNode next)
		: base(accessor, owner, next)
	{
	}

	internal override void Walk()
	{
		CCTypeSet cts2 = Stack.Peek(0);
		CCTypeSet cts1 = Stack.Peek(1);
		Stack.Peek(2).CheckInt();
		Stack.Peek(3).CheckInt();
		foreach (CCType tos in cts1) {
			tos.xType.CheckExtends(owner);
		}
		foreach (CCType tos in cts2) {
			tos.xType.CheckExtends(owner);
		}
		next.MergeStack(Stack.Pop(4));
	}
}

class CCNodeNativeArraySubSelf : CCNodeNativeArrayAccessor {

	internal CCNodeNativeArraySubSelf(Function accessor,
		XType owner, CCNode next)
		: base(accessor, owner, next)
	{
	}

	internal override void Walk()
	{
		CCTypeSet cts = Stack.Peek(0);
		Stack.Peek(1).CheckInt();
		Stack.Peek(2).CheckInt();
		foreach (CCType tos in cts) {
			tos.xType.CheckExtends(owner);
		}
		next.MergeStack(Stack.Pop(3));
	}
}

class CCNodeNativeArrayIsInit : CCNodeNativeArrayAccessor {

	internal CCNodeNativeArrayIsInit(Function accessor,
		XType owner, CCNode next)
		: base(accessor, owner, next)
	{
	}

	internal override void Walk()
	{
		CCTypeSet cts = Stack.Peek(0);
		foreach (CCType tos in cts) {
			tos.xType.CheckExtends(owner);
		}
		next.MergeStack(Stack.Pop().Push(CCTypeSet.BOOL));
	}
}

class CCNodeNativeArrayLength : CCNodeNativeArrayAccessor {

	internal CCNodeNativeArrayLength(Function accessor,
		XType owner, CCNode next)
		: base(accessor, owner, next)
	{
	}

	internal override void Walk()
	{
		CCTypeSet cts = Stack.Peek(0);
		foreach (CCType tos in cts) {
			tos.xType.CheckExtends(owner);
		}
		next.MergeStack(Stack.Pop().Push(CCTypeSet.INT));
	}
}

class CCNodeNativeArrayGet : CCNodeNativeArrayAccessor {

	internal CCNodeNativeArrayGet(Function accessor,
		XType owner, CCNode next)
		: base(accessor, owner, next)
	{
	}

	internal override void Walk()
	{
		CCTypeSet cts = Stack.Peek(0);
		Stack.Peek(1).CheckInt();
		CCTypeSet mv = null;
		foreach (CCType tos in cts) {
			tos.xType.CheckExtends(owner);
			CCType tos2 = tos.GetEmbedded(owner);
			CCStruct cs = CCStruct.Lookup(tos2);
			CCTypeSet vt = cs.Get(0);
			if (vt != null) {
				if (mv == null) {
					mv = vt;
				} else {
					mv = CCTypeSet.Merge(mv, vt);
				}
			}
		}
		if (mv != null) {
			next.MergeStack(Stack.Pop(2).Push(mv));
		}
	}
}

class CCNodeNativeArrayPut : CCNodeNativeArrayAccessor {

	internal CCNodeNativeArrayPut(Function accessor,
		XType owner, CCNode next)
		: base(accessor, owner, next)
	{
	}

	internal override void Walk()
	{
		CCTypeSet cts = Stack.Peek(0);
		Stack.Peek(1).CheckInt();
		CCTypeSet sv = Stack.Peek(2);
		foreach (CCType tos in cts) {
			tos.xType.CheckExtends(owner);
			CCType tos2 = tos.GetEmbedded(owner);
			CCStruct cs = CCStruct.Lookup(tos2);
			cs.Merge(0, sv);
		}
		next.MergeStack(Stack.Pop(3));
	}
}

class CCNodeNativeArrayClear : CCNodeNativeArrayAccessor {

	internal CCNodeNativeArrayClear(Function accessor,
		XType owner, CCNode next)
		: base(accessor, owner, next)
	{
	}

	internal override void Walk()
	{
		CCTypeSet cts = Stack.Peek(0);
		Stack.Peek(1).CheckInt();
		foreach (CCType tos in cts) {
			tos.xType.CheckExtends(owner);
		}
		next.MergeStack(Stack.Pop(2));
	}
}

class CCNodeNativeArrayIsEltInit : CCNodeNativeArrayAccessor {

	internal CCNodeNativeArrayIsEltInit(Function accessor,
		XType owner, CCNode next)
		: base(accessor, owner, next)
	{
	}

	internal override void Walk()
	{
		CCTypeSet cts = Stack.Peek(0);
		Stack.Peek(1).CheckInt();
		foreach (CCType tos in cts) {
			tos.xType.CheckExtends(owner);
		}
		next.MergeStack(Stack.Pop(2).Push(CCTypeSet.BOOL));
	}
}

class CCNodeNativeArrayRef : CCNodeNativeArrayAccessor {

	internal CCNodeNativeArrayRef(Function accessor,
		XType owner, CCNode next)
		: base(accessor, owner, next)
	{
	}

	internal override void Walk()
	{
		CCTypeSet cts = Stack.Peek(0);
		Stack.Peek(1).CheckInt();
		CCTypeSet mv = null;
		foreach (CCType tos in cts) {
			tos.xType.CheckExtends(owner);
			CCType tos2 = tos.GetEmbedded(
				owner.GetArrayElementType());
			CCTypeSet vt = new CCTypeSet(tos2);
			if (vt != null) {
				if (mv == null) {
					mv = vt;
				} else {
					mv = CCTypeSet.Merge(mv, vt);
				}
			}
		}
		if (mv != null) {
			next.MergeStack(Stack.Pop(2).Push(mv));
		}
	}
}

/*
 * Node for native functions.
 */

class CCNodeNative : CCNode {

	Function fn;
	XType[] tParams;
	CCTypeSet[] ctsRets;
	CCNode next;

	internal CCNodeNative(Function fn,
		XType[] tParams, CCTypeSet[] ctsRets, CCNode next)
	{
		this.fn = fn;
		this.tParams = tParams;
		this.ctsRets = ctsRets;
		this.next = next;
	}

	internal override void Walk()
	{
		Stack.CheckMultipleSubTypeOf(tParams);
		CCStack nstack = Stack.Pop(tParams.Length).Push(ctsRets);
		next.MergeStack(nstack);
	}

	internal override GFunction ToGFunction()
	{
		// FIXME
		return GFunctionNative.Add(fn, "NYI");
	}

	public override string ToString()
	{
		return string.Format("{0} ({1} [native])",
			base.ToString(), fn.DebugName);
	}
}

/*
 * Node for std::dup.
 */

class CCNodeNativeDup : CCNode {

	Function f;
	CCNode next;

	internal CCNodeNativeDup(Function f, CCNode next)
	{
		this.f = f;
		this.next = next;
	}

	internal override void Walk()
	{
		CCTypeSet cts = Stack.Peek(0);
		next.MergeStack(Stack.Push(cts));
	}

	public override string ToString()
	{
		return string.Format("{0} (std::dup [native])",
			base.ToString());
	}

	internal override GFunction ToGFunction()
	{
		string code;
		XType xt = Stack.Peek(0).GetRestricted();
		if (xt == XType.BOOL || xt == XType.U8 || xt == XType.I8) {
			code = "T1_PUSH_U8(T1_TOS_U8());";
		} else if (xt == XType.U16 || xt == XType.I16) {
			code = "T1_PUSH_U16(T1_TOS_U16());";
		} else if (xt == XType.U32 || xt == XType.I32) {
			code = "T1_PUSH_U32(T1_TOS_U32());";
		} else if (xt == XType.U64 || xt == XType.I64) {
			code = "T1_PUSH_U64(T1_TOS_U64());";
		} else if (xt == null) {
			code = "T1_PUSH_REF(T1_TOS_REF());";
		} else {
			throw new Exception(string.Format("unknown restricted type: {0}", xt.Name));
		}
		return GFunctionNative.Add(f, code);
	}
}

/*
 * Node for std::swap.
 */

class CCNodeNativeSwap : CCNode {

	CCNode next;

	internal CCNodeNativeSwap(CCNode next)
	{
		this.next = next;
	}

	internal override void Walk()
	{
		CCTypeSet cts1 = Stack.Peek(1);
		CCTypeSet cts2 = Stack.Peek(0);
		next.MergeStack(Stack.Pop(2).Push(cts2).Push(cts1));
	}

	public override string ToString()
	{
		return string.Format("{0} (std::swap [native])",
			base.ToString());
	}
}

/*
 * Node for std::over.
 */

class CCNodeNativeOver : CCNode {

	CCNode next;

	internal CCNodeNativeOver(CCNode next)
	{
		this.next = next;
	}

	internal override void Walk()
	{
		CCTypeSet cts = Stack.Peek(1);
		next.MergeStack(Stack.Push(cts));
	}

	public override string ToString()
	{
		return string.Format("{0} (std::over [native])",
			base.ToString());
	}
}

/*
 * Node for std::rot.
 */

class CCNodeNativeRot : CCNode {

	CCNode next;

	internal CCNodeNativeRot(CCNode next)
	{
		this.next = next;
	}

	internal override void Walk()
	{
		CCTypeSet cts1 = Stack.Peek(2);
		CCTypeSet cts2 = Stack.Peek(1);
		CCTypeSet cts3 = Stack.Peek(0);
		next.MergeStack(Stack.Pop(3).Push(cts2).Push(cts3).Push(cts1));
	}

	public override string ToString()
	{
		return string.Format("{0} (std::rot [native])",
			base.ToString());
	}
}

/*
 * Node for std::-rot.
 */

class CCNodeNativeNRot : CCNode {

	CCNode next;

	internal CCNodeNativeNRot(CCNode next)
	{
		this.next = next;
	}

	internal override void Walk()
	{
		CCTypeSet cts1 = Stack.Peek(2);
		CCTypeSet cts2 = Stack.Peek(1);
		CCTypeSet cts3 = Stack.Peek(0);
		next.MergeStack(Stack.Pop(3).Push(cts3).Push(cts1).Push(cts2));
	}

	public override string ToString()
	{
		return string.Format("{0} (std::-rot [native])",
			base.ToString());
	}
}

/*
 * Node for the function that returns a given std::type instance.
 */

class CCNodeNativeXType : CCNode {

	CCType typeOfType;
	CCNode next;

	internal CCNodeNativeXType(XType xt, CCNode next)
	{
		this.next = next;
		typeOfType = CCType.TypeOfType(xt);
	}

	internal override void Walk()
	{
		next.MergeStack(Stack.Push(typeOfType));
	}
}

/*
 * Node for std::new.
 */

class CCNodeNativeNew : CCNode {

	CCNode next;

	internal CCNodeNativeNew(CCNode next)
	{
		this.next = next;
	}

	internal override void Walk()
	{
		CCTypeSet cts = Stack.Peek(0);
		next.MergeStack(Stack.Pop().Push(cts.ToNewInstance()));
	}
}
