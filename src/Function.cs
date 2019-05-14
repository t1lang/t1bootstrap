using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

abstract class Function : IComparable<Function> {

	static ulong currentSerial = 0;

	ulong serial;

	/*
	 * Each function has a "debug name" which is normally the name
	 * under which it was first registered.
	 */
	internal string DebugName {
		get;
		private set;
	}

	/*
	 * Create the instance with the provided "debug name".
	 */
	internal Function(string debugName)
	{
		this.DebugName = debugName;
		serial = currentSerial ++;
	}

	internal abstract void Run(CPU cpu);

	public int CompareTo(Function other)
	{
		return serial.CompareTo(other.serial);
	}

	internal virtual CCNode Enter(CCStack stack,
		CCNodeEntry parent, CCNode next)
	{
		throw new Exception(string.Format("native function {0} cannot be compiled", DebugName));
	}

	internal virtual void Print(TextWriter tw, int indent)
	{
		Compiler.Indent(tw, indent);
		tw.WriteLine("function: {0}", DebugName);
	}

	public override string ToString()
	{
		return string.Format("{0} ({1})", DebugName, serial);
	}

	/*
	 * Each registration consists in a name and a list of types.
	 * Type lists are partially ordered. When a function is called:
	 *
	 *  - The call is by name. Only functions registered with that
	 *    name may be considered.
	 *
	 *  - Matching functions are those such that the current stack
	 *    contents are sub-types of the registration types.
	 *
	 *  - The called function is the most precise of the matching
	 *    functions. If there is no matching function that is more
	 *    precise than all other matching functions, then the call
	 *    is ambiguous and fails.
	 *
	 * The partial order for function registrations depends on
	 * sub-typing, which may change over time; thus, we do not
	 * precompute such orders.
	 */
	static IDictionary<string, List<FunctionRegistration>> ALL;

	static Function()
	{
		ALL = new SortedDictionary<string, List<FunctionRegistration>>(
			StringComparer.Ordinal);
	}

	internal static void Register(string name, XType[] xts, Function f)
	{
		List<FunctionRegistration> r;
		if (!ALL.TryGetValue(name, out r)) {
			r = new List<FunctionRegistration>();
			r.Add(new FunctionRegistration(xts, f));
			ALL[name] = r;
			return;
		}

		foreach (FunctionRegistration fr in r) {
			if (Compare(xts, fr.types) == 0) {
				throw new Exception(string.Format("duplicate registration for function {0} with types: {1}", name, ToString(xts)));
			}
		}
		r.Add(new FunctionRegistration(xts, f));
	}

	internal static void RegisterImmediate(string name, Function f)
	{
		List<FunctionRegistration> r = new List<FunctionRegistration>();
		if (!ALL.TryGetValue(name, out r)) {
			r = new List<FunctionRegistration>();
			ALL[name] = r;
		}
		foreach (FunctionRegistration fr in r) {
			if (fr.types.Length == 0) {
				throw new Exception(string.Format("duplicate registration for function {0} with no parameters", name));
			}
		}
		r.Add(new FunctionRegistration(f));
	}

	internal static Function Lookup(string name, CPU cpu)
	{
		/*
		 * Find the function registrations for the specified name.
		 */
		List<FunctionRegistration> r1;
		if (!ALL.TryGetValue(name, out r1)) {
			throw new Exception(string.Format("call fails: no such function: {0}", name));
		}

		/*
		 * Get all matching functions. We keep a pruned list in r2:
		 * when a function is added to the list, we first remove all
		 * functions that are less precise than the new function,
		 * and we don't add a function to r2 if a more precise
		 * function is already there.
		 */
		int maxNum = 0;
		int d = cpu.Depth;
		List<FunctionRegistration> r2 =
			new List<FunctionRegistration>();
		foreach (FunctionRegistration fr in r1) {
			maxNum = Math.Max(fr.types.Length, maxNum);
			if (!fr.Match(cpu, d)) {
				continue;
			}
			bool inc = true;
			for (int i = r2.Count - 1; i >= 0; i --) {
				int cc = Compare(r2[i].types, fr.types);
				if (cc == 1) {
					r2.RemoveAt(i);
				} else if (cc == -1) {
					inc = false;
					break;
				}
			}
			if (inc) {
				r2.Add(fr);
			}
		}
		maxNum = Math.Min(maxNum, d);

		/*
		 * We remove duplicates, in case several matches are
		 * different registrations for the same function.
		 */
		RemoveDuplicateFunctions(r2);

		/*
		 * If r2 is empty, then there is no matching call. If r2
		 * contains more than one function, then it is ambiguous.
		 */
		if (r2.Count == 0) {
			throw new Exception(string.Format("no matching function for call {0} on: {1}", name, ToString(cpu, maxNum)));
		} else if (r2.Count > 1) {
			StringBuilder sb = new StringBuilder();
			sb.AppendFormat("multiple matching function for call {0} on: {1}", name, ToString(cpu, maxNum));
			foreach (FunctionRegistration fr in r2) {
				sb.Append(Environment.NewLine);
				sb.Append("   ");
				sb.Append(ToString(fr.types));
			}
			throw new Exception(sb.ToString());
		}
		return r2[0].fun;
	}

	static void RemoveDuplicateFunctions(List<FunctionRegistration> r)
	{
		for (int i = 0; i < r.Count; i ++) {
			if (i >= r.Count) {
				break;
			}
			Function f = r[i].fun;
			for (int j = r.Count - 1; j > i; j --) {
				if (r[j].fun == f) {
					r.RemoveAt(j);
				}
			}
		}
	}

	/*
	 * Find an immediate function by name. If there is no such
	 * function, null is returned.
	 */
	internal static Function LookupImmediate(string name)
	{
		List<FunctionRegistration> r;
		if (!ALL.TryGetValue(name, out r)) {
			return null;
		}
		foreach (FunctionRegistration fr in r) {
			if (fr.immediate) {
				return fr.fun;
			}
		}
		return null;
	}

	/*
	 * Function a function with a given name and no parameter. If
	 * there is no such function, null is returned.
	 */
	internal static Function LookupNoArgs(string name)
	{
		List<FunctionRegistration> r;
		if (!ALL.TryGetValue(name, out r)) {
			return null;
		}
		foreach (FunctionRegistration fr in r) {
			if (fr.types.Length == 0) {
				return fr.fun;
			}
		}
		return null;
	}

	/*
	 * Get all function registrations for a given name. The returned
	 * list MUST NOT be modified. If there is none, an empty list
	 * is returned.
	 */
	internal static List<FunctionRegistration> LookupAll(string name)
	{
		List<FunctionRegistration> r;
		if (!ALL.TryGetValue(name, out r)) {
			r = new List<FunctionRegistration>();
		}
		return r;
	}

	/*
	 * Perform a call resolution over the specified argument types,
	 * and the list of functions to test for.
	 */
	internal static Function Resolve(string name,
		List<FunctionRegistration> r, XType[] xts)
	{
		/*
		 * Keep only matching functions, removing functions when
		 * allowed by the precision partial order.
		 */
		List<FunctionRegistration> r2 =
			new List<FunctionRegistration>();
		foreach (FunctionRegistration fr in r) {
			if (!fr.Match(xts)) {
				continue;
			}
			bool inc = true;
			for (int i = r2.Count - 1; i >= 0; i --) {
				int cc = Compare(r2[i].types, fr.types);
				if (cc == 1) {
					r2.RemoveAt(i);
				} else if (cc == -1) {
					inc = false;
					break;
				}
			}
			if (inc) {
				r2.Add(fr);
			}
		}
		RemoveDuplicateFunctions(r2);

		/*
		 * The call fails to resolve only if we get a single
		 * function in r2 at this point.
		 */
		if (r2.Count == 0) {
			throw new Exception(string.Format("no matching function for call {0} on: {1}", name, ToString(xts)));
		} else if (r2.Count > 1) {
			StringBuilder sb = new StringBuilder();
			sb.AppendFormat("multiple matching function for call {0} on: {1}", name, ToString(xts));
			foreach (FunctionRegistration fr in r2) {
				sb.Append(Environment.NewLine);
				sb.Append("   ");
				sb.Append(ToString(fr.types));
			}
			throw new Exception(sb.ToString());
		}

		return r2[0].fun;
	}

	/*
	 * Returned value:
	 *   0                if xt1 is the same list of types as xt2
	 *  -1                if xt1 is more precise than xt2
	 *  +1                if xt1 is less precise than xt2
	 *  Int32.MinValue    if xt1 and xt2 are not comparable
	 */
	internal static int Compare(XType[] xt1, XType[] xt2)
	{
		/*
		 * First compare types pairwise, for the common part of
		 * the arrays.
		 */
		int r = 0;
		int n1 = xt1.Length;
		int n2 = xt2.Length;
		int n = Math.Min(n1, n2);
		for (int i = 0; i < n; i ++) {
			bool b1 = xt1[n1 - 1 - i].IsSubTypeOf(xt2[n2 - 1 - i]);
			bool b2 = xt2[n2 - 1 - i].IsSubTypeOf(xt1[n1 - 1 - i]);
			if (b1 && b2) {
				continue;
			} else if (b1) {
				if (r > 0) {
					return Int32.MinValue;
				}
				r = -1;
			} else if (b2) {
				if (r < 0) {
					return Int32.MinValue;
				}
				r = +1;
			} else {
				return Int32.MinValue;
			}
		}

		/*
		 * If one array is longer, then it is "more precise",
		 * assuming that no contrary decision was reached before.
		 */
		if (n1 < n2) {
			if (r == 0) {
				r = +1;
			} else if (r < 0) {
				return Int32.MinValue;
			}
		} else if (n1 > n2) {
			if (r == 0) {
				r = -1;
			} else if (r > 0) {
				return Int32.MinValue;
			}
		}
		return r;
	}

	static string ToString(CPU cpu, int depth)
	{
		StringBuilder sb = new StringBuilder();
		for (int i = depth - 1; i >= 0; i --) {
			if (sb.Length > 0) {
				sb.Append(" ");
			}
			sb.Append(cpu.Peek(i).VType.Name);
		}
		return sb.ToString();
	}

	static string ToString(XType[] xts)
	{
		if (xts.Length == 0) {
			return "<>";
		}
		StringBuilder sb = new StringBuilder();
		foreach (XType xt in xts) {
			if (sb.Length > 0) {
				sb.Append(" ");
			}
			sb.Append(xt.Name);
		}
		return sb.ToString();
	}
}

/*
 * A FunctionRegistration records how a function is registered for purposes
 * of function lookup.
 */

class FunctionRegistration {

	internal XType[] types;
	internal Function fun;
	internal bool immediate;

	internal FunctionRegistration(Function fun)
	{
		this.types = XType.ZERO;
		this.fun = fun;
		this.immediate = true;
	}

	internal FunctionRegistration(XType[] types, Function fun)
	{
		this.types = types;
		this.fun = fun;
		this.immediate = false;
	}

	internal bool Match(CPU cpu, int depth)
	{
		int n = types.Length;
		if (n > depth) {
			return false;
		}
		for (int i = 0; i < n; i ++) {
			XType rt = types[n - 1 - i];
			if (!cpu.Peek(i).VType.IsSubTypeOf(rt)) {
				return false;
			}
		}
		return true;
	}

	internal bool Match(XType[] xts)
	{
		int n = types.Length;
		int m = xts.Length;
		if (n > m) {
			return false;
		}
		for (int i = 0; i < n; i ++) {
			if (!xts[m - 1 - i].IsSubTypeOf(types[n - 1 - i])) {
				return false;
			}
		}
		return true;
	}
}

/*
 * An interpreted function is built out of opcodes; when executed, it
 * creates a new frame and enters the function.
 */

class FunctionInterpreted : Function {

	int numLocalFields;
	Opcode[] code;

	internal int CodeLength {
		get {
			return code.Length;
		}
	}

	internal int numLocalFieldsCoalesced;

	/*
	 * Local field mapping, with coalescing of arrays.
	 */
	internal int[] fieldInverseMapping;

	/*
	 * Types of local instances.
	 */
	internal XType[] localEmbedTypes;

	internal FunctionInterpreted(string debugName,
		int numLocalFields, XType[] localEmbedTypes,
		int numLocalFieldsCoalesced, int[] fieldInverseMapping,
		Opcode[] code)
		: base(debugName)
	{
		this.numLocalFields = numLocalFields;
		this.localEmbedTypes = localEmbedTypes;
		this.numLocalFieldsCoalesced = numLocalFieldsCoalesced;
		this.fieldInverseMapping = fieldInverseMapping;
		this.code = code;
	}

	internal override void Run(CPU cpu)
	{
		cpu.Enter(code, numLocalFields, localEmbedTypes);
	}

	internal void BuildTree(int off, CCNodeOpcode node)
	{
		code[off].BuildTree(node);
	}

	internal GOp ToGOp(int off, CCNodeOpcode node)
	{
		GOp g = code[off].ToGOp(node);
		g.addr = off;
		return g;
	}

	internal override CCNode Enter(CCStack stack,
		CCNodeEntry parent, CCNode next)
	{
		return CCNodeEntry.EnterFunction(this, stack, parent, next);
	}

	internal string OpcodeToString(int addr)
	{
		return code[addr].ToString(addr);
	}

	internal override void Print(TextWriter tw, int indent)
	{
		base.Print(tw, indent);
		for (int i = 0; i < code.Length; i ++) {
			Compiler.Indent(tw, indent);
			Console.WriteLine("{0,6}  {1}", i, code[i].ToString(i));
		}
	}
}

/*
 * A special function backed by native code, and that cannot be compiled.
 */

class FunctionSpec : Function {

	internal delegate void NativeRun(CPU cpu);

	NativeRun code;

	internal FunctionSpec(string debugName, NativeRun code)
		: base(debugName)
	{
		this.code = code;
	}

	internal override void Run(CPU cpu)
	{
		code(cpu);
	}
}

/*
 * Base class for native functions that can be compiled. This creates a
 * node that expects some parameter types (or sub-types thereof), and
 * returns values with specific types (and not bound to any allocation
 * node).
 */

abstract class FunctionNative : Function {

	XType[] tParams;
	CCTypeSet[] ctsRets;

	internal FunctionNative(string debugName,
		XType[] tParams, XType[] tRets)
		: base(debugName)
	{
		this.tParams = tParams;
		int n = tRets.Length;
		ctsRets = new CCTypeSet[n];
		for (int i = 0; i < n; i ++) {
			ctsRets[i] = new CCTypeSet(new CCType(tRets[i]));
		}
	}

	internal override CCNode Enter(CCStack stack,
		CCNodeEntry parent, CCNode next)
	{
		CCNode node = new CCNodeNative(this, tParams, ctsRets, next);
		node.MergeStack(stack);
		return node;
	}
}
