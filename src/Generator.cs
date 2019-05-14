using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/*
 * A GFunction instance represents a function code that is to be generated.
 * Several GFunction may correspond to a given Function if specialization
 * occurred based on used types.
 */

abstract class GFunction : IComparable<GFunction> {

	static ulong currentSerial = 0;

	internal ulong Serial {
		get; private set;
	}

	internal Function f;

	internal GFunction(Function f)
	{
		this.f = f;
		Serial = currentSerial ++;
	}

	internal virtual void Print(TextWriter tw, int indent)
	{
		Compiler.Indent(tw, indent);
		tw.WriteLine("{0} ({1}) [gen]", f.DebugName, Serial);
	}

	public int CompareTo(GFunction gf)
	{
		return Serial.CompareTo(gf.Serial);
	}

	public override bool Equals(object other)
	{
		GFunction gf = other as GFunction;
		if (gf != null) {
			return Serial == gf.Serial;
		} else {
			return false;
		}
	}

	public override int GetHashCode()
	{
		return (int)Serial;
	}
}

/*
 * A GFunctionNative is a GFunction which is implemented as a piece of C
 * code which will be integrated in the runtime loop. Identical codes
 * for the same function are merged (using exact textual equality).
 */

class GFunctionNative : GFunction {

	string code;

	GFunctionNative(Function f, string code)
		: base(f)
	{
		this.code = code;
	}

	static SortedDictionary<Function,
		SortedDictionary<string, GFunctionNative>> ALL =
		new SortedDictionary<Function,
			SortedDictionary<string, GFunctionNative>>();

	internal static GFunctionNative Add(Function f, string code)
	{
		GFunctionNative gf;
		SortedDictionary<string, GFunctionNative> d;
		if (!ALL.TryGetValue(f, out d)) {
			d = new SortedDictionary<string, GFunctionNative>(
				StringComparer.Ordinal);
			ALL[f] = d;
			gf = new GFunctionNative(f, code);
			d[code] = gf;
			return gf;
		}
		if (!d.TryGetValue(code, out gf)) {
			gf = new GFunctionNative(f, code);
			d[code] = gf;
		}
		return gf;
	}

	public override string ToString()
	{
		return string.Format("{0} ({1}) [native]", f, Serial);
	}
}

/*
 * A GFunctionInterpreted is a GFunction for a FunctionInterpreted
 * instance.
 */

class GFunctionInterpreted : GFunction {

	internal FunctionInterpreted fi;

	GOpFrame frame;
	GOp[] spec;

	GFunctionInterpreted(CCFunctionInterpreted cfi)
		: base(cfi.fi)
	{
		this.fi = cfi.fi;

		/*
		 * Specialize opcodes. In this step:
		 *
		 *  - For each source opcode, we create an appropriate GOp
		 *    instance. Type specialization for local fields
		 *    happens here.
		 *
		 *  - Unreachable opcodes (whose entry stack is null) yield
		 *    a GOpZero.
		 */
		List<GOp> specOps = new List<GOp>();
		int srcLen = cfi.nodes.Length;
		for (int i = 0; i < srcLen; i ++) {
			GOp g = cfi.ToGOp(i);
			if (g == null) {
				throw new Exception("NYI (GOp)");
			}
			specOps.Add(g);
		}
		spec = specOps.ToArray();

		AdjustJumps();

		/*
		 * Compute local frame elements:
		 *
		 *  - For local fields, only putlocal and putlocalindexed
		 *    matter; we check that each local has a single storage
		 *    type.
		 *
		 *  - For local instances, we use reflocal and
		 *    reflocalindexed.
		 */
		frame = new GOpFrame(this);
		for (int i = 0; i < spec.Length; i ++) {
			var pl = spec[i] as GOpPutLocal;
			if (pl != null) {
				frame.AddLocalVariable(pl.off, pl.ltype);
			}
			var pli = spec[i] as GOpPutLocalIndexed;
			if (pli != null) {
				frame.AddLocalVariableArray(
					pli.off, pli.len, pli.ltype);
			}
			var rl = spec[i] as GOpRefLocal;
			if (rl != null) {
				frame.AddLocalInstance(rl.off, rl.ltype);
			}
			var rli = spec[i] as GOpRefLocalIndexed;
			if (rli != null) {
				frame.AddLocalInstanceArray(
					rli.off, rli.len, rli.ltype);
			}
		}
	}

	/*
	 * Adjust jump opcodes to point to their target GOp.
	 */
	void AdjustJumps()
	{
		for (int i = 0; i < spec.Length; i ++) {
			GOpJump gj = spec[i] as GOpJump;
			if (gj == null) {
				continue;
			}
			gj.target = spec[i + 1 + gj.disp];
		}
	}

	static SortedDictionary<FunctionInterpreted,
		SortedSet<GFunctionInterpreted>> ALL =
			new SortedDictionary<FunctionInterpreted,
				SortedSet<GFunctionInterpreted>>();

	/*
	 * Turn a function call into the corresponding GFunctionInterpreted.
	 * This applies recursively on all the the call sub-tree.
	 */
	internal static GFunctionInterpreted Add(CCFunctionInterpreted cfi)
	{
		GFunctionInterpreted ngf = new GFunctionInterpreted(cfi);

		/*
		 * Find the GFunction already existing for this function.
		 */
		SortedSet<GFunctionInterpreted> ss;
		if (!ALL.TryGetValue(cfi.fi, out ss)) {
			ss = new SortedSet<GFunctionInterpreted>();
			ss.Add(ngf);
			ALL[cfi.fi] = ss;
			return ngf;
		}

		/*
		 * If the new function can be merged into any of the
		 * existing GFunction, do it; otherwise, add it as a
		 * new GFunction.
		 */
		foreach (GFunctionInterpreted gf in ss) {
			if (gf.Merge(ngf)) {
				return gf;
			}
		}
		ss.Add(ngf);
		return ngf;
	}

	/*
	 * Merge the provided GFunctionInterpreted into this one. If the
	 * merge is not possible, this instance is not modified, and
	 * false is returned.
	 */
	bool Merge(GFunctionInterpreted gf)
	{
		/*
		 * Normally, Merge() is not invoked for a distinct function,
		 * but an extra test here is cheap.
		 */
		if (fi != gf.fi) {
			return false;
		}

		/*
		 * We first test the frames, but do not complete their
		 * merge until the operation is confirmed.
		 */
		if (!frame.CanMerge(gf.frame)) {
			return false;
		}

		/*
		 * Since the two GFunctionInterpreted relate to the same
		 * interpreted function, all opcodes are naturally
		 * compatible, except possibly the Call opcodes, which
		 * must be checked.
		 */
		GOp[] spec3 = new GOp[spec.Length];
		for (int i = 0; i < spec.Length; i ++) {
			GOp g1 = spec[i];
			GOp g2 = gf.spec[i];
			if ((g1 is GOpCall) && (g2 is GOpCall)) {
				GOpCall g3 = ((GOpCall)g1).Merge((GOpCall)g2);
				if (g3 == null) {
					return false;
				}
				spec3[i] = g3;
			} else {
				if (g1 is GOpZero) {
					spec3[i] = g2;
				} else {
					spec3[i] = g1;
				}
			}
		}
		spec = spec3;
		AdjustJumps();

		/*
		 * Merge the frames.
		 */
		frame.Merge(gf.frame);
		return true;
	}

	internal override void Print(TextWriter tw, int indent)
	{
		Compiler.Indent(tw, indent);
		tw.WriteLine("{0} ({1})", fi.DebugName, Serial);
		for (int i = 0; i < spec.Length; i ++) {
			spec[i].Print(tw, indent + 1);
		}
	}

	public override string ToString()
	{
		return string.Format("{0} ({1})", f, Serial);
	}

	internal static void PrintAll(TextWriter tw)
	{
		foreach (var kvp in ALL) {
			Compiler.Indent(tw, 1);
			tw.WriteLine("***** {0}", kvp.Key);
			foreach (GFunction gf in kvp.Value) {
				Compiler.Indent(tw, 2);
				tw.WriteLine("-----");
				gf.Print(tw, 3);
			}
		}
	}
}

/*
 * A GOpFrame instance accumulates the local variables and instances
 * for a function. It then generates a C struct for the frame, which
 * allows accessing local items through offsetof() expressions.
 */

class GOpFrame {

	GFunctionInterpreted gf;
	SortedDictionary<int, LocalVariable> variables;
	SortedDictionary<int, LocalInstance> instances;

	internal GOpFrame(GFunctionInterpreted gf)
	{
		this.gf = gf;
		variables = new SortedDictionary<int, LocalVariable>();
		instances = new SortedDictionary<int, LocalInstance>();
	}

	internal void AddLocalVariable(int off, XType ltype)
	{
		AddLocalVariableArray(off, -1, ltype);
	}

	internal void AddLocalVariableArray(int off, int len, XType ltype)
	{
		LocalVariable lv;
		if (!variables.TryGetValue(off, out lv)) {
			variables[off] = new LocalVariable(len, ltype);
			return;
		}
		if (len != lv.len) {
			throw new Exception("length mismatch for local variable array");
		}
		if (ltype.IsRestricted && lv.ltype.IsRestricted) {
			if (ltype != lv.ltype) {
				throw new Exception(string.Format("local variable storage size mismatch: {0} / {1}", ltype.Name, lv.ltype.Name));
			}
		} else if (ltype.IsRestricted || lv.ltype.IsRestricted) {
			throw new Exception(string.Format("local variable storage type mismatch: {0} / {1}", ltype.Name, lv.ltype.Name));
		}
	}

	internal void AddLocalInstance(int off, XType ltype)
	{
		AddLocalInstanceArray(off, -1, ltype);
	}

	internal void AddLocalInstanceArray(int off, int len, XType ltype)
	{
		CCValues.AddTypeLayout(ltype);
		LocalInstance li;
		if (!instances.TryGetValue(off, out li)) {
			instances[off] = new LocalInstance(len, ltype);
			return;
		}
		if (len != li.len) {
			throw new Exception("length mismatch for local instance array");
		}
	}

	internal bool CanMerge(GOpFrame frame)
	{
		foreach (var kvp in frame.variables) {
			int off = kvp.Key;
			LocalVariable lv2 = kvp.Value;
			LocalVariable lv1;
			if (!variables.TryGetValue(off, out lv1)) {
				continue;
			}
			if (lv1.ltype.IsRestricted || lv2.ltype.IsRestricted) {
				if (lv1.ltype != lv2.ltype) {
					return false;
				}
			}
			if (lv1.len != lv2.len) {
				return false;
			}
		}
		foreach (var kvp in frame.instances) {
			int off = kvp.Key;
			LocalInstance li2 = kvp.Value;
			LocalInstance li1;
			if (!instances.TryGetValue(off, out li1)) {
				continue;
			}
			if (li1.ltype != li2.ltype) {
				return false;
			}
		}
		return true;
	}

	internal void Merge(GOpFrame frame)
	{
		foreach (var kvp in frame.variables) {
			int off = kvp.Key;
			LocalVariable lv = kvp.Value;
			if (!variables.ContainsKey(off)) {
				variables[off] = lv;
			}
		}
		foreach (var kvp in frame.instances) {
			int off = kvp.Key;
			LocalInstance li = kvp.Value;
			if (!instances.ContainsKey(off)) {
				instances[off] = li;
			}
		}
	}

	internal void PrintLayout(TextWriter tw)
	{
		tw.WriteLine();
		tw.WriteLine("struct t1f_{0} {{ /* {1} */",
			gf.Serial, Compiler.Encode(gf.fi.DebugName));
		Compiler.Indent(tw, 1);
		tw.WriteLine("void *t1g_header;");

		/*
		 * Variables with restricted types; we output them in
		 * decreasing sizes, so that optimal packing is applied.
		 */
		for (int k = 6; k >= 3; k --) {
			foreach (var kvp in variables) {
				int off = kvp.Key;
				LocalVariable lv = kvp.Value;
				if (!lv.ltype.IsRestricted) {
					continue;
				}
				if (BITSIZE[lv.ltype] != (1 << k)) {
					continue;
				}
				Compiler.Indent(tw, 1);
				tw.Write("{0} t1g_r_{1}",
					Compiler.RestrictedCType(lv.ltype),
					off);
				if (lv.len > 0) {
					tw.Write("[{0}]", lv.len);
				}
				tw.WriteLine(";");
			}
		}

		/*
		 * Variables with reference types.
		 */
		foreach (var kvp in variables) {
			int off = kvp.Key;
			LocalVariable lv = kvp.Value;
			if (lv.ltype.IsRestricted) {
				continue;
			}
			Compiler.Indent(tw, 1);
			tw.Write("void *t1g_p_{0}", off);
			if (lv.len > 0) {
				tw.Write("[{0}]", lv.len);
			}
			tw.WriteLine(";");
		}

		/*
		 * Local instances.
		 */
		foreach (var kvp in instances) {
			int off = kvp.Key;
			LocalInstance li = kvp.Value;
			Compiler.Indent(tw, 1);
			tw.Write("struct t1s_{0} t1g_i_{1}",
				Compiler.Encode(li.ltype.Name), off);
			if (li.len > 0) {
				tw.Write("[{0}]", li.len);
			}
			tw.WriteLine(";");
		}

		tw.WriteLine("};");
	}

	struct LocalVariable {

		internal XType ltype;
		internal int len;

		internal LocalVariable(int len, XType ltype)
		{
			this.ltype = ltype;
			this.len = len;
		}
	}

	struct LocalInstance {

		internal XType ltype;
		internal int len;

		internal LocalInstance(int len, XType ltype)
		{
			this.ltype = ltype;
			this.len = len;
		}
	}

	static SortedDictionary<XType, int> BITSIZE =
		new SortedDictionary<XType, int>();

	static GOpFrame()
	{
		BITSIZE[XType.BOOL] = 8;
		BITSIZE[XType.U8] = 8;
		BITSIZE[XType.U16] = 16;
		BITSIZE[XType.U32] = 32;
		BITSIZE[XType.U64] = 64;
		BITSIZE[XType.I8] = 8;
		BITSIZE[XType.I16] = 16;
		BITSIZE[XType.I32] = 32;
		BITSIZE[XType.I64] = 64;
	}
}

class Generator {

	List<GOp> code;

	internal Generator()
	{
		code = new List<GOp>();
	}

	internal void Add(GOp g)
	{
		code.Add(g);
	}

	internal void Generate(TextWriter w)
	{
		bool stable = true;
		for (;;) {
			int addr = 0;
			foreach (GOp g in code) {
				int a0 = g.addr;
				int l0 = g.length;
				g.addr = addr;
				g.UpdateLength();
				if (a0 != addr || l0 != g.length) {
					stable = false;
				}
				addr += g.length;
			}
			if (stable) {
				break;
			}
		}

		w.WriteLine();
		w.Write("static const uint8_t t1c_code[] = {");
		int col = -1;
		foreach (GOp g in code) {
			string s = g.ToC();
			if (s == null) {
				continue;
			}
			if (col < 0) {
				w.WriteLine();
				w.Write("\t{0}", s);
				col = 8 + s.Length;
			} else {
				w.Write(",");
				col ++;
				int len = s.Length;
				if ((col + len + 1) <= 78) {
					w.Write(" {0}", s);
					col += 1 + len;
				} else {
					w.WriteLine();
					w.Write("\t{0}", s);
					col = 8 + s.Length;
				}
			}
		}
		w.WriteLine();
		w.WriteLine("};");
	}
}

abstract class GOp {

	internal int addr;
	internal int length;

	internal virtual void Print(TextWriter tw, int indent)
	{
		Compiler.Indent(tw, indent);
		tw.WriteLine(ToString());
	}

	internal abstract void UpdateLength();

	internal abstract string ToC();

	internal static int Length7EUnsigned(uint x)
	{
		int n = 1;
		while (x >= 0x80) {
			x >>= 7;
			n ++;
		}
		return n;
	}

	internal static string Expr7EUnsigned(uint x)
	{
		string s = string.Format("0x{0:X2}", x & 0x7F);
		while (x >= 0x80) {
			x >>= 7;
			s = string.Format("0x{0:X2}, {1}",
				(x & 0x7F) | 0x80, s);
		}
		return s;
	}

	internal static int Length7ESigned(long x)
	{
		int n = 1;
		while (x < -64L || x > 63L) {
			x >>= 7;
			n ++;
		}
		return n;
	}

	internal static string Expr7ESigned(long x)
	{
		string s = string.Format("0x{0:X2}", (int)(x & 0x7F));
		while (x < -64L || x > 63L) {
			x >>= 7;
			s = string.Format("0x{0:X2}, {1}",
				(int)(x & 0x7F) | 0x80, s);
		}
		return s;
	}

	/*
	 * Each jump has one argument: the absolute value of the
	 * displacement (7E). Forward and backward jumps use distinct
	 * opcodes.
	 */
	internal const int JUMPIF_BCK = 3;
	internal const int JUMPIF_FWD = 4;
	internal const int JUMPIFNOT_BCK = 5;
	internal const int JUMPIFNOT_FWD = 6;
	internal const int JUMPUNCOND_BCK = 7;
	internal const int JUMPUNCOND_FWD = 8;
}

class GOpZero : GOp {

	internal override void UpdateLength()
	{
	}

	internal override string ToC()
	{
		return null;
	}

	public override string ToString()
	{
		return "unreachable";
	}
}

class GOpJumpSelect : GOp {

	int depth;

	internal GOpJumpSelect(int depth)
	{
		this.depth = depth;
	}

	internal override void UpdateLength()
	{
		throw new Exception("NYI");
	}

	internal override string ToC()
	{
		throw new Exception("NYI");
	}

	public override string ToString()
	{
		return string.Format("jumpselect {0}", depth);
	}
}

class GOpCall : GOp {

	string fname;
	Dispatcher dispatcher;
	SortedDictionary<Function, GFunction> cfm;

	internal GOpCall(string fname, Dispatcher dispatcher,
		SortedDictionary<Function, GFunction> cfm)
	{
		this.dispatcher = dispatcher;
		this.cfm = cfm;
	}

	internal GOpCall Merge(GOpCall gc)
	{
		/*
		 * For the merge to be possible, calls to the same
		 * Function should target the same GFunction.
		 */
		foreach (var kvp in gc.cfm) {
			Function f = kvp.Key;
			GFunction gf2 = kvp.Value;
			GFunction gf1;
			if (!cfm.TryGetValue(f, out gf1)) {
				continue;
			}
			if (gf1 != gf2) {
				return null;
			}
		}

		/*
		 * Compute the merged dispatcher.
		 */
		Dispatcher d3 = Dispatcher.Merge(dispatcher, gc.dispatcher);
		if (d3 == null) {
			return null;
		}

		/*
		 * Merge the maps.
		 */
		SortedDictionary<Function, GFunction> cfm3 =
			new SortedDictionary<Function, GFunction>();
		foreach (var kvp in cfm) {
			cfm3[kvp.Key] = kvp.Value;
		}
		foreach (var kvp in gc.cfm) {
			cfm3[kvp.Key] = kvp.Value;
		}

		return new GOpCall(fname, d3, cfm3);
	}

	internal override void Print(TextWriter tw, int indent)
	{
		Compiler.Indent(tw, indent);
		tw.WriteLine("call {0}", fname);
		foreach (GFunction gf in cfm.Values) {
			Compiler.Indent(tw, indent + 1);
			tw.WriteLine("{0}", gf);
		}
	}

	internal override void UpdateLength()
	{
		throw new Exception("NYI");
	}

	internal override string ToC()
	{
		throw new Exception("NYI");
	}

	public override string ToString()
	{
		return string.Format("call {0}", fname);
	}
}

class GOpConst : GOp {

	internal XValue v;

	internal GOpConst(XValue v)
	{
		this.v = v;
	}

	internal override void UpdateLength()
	{
		throw new Exception("NYI");
	}

	internal override string ToC()
	{
		throw new Exception("NYI");
	}

	public override string ToString()
	{
		return string.Format("const {0}", v);
	}
}

class GOpGetLocal : GOp {

	internal int off;
	XType ltype;

	internal GOpGetLocal(int off, XType ltype)
	{
		this.off = off;
		this.ltype = ltype;
	}

	internal override void UpdateLength()
	{
		throw new Exception("NYI");
	}

	internal override string ToC()
	{
		throw new Exception("NYI");
	}

	public override string ToString()
	{
		return string.Format("getlocal {0} ({1})", off, ltype.Name);
	}
}

class GOpGetLocalIndexed : GOp {

	internal int off;
	internal int len;
	XType ltype;

	internal GOpGetLocalIndexed(int off, int len, XType ltype)
	{
		this.off = off;
		this.len = len;
		this.ltype = ltype;
	}

	internal override void UpdateLength()
	{
		throw new Exception("NYI");
	}

	internal override string ToC()
	{
		throw new Exception("NYI");
	}

	public override string ToString()
	{
		return string.Format("getlocalindex ({0},{1}) ({2})",
			off, len, ltype.Name);
	}
}

abstract class GOpJump : GOp {

	internal GOp target;
	internal int disp;

	internal GOpJump(int disp)
	{
		this.disp = disp;
	}

	internal override void UpdateLength()
	{
		uint ud;
		if (disp < 0) {
			ud = (uint)(addr + length - target.addr);
			length = 1 + Length7EUnsigned(ud);
		} else {
			if (target.addr == 0) {
				/*
				 * No address allocated yet for the target;
				 * we assume that it is close.
				 */
				ud = 0;
			} else {
				ud = (uint)(target.addr - addr - length);
			}
			length = 1 + Length7EUnsigned(ud);
		}
	}

	internal string ToC(int opBck, int opFwd)
	{
		int op;
		uint ud;
		if (disp < 0) {
			op = opBck;
			ud = (uint)(addr + length - target.addr);
		} else {
			op = opFwd;
			ud = (uint)(target.addr - addr - length);
		}
		return string.Format("0x{0:X2}, {1}", op, Expr7EUnsigned(ud));
	}
}

class GOpJumpIf : GOpJump {

	internal GOpJumpIf(int disp)
		: base(disp)
	{
	}

	public override string ToString()
	{
		return string.Format("jumpif disp={0}", disp);
	}

	internal override string ToC()
	{
		return ToC(JUMPIF_BCK, JUMPIF_FWD);
	}
}

class GOpJumpIfNot : GOpJump {

	internal GOpJumpIfNot(int disp)
		: base(disp)
	{
	}

	public override string ToString()
	{
		return string.Format("jumpifnot disp={0}", disp);
	}

	internal override string ToC()
	{
		return ToC(JUMPIFNOT_BCK, JUMPIFNOT_FWD);
	}
}

class GOpJumpUncond : GOpJump {

	internal GOpJumpUncond(int disp)
		: base(disp)
	{
	}

	public override string ToString()
	{
		return string.Format("jump disp={0}", disp);
	}

	internal override string ToC()
	{
		return ToC(JUMPUNCOND_BCK, JUMPUNCOND_FWD);
	}
}

class GOpPutLocal : GOp {

	internal int off;
	internal XType ltype;

	internal GOpPutLocal(int off, XType ltype)
	{
		this.off = off;
		this.ltype = ltype;
	}

	internal override void UpdateLength()
	{
		throw new Exception("NYI");
	}

	internal override string ToC()
	{
		throw new Exception("NYI");
	}

	public override string ToString()
	{
		return string.Format("putlocal {0} ({1})",
			off, ltype.Name);
	}
}

class GOpPutLocalIndexed : GOp {

	internal int off;
	internal int len;
	internal XType ltype;

	internal GOpPutLocalIndexed(int off, int len, XType ltype)
	{
		this.off = off;
		this.len = len;
		this.ltype = ltype;
	}

	internal override void UpdateLength()
	{
		throw new Exception("NYI");
	}

	internal override string ToC()
	{
		throw new Exception("NYI");
	}

	public override string ToString()
	{
		return string.Format("putlocalindex ({0},{1}) ({2})",
			off, len, ltype.Name);
	}
}

class GOpRefLocal : GOp {

	internal int off;
	internal XType ltype;

	internal GOpRefLocal(int off, XType ltype)
	{
		this.off = off;
		this.ltype = ltype;
	}

	internal override void UpdateLength()
	{
		throw new Exception("NYI");
	}

	internal override string ToC()
	{
		throw new Exception("NYI");
	}

	public override string ToString()
	{
		return string.Format("reflocal {0} ({1})",
			off, ltype.Name);
	}
}

class GOpRefLocalIndexed : GOp {

	internal int off;
	internal int len;
	internal XType ltype;

	internal GOpRefLocalIndexed(int off, int len, XType ltype)
	{
		this.off = off;
		this.len = len;
		this.ltype = ltype;
	}

	internal override void UpdateLength()
	{
		throw new Exception("NYI");
	}

	internal override string ToC()
	{
		throw new Exception("NYI");
	}

	public override string ToString()
	{
		return string.Format("reflocalindex ({0},{1}) ({2})",
			off, len, ltype.Name);
	}
}

class GOpRet : GOp {

	internal override void UpdateLength()
	{
		throw new Exception("NYI");
	}

	internal override string ToC()
	{
		throw new Exception("NYI");
	}

	public override string ToString()
	{
		return "ret";
	}
}
