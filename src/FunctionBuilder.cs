using System;
using System.Collections.Generic;

/*
 * A FunctionBuilder instance keeps track of the building of a single
 * interpreted function. It remembers the local variables and instances
 * for that function; it accumulates instructions; it maintains a
 * control-flow stack which is used to resolve jumps.
 *
 * If a builder is tagged "automatic", then it really is an interpreter,
 * which should execute code immediately (as long as all outstanding
 * control-flow structures are resolved).
 */

class FunctionBuilder {

	/*
	 * The "automatic" flag.
	 */
	internal bool Auto {
		get; private set;
	}

	/*
	 * True if the builder has no code and no outstanding control
	 * structure.
	 */
	internal bool IsEmpty {
		get {
			return code.Count == 0 && cfPtr == -1;
		}
	}

	string debugName;
	int[] cfStack;
	int cfPtr;
	List<Opcode> code;
	int numLocalFields;
	List<int> fieldMapping;
	List<XType> localEmbedTypes;
	IDictionary<string, LocalAccessor> locals;
	FunctionBuilder localUpper;
	bool jumpToLast;

	/*
	 * Create an automatic builder.
	 */
	internal FunctionBuilder()
		: this("<automatic>")
	{
		this.Auto = true;
	}

	/*
	 * Create a non-automatic builder with the specified debug name.
	 */
	internal FunctionBuilder(string debugName)
	{
		this.Auto = false;
		this.debugName = debugName;
		cfStack = new int[16];
		code = new List<Opcode>();
		numLocalFields = 0;
		fieldMapping = new List<int>();
		localEmbedTypes = new List<XType>();
		locals = new SortedDictionary<string, LocalAccessor>(
			StringComparer.Ordinal);
		localUpper = null;
		cfPtr = -1;
		jumpToLast = true;
	}

	/*
	 * Share the locals from the provided builder. This shall be
	 * called only on two automatic builders. Note that newly
	 * defined locals in this builder will not be visible from the
	 * outer builder; however, no automatic shadowing will occur
	 * (name collisions still trigger errors).
	 */
	internal void InheritLocals(FunctionBuilder localUpper)
	{
		if (!Auto || !localUpper.Auto) {
			throw new Exception("local sharing is defined only for automatic builders");
		}
		this.localUpper = localUpper;
	}

	/*
	 * Build the function. If there are outstanding control structures
	 * (control-flow stack is not empty), then an exception is thrown.
	 *
	 * This function shall not be called with an automatic builder.
	 */
	internal Function Build()
	{
		if (cfPtr != -1) {
			throw new Exception(string.Format("cannot build {0}: control-flow stack is not empty", debugName));
		}

		/*
		 * jumpToLast is true if there is a jump to the end of
		 * the code, or if the code is empty.
		 */
		if (jumpToLast || code[code.Count - 1].MayFallThrough) {
			Ret();
		}

		/*
		 * fieldMapping contains the mapping from field number
		 * (with array coalescing) to field offset; we want the
		 * inverse mapping.
		 */
		int[] fc = new int[numLocalFields];
		int n = fieldMapping.Count;
		for (int i = 0; i < n; i ++) {
			fc[fieldMapping[i]] = i;
		}

		return new FunctionInterpreted(debugName,
			numLocalFields, localEmbedTypes.ToArray(),
			n, fc, code.ToArray());
	}

	/*
	 * Build the function if possible; this is for an automatic
	 * builder. If the function is empty, or there are outstanding
	 * control-flow structures, then this method returns null.
	 *
	 * When a non-null function is returned, this builder is
	 * automatically cleared and made ready to accumulate new
	 * instructions.
	 */
	internal Function BuildAuto()
	{
		if (cfPtr != -1 || code.Count == 0) {
			return null;
		}
		if (jumpToLast || code[code.Count - 1].MayFallThrough) {
			Ret();
		}
		Function f = new FunctionInterpreted(debugName,
			0, null, 0, IZERO, code.ToArray());
		code.Clear();
		numLocalFields = 0;
		fieldMapping.Clear();
		jumpToLast = true;
		return f;
	}

	static int[] IZERO = new int[0];

	/*
	 * Roll operation on the control-flow stack. At depth 0, this
	 * does nothing; at depth 1, the two top elements are swapped.
	 */
	internal void CSRoll(int depth)
	{
		if (depth > cfPtr) {
			throw new Exception("control-flow stack underflow");
		}
		if (depth < 0) {
			throw new Exception("invalid control-flow stack depth");
		}
		if (depth == 0) {
			return;
		}
		int x = cfStack[cfPtr - depth];
		Array.Copy(cfStack, cfPtr - (depth - 1),
			cfStack, cfPtr - depth, depth);
		cfStack[cfPtr] = x;
	}

	/*
	 * Peek on the control-flow stack at the specified depth (depth 0
	 * is top-of-stack); a copy of the value is pushed onto the
	 * constrol-flow stack.
	 */
	internal void CSPick(int depth)
	{
		if (depth > cfPtr) {
			throw new Exception("control-flow stack underflow");
		}
		if (depth < 0) {
			throw new Exception("invalid control-flow stack depth");
		}
		int x = cfStack[cfPtr - depth];
		CSPush(x);
	}

	void CSPush(int x)
	{
		int len = cfStack.Length;
		if (++ cfPtr == len) {
			int[] ncfStack = new int[len << 1];
			Array.Copy(cfStack, 0, ncfStack, 0, len);
			cfStack = ncfStack;
		}
		cfStack[cfPtr] = x;
	}

	int CSPop()
	{
		if (cfPtr < 0) {
			throw new Exception("control-flow stack underflow");
		}
		return cfStack[cfPtr --];
	}

	/*
	 * Push the current code slot (address of next opcode to add)
	 * as an origin on the control-flow stack.
	 */
	internal void CSPushOrig()
	{
		CSPush(code.Count);
	}

	/*
	 * Push the current code slot (address of next opcode to add)
	 * as a destination on the control-flow stack.
	 */
	internal void CSPushDest()
	{
		CSPush(-code.Count - 1);
	}

	/*
	 * Pop a value from the control-flow stack; it should be an
	 * origin (otherwise, an exception is thrown).
	 */
	internal int CSPopOrig()
	{
		int x = CSPop();
		if (x < 0) {
			throw new Exception("not an origin");
		}
		return x;
	}

	/*
	 * Pop a value from the control-flow stack; it should be a
	 * destination (otherwise, an exception is thrown).
	 */
	internal int CSPopDest()
	{
		int x = CSPop();
		if (x >= 0) {
			throw new Exception("not a destination");
		}
		return -x - 1;
	}

	void AddLocal(string name, LocalAccessor la)
	{
		FunctionBuilder fb = this;
		while (fb != null) {
			if (fb.locals.ContainsKey(name)) {
				throw new Exception(string.Format("local name collision on: {0}", name));
			}
			fb = fb.localUpper;
		}
		locals[name] = la;
	}

	/*
	 * Define a new local field. The type may be null, which
	 * is equivalent to XType.OBJECT.
	 */
	internal void DefLocalField(string name, XType ltype)
	{
		if (Auto) {
			ContainerField cf = new ContainerField(name, ltype);
			AddLocal(name,
				new LocalAccessorInterpreterFieldGet(this, cf));
			AddLocal("->" + name,
				new LocalAccessorInterpreterFieldPut(this, cf));
		} else {
			int off = numLocalFields ++;
			fieldMapping.Add(off);
			AddLocal(name,
				new LocalAccessorFieldGet(this, off));
			AddLocal("->" + name,
				new LocalAccessorFieldPut(this, off, ltype));
		}
	}

	/*
	 * Define a new local field array. The type may be null, which
	 * is equivalent to XType.OBJECT.
	 */
	internal void DefLocalFieldArray(string name, int len, XType ltype)
	{
		if (len <= 0) {
			throw new Exception(string.Format("invalid local array length: {0}", len));
		}
		if (Auto) {
			ContainerFieldArray cfa =
				new ContainerFieldArray(name, len, ltype);
			AddLocal(name + "@",
				new LocalAccessorInterpreterFieldArrayGet(
					this, cfa));
			AddLocal("->" + name + "@",
				new LocalAccessorInterpreterFieldArrayPut(
					this, cfa));
		} else {
			int off = numLocalFields;
			numLocalFields += len;
			fieldMapping.Add(off);
			AddLocal(name + "@",
				new LocalAccessorFieldArrayGet(
					this, off, len));
			AddLocal("->" + name + "@",
				new LocalAccessorFieldArrayPut(
					this, off, len, ltype));
		}
	}

	/*
	 * Define a new local instance.
	 */
	internal void DefLocalEmbed(string name, XType ltype)
	{
		if (Auto) {
			XObject obj = ltype.NewInstance();
			AddLocal(name + "&",
				new LocalAccessorInterpreterEmbedRef(
					this, obj));
		} else {
			int off = localEmbedTypes.Count;
			localEmbedTypes.Add(ltype);
			AddLocal(name + "&",
				new LocalAccessorEmbedRef(this, off));
		}
	}

	/*
	 * Define a new array of local instances.
	 */
	internal void DefLocalEmbedArray(string name, int len, XType ltype)
	{
		if (len <= 0) {
			throw new Exception(string.Format("invalid local array length: {0}", len));
		}
		if (Auto) {
			XObject[] objs = new XObject[len];
			for (int i = 0; i < len; i ++) {
				objs[i] = ltype.NewInstance();
			}
			AddLocal(name + "@&",
				new LocalAccessorInterpreterEmbedArrayRef(
					this, objs));
		} else {
			int off = localEmbedTypes.Count;
			for (int i = 0; i < len; i ++) {
				localEmbedTypes.Add(ltype);
			}
			AddLocal(name + "@&",
				new LocalAccessorEmbedArrayRef(this, off, len));
		}
	}

	internal void Add(Opcode op)
	{
		code.Add(op);
		jumpToLast = false;
	}

	/*
	 * Add a "special" opcode, that runs the provided native code.
	 */
	internal void Special(OpcodeSpecial.NativeRun code)
	{
		Add(new OpcodeSpecial(code));
	}

	/*
	 * Add a "push literal value" opcode.
	 */
	internal void Literal(XValue xv)
	{
		Add(new OpcodeConst(xv));
	}

	/*
	 * Add a "call function" opcode.
	 */
	internal void Call(string name)
	{
		Add(new OpcodeCall(name));
	}

	/*
	 * If the provided name is an accessor for a local field or
	 * instance, then this method adds the invocation of that accessor
	 * and returns true; otherwise, it does nothing and returns false.
	 */
	internal bool DoLocal(string name)
	{
		LocalAccessor la;
		FunctionBuilder fb = this;
		while (fb != null) {
			if (fb.locals.TryGetValue(name, out la)) {
				la.Apply(this);
				return true;
			}
			fb = fb.localUpper;
		}
		return false;
	}

	/*
	 * Add a "ret" opcode.
	 */
	internal void Ret()
	{
		Add(new OpcodeRet());
	}

	/*
	 * Add an unconditional forward jump; this also pushes an origin
	 * on the control-flow stack.
	 */
	internal void Ahead()
	{
		CSPushOrig();
		Add(new OpcodeJumpUncond());
	}

	/*
	 * Add a conditional forward jump; this also pushes an origin
	 * on the control-flow stack. The jump will be taken if the
	 * top-of-stack boolean is true.
	 */
	internal void AheadIf()
	{
		CSPushOrig();
		Add(new OpcodeJumpIf());
	}

	/*
	 * Add a conditional forward jump; this also pushes an origin
	 * on the control-flow stack. The jump will be taken if the
	 * top-of-stack boolean is false.
	 */
	internal void AheadIfNot()
	{
		CSPushOrig();
		Add(new OpcodeJumpIfNot());
	}

	/*
	 * Pop an origin from the control-flow stack, and resolve it
	 * to the current address.
	 */
	internal void Then()
	{
		int x = CSPopOrig();
		code[x].ResolveJump(code.Count - x - 1);
		jumpToLast = true;
	}

	/*
	 * Push the current address as a destination (to be used as target
	 * for an ulterior backward jump).
	 */
	internal void Begin()
	{
		CSPushDest();
	}

	/*
	 * Add an unconditional backward jump, resolved against the
	 * destination currently at the top of the control-flow stack.
	 */
	internal void Again()
	{
		int x = CSPopDest();
		Add(new OpcodeJumpUncond(x - code.Count - 1));
	}

	/*
	 * Add a conditional backward jump, resolved against the
	 * destination currently at the top of the control-flow stack.
	 * The jump will be taken if the top-of-stack boolean is true.
	 */
	internal void AgainIf()
	{
		int x = CSPopDest();
		Add(new OpcodeJumpIf(x - code.Count - 1));
	}

	/*
	 * Add a conditional backward jump, resolved against the
	 * destination currently at the top of the control-flow stack.
	 * The jump will be taken if the top-of-stack boolean is false.
	 */
	internal void AgainIfNot()
	{
		int x = CSPopDest();
		Add(new OpcodeJumpIfNot(x - code.Count - 1));
	}

	abstract class LocalAccessor {

		internal FunctionBuilder Owner {
			get;
			private set;
		}

		internal LocalAccessor(FunctionBuilder owner)
		{
			this.Owner = owner;
		}

		internal abstract void Apply(FunctionBuilder dest);
	}

	class LocalAccessorFieldGet : LocalAccessor {

		int off;

		internal LocalAccessorFieldGet(FunctionBuilder owner, int off)
			: base(owner)
		{
			this.off = off;
		}

		internal override void Apply(FunctionBuilder dest)
		{
			dest.Add(new OpcodeGetLocal(off));
		}
	}

	class LocalAccessorFieldPut : LocalAccessor {

		int off;
		XType ltype;

		internal LocalAccessorFieldPut(
			FunctionBuilder owner, int off, XType ltype)
			: base(owner)
		{
			this.off = off;
			this.ltype = ltype;
		}

		internal override void Apply(FunctionBuilder dest)
		{
			dest.Add(new OpcodePutLocal(off, ltype));
		}
	}

	class LocalAccessorFieldArrayGet : LocalAccessor {

		int off, len;

		internal LocalAccessorFieldArrayGet(
			FunctionBuilder owner, int off, int len)
			: base(owner)
		{
			this.off = off;
			this.len = len;
		}

		internal override void Apply(FunctionBuilder dest)
		{
			dest.Add(new OpcodeGetLocalIndexed(off, len));
		}
	}

	class LocalAccessorFieldArrayPut : LocalAccessor {

		int off, len;
		XType ltype;

		internal LocalAccessorFieldArrayPut(
			FunctionBuilder owner, int off, int len, XType ltype)
			: base(owner)
		{
			this.off = off;
			this.len = len;
			this.ltype = ltype;
		}

		internal override void Apply(FunctionBuilder dest)
		{
			dest.Add(new OpcodePutLocalIndexed(off, len, ltype));
		}
	}

	// FIXME: accessor "x*" to initialize an array instance (field array)

	class LocalAccessorEmbedRef : LocalAccessor {

		int off;

		internal LocalAccessorEmbedRef(FunctionBuilder owner, int off)
			: base(owner)
		{
			this.off = off;
		}

		internal override void Apply(FunctionBuilder dest)
		{
			dest.Add(new OpcodeRefLocal(off));
		}
	}

	class LocalAccessorEmbedArrayRef : LocalAccessor {

		int off, len;

		internal LocalAccessorEmbedArrayRef(
			FunctionBuilder owner, int off, int len)
			: base(owner)
		{
			this.off = off;
			this.len = len;
		}

		internal override void Apply(FunctionBuilder dest)
		{
			dest.Add(new OpcodeRefLocalIndexed(off, len));
		}
	}

	// FIXME: accessor "x*" to initialize an array instance (embed array)

	class ContainerField {

		string debugName;
		XValue val;
		XType ltype;

		internal ContainerField(string debugName, XType ltype)
		{
			this.debugName = debugName;
			this.ltype = ltype;
			if (ltype == null) {
				val.Clear();
			} else {
				val.Clear(ltype);
			}
		}

		internal void Get(CPU cpu)
		{
			if (val.IsUninitialized) {
				throw new Exception(string.Format("reading uninitialized local variable {0}", debugName));
			}
			cpu.Push(val);
		}

		internal void Put(CPU cpu)
		{
			XValue xv = cpu.Pop();
			if (ltype != null && !xv.VType.IsSubTypeOf(ltype)) {
				throw new Exception(string.Format("write of value of type {0} into local variable {1} of type {2}", xv.VType.Name, debugName, ltype.Name));
			}
			val = xv;
		}
	}

	class ContainerFieldArray {

		string debugName;
		XValue[] vals;
		XType ltype;

		internal ContainerFieldArray(string debugName,
			int len, XType ltype)
		{
			this.debugName = debugName;
			this.ltype = ltype;
			vals = new XValue[len];
			for (int i = 0; i < len; i ++) {
				if (ltype == null) {
					vals[i].Clear();
				} else {
					vals[i].Clear(ltype);
				}
			}
		}

		int PopIndex(CPU cpu)
		{
			int k = cpu.Pop().Int;
			if (k < 0 || k >= vals.Length) {
				throw new Exception(string.Format("index out of bounds for local {0}: {1} (max: {2})", debugName, k, vals.Length));
			}
			return k;
		}

		internal void GetIndexed(CPU cpu)
		{
			int k = PopIndex(cpu);
			cpu.Push(vals[k]);
		}

		internal void PutIndexed(CPU cpu)
		{
			int k = PopIndex(cpu);
			XValue xv = cpu.Pop();
			if (ltype != null && !xv.VType.IsSubTypeOf(ltype)) {
				throw new Exception(string.Format("write of value of type {0} into local variable {1} (index: {2}) of type {3}", xv.VType.Name, debugName, k, ltype.Name));
			}
			vals[k] = xv;
		}
	}

	class LocalAccessorInterpreterFieldGet : LocalAccessor {

		ContainerField cf;

		internal LocalAccessorInterpreterFieldGet(
			FunctionBuilder owner, ContainerField cf)
			: base(owner)
		{
			this.cf = cf;
		}

		internal override void Apply(FunctionBuilder dest)
		{
			dest.Add(new OpcodeSpecial(cf.Get));
		}
	}

	class LocalAccessorInterpreterFieldPut : LocalAccessor {

		ContainerField cf;

		internal LocalAccessorInterpreterFieldPut(
			FunctionBuilder owner, ContainerField cf)
			: base(owner)
		{
			this.cf = cf;
		}

		internal override void Apply(FunctionBuilder dest)
		{
			dest.Add(new OpcodeSpecial(cf.Put));
		}
	}

	class LocalAccessorInterpreterFieldArrayGet : LocalAccessor {

		ContainerFieldArray cfa;

		internal LocalAccessorInterpreterFieldArrayGet(
			FunctionBuilder owner, ContainerFieldArray cfa)
			: base(owner)
		{
			this.cfa = cfa;
		}

		internal override void Apply(FunctionBuilder dest)
		{
			dest.Add(new OpcodeSpecial(cfa.GetIndexed));
		}
	}

	class LocalAccessorInterpreterFieldArrayPut : LocalAccessor {

		ContainerFieldArray cfa;

		internal LocalAccessorInterpreterFieldArrayPut(
			FunctionBuilder owner, ContainerFieldArray cfa)
			: base(owner)
		{
			this.cfa = cfa;
		}

		internal override void Apply(FunctionBuilder dest)
		{
			dest.Add(new OpcodeSpecial(cfa.PutIndexed));
		}
	}

	// FIXME: accessor "x*" to initialize an array instance (field array)

	class LocalAccessorInterpreterEmbedRef : LocalAccessor {

		XObject obj;

		internal LocalAccessorInterpreterEmbedRef(
			FunctionBuilder owner, XObject obj)
			: base(owner)
		{
			this.obj = obj;
		}

		internal override void Apply(FunctionBuilder dest)
		{
			dest.Add(new OpcodeSpecial(cpu => {
				cpu.Push(new XValue(obj));
			}));
		}
	}

	class LocalAccessorInterpreterEmbedArrayRef : LocalAccessor {

		XObject[] objs;

		internal LocalAccessorInterpreterEmbedArrayRef(
			FunctionBuilder owner, XObject[] objs)
			: base(owner)
		{
			this.objs = objs;
		}

		internal override void Apply(FunctionBuilder dest)
		{
			dest.Add(new OpcodeSpecial(cpu => {
				int k = cpu.Pop().Int;
				if (k < 0 || k >= objs.Length) {
					throw new Exception(string.Format("index out of bounds for local instance array: {0} (max: {1})", k, objs.Length));
				}
				cpu.Push(new XValue(objs[k]));
			}));
		}
	}

	// FIXME: accessor "x*" to initialize an array instance (embed array)
}
