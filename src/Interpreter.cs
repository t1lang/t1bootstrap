using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/*
 * The Interpreter class contains the code which can process source
 * code and build functions. An instance is created over a given source
 * stream. Static state is maintained to keep track of defined export
 * lists.
 */

class Interpreter {

	/*
	 * Current namespace.
	 */
	internal string CurrentNamespace {
		get;
		private set;
	}

	/*
	 * Current function builder.
	 */
	internal FunctionBuilder CurrentBuilder {
		get;
		private set;
	}

	Lexer lexer;
	IDictionary<string, Alias> aliases;
	string delayedToken;
	List<FunctionBuilder> savedBuilders;
	XType currentStruct;
	List<XType> savedStructs;

	internal Interpreter(TextReader sourceStream)
	{
		lexer = new Lexer(sourceStream);
		aliases = new SortedDictionary<string, Alias>(
			StringComparer.Ordinal);
		delayedToken = null;
		savedBuilders = new List<FunctionBuilder>();
		currentStruct = null;
		savedStructs = new List<XType>();

		CurrentNamespace = "def";
		Import("std");
	}

	internal void Run()
	{
		CPU cpu = new CPU(this);
		savedBuilders.Clear();
		CurrentBuilder = new FunctionBuilder();
		InterpreterStep is0 = new InterpreterStep(this, null);
		cpu.Enter(new Opcode[] {
				new OpcodeSpecial(is0.Run),
				new OpcodeJumpUncond(-2)
			}, 0, null);
		while (!cpu.IsFinished) {
			Opcode op = cpu.ipBuf[cpu.ipOff ++];
			op.Run(cpu);
		}
	}

	void ShareAutoLocals()
	{
		int n = savedBuilders.Count;
		for (int i = n - 1; i >= 0; i --) {
			if (savedBuilders[i].Auto) {
				CurrentBuilder.InheritLocals(savedBuilders[i]);
				break;
			}
		}
	}

	internal void AddAlias(string rawName, string qualifiedName)
	{
		Alias a;
		if (!aliases.TryGetValue(rawName, out a)) {
			aliases[rawName] = new Alias(qualifiedName, true);
			return;
		}
		if (!a.provenanceFlag) {
			a.destination = qualifiedName;
			a.provenanceFlag = true;
			return;
		}
		if (a.destination == rawName) {
			return;
		}
		throw new Exception(string.Format("alias conflict: {0} -> {1} / {2}", rawName, a.destination, qualifiedName));
	}

	internal void Import(string nsName)
	{
		List<string> el;
		if (!EXPORT_LISTS.TryGetValue(nsName, out el)) {
			throw new Exception(string.Format("no such export list: {0}", nsName));
		}
		foreach (string qualifiedName in el) {
			Alias a;
			string rawName = Names.GetRaw(qualifiedName);
			if (!aliases.TryGetValue(rawName, out a)) {
				aliases[rawName] =
					new Alias(qualifiedName, false);
				continue;
			}
			if (a.destination == qualifiedName) {
				continue;
			}
			if (a.provenanceFlag) {
				continue;
			}
			a.destination = null;
		}
	}

	/*
	 * Export a name: the name is added to the export list for its
	 * namespace. If the provided name is unqualified, the current
	 * namespace is used to complete it.
	 */
	internal void AddExport(string name)
	{
		if (!Names.HasNamespace(name)) {
			name = Names.Make(CurrentNamespace, name);
		}
		AddExportQualified(name);
	}

	/*
	 * Export a name: that name must be qualified. It is added to the
	 * export list of its namespace.
	 */
	internal static void AddExportQualified(string name)
	{
		string nname = Names.GetNamespace(name);
		if (nname == null) {
			throw new Exception(string.Format("cannot export unqualified name: {0}", name));
		}
		List<string> el;
		if (EXPORT_LISTS.TryGetValue(nname, out el)) {
			if (el.Contains(name)) {
				return;
			}
		} else {
			el = new List<string>();
			EXPORT_LISTS[nname] = el;
		}
		el.Add(name);
	}

	/*
	 * Complete a name if needed: if the name is raw, then aliases
	 * are applied; if no aliases is defined, the current namespace
	 * is used.
	 */
	internal string CompleteName(string name)
	{
		if (Names.HasNamespace(name)) {
			return name;
		}
		Alias a;
		if (aliases.TryGetValue(name, out a)) {
			return a.destination;
		} else {
			return Names.Make(CurrentNamespace, name);
		}
	}

	/*
	 * Get next token; null is returned on EOF.
	 */
	internal string NextTokenOrEOF()
	{
		string t = delayedToken;
		if (t != null) {
			delayedToken = null;
			return t;
		}
		t = lexer.Next();
		return t;
	}

	/*
	 * Get next token; throw an exception on EOF.
	 */
	internal string NextToken()
	{
		string t = NextTokenOrEOF();
		if (t == null) {
			throw new Exception("unexpected end of stream");
		}
		return t;
	}

	/*
	 * Push back a token, to read it again.
	 */
	internal void PushbackToken(string t)
	{
		delayedToken = t;
	}

	/*
	 * Check whether a given token is a literal constant (literal
	 * string, character constant, boolean constant or numerical
	 * constant).
	 */
	internal static bool IsLiteralConstant(string t)
	{
		if (t.Length >= 1) {
			int fc = t[0];
			if (fc == '`' || fc == '"') {
				return true;
			}
			if (t.Length >= 2 && (fc == '+' || fc == '-')) {
				fc = t[1];
			}
			if (fc >= '0' && fc <= '9') {
				return true;
			}
			if (t == "true" || t == "false") {
				return true;
			}
		}
		return false;
	}

	/*
	 * Interpret a token as a constant value (numerical constant,
	 * boolean, literal string). If the token is not such a constant,
	 * the returned value is uninitialized.
	 */
	internal static XValue ParseConst(string t)
	{
		if (t.Length == 0) {
			return new XValue((XObject)null);
		}
		if (t == "true") {
			return new XValue(XType.BOOL, 1);
		}
		if (t == "false") {
			return new XValue(XType.BOOL, 0);
		}
		if (t[0] == '"') {
			return new XValue(t.Substring(1));
		}
		if (t[0] == '`') {
			int cp = t[1];
			if (cp > 0x7F) {
				throw new Exception("non-ASCII character constant");
			}
			return (byte)cp;
		}
		bool neg = false;
		if (t[0] == '+') {
			t = t.Substring(1);
		} else if (t[0] == '-') {
			neg = true;
			t = t.Substring(1);
		}
		if (t.Length == 0 || t[0] < '0' || t[0] > '9') {
			return new XValue((XObject)null);
		}

		XType bt = XType.INT;
		ZInt min = Int32.MinValue;
		ZInt max = Int32.MaxValue;
		if (t.EndsWith("u8") || t.EndsWith("U8")) {
			t = t.Substring(0, t.Length - 2);
			bt = XType.U8;
			min = 0;
			max = Byte.MaxValue;
		} else if (t.EndsWith("u16") || t.EndsWith("U16")) {
			t = t.Substring(0, t.Length - 3);
			bt = XType.U16;
			min = 0;
			max = UInt16.MaxValue;
		} else if (t.EndsWith("u32") || t.EndsWith("U32")) {
			t = t.Substring(0, t.Length - 3);
			bt = XType.U32;
			min = 0;
			max = UInt32.MaxValue;
		} else if (t.EndsWith("u64") || t.EndsWith("U64")) {
			t = t.Substring(0, t.Length - 3);
			bt = XType.U64;
			min = 0;
			max = UInt64.MaxValue;
		} else if (t.EndsWith("i8") || t.EndsWith("I8")) {
			t = t.Substring(0, t.Length - 2);
			bt = XType.I8;
			min = SByte.MinValue;
			max = SByte.MaxValue;
		} else if (t.EndsWith("i16") || t.EndsWith("I16")) {
			t = t.Substring(0, t.Length - 3);
			bt = XType.I16;
			min = Int16.MinValue;
			max = Int16.MaxValue;
		} else if (t.EndsWith("i32") || t.EndsWith("I32")) {
			t = t.Substring(0, t.Length - 3);
			bt = XType.I32;
			min = Int32.MinValue;
			max = Int32.MaxValue;
		} else if (t.EndsWith("i64") || t.EndsWith("I64")) {
			t = t.Substring(0, t.Length - 3);
			bt = XType.I64;
			min = Int64.MinValue;
			max = Int64.MaxValue;
		}

		ZInt x = ZInt.Parse(t);
		if (neg) {
			x = -x;
		}
		if (x < min || x > max) {
			throw new Exception(string.Format("value {0} is out of allowed range for type {1}", x, bt.Name));
		}

		return new XValue(bt, x.ToULong);
	}

	internal void PopBuilder()
	{
		int n = savedBuilders.Count;
		if (n > 0) {
			CurrentBuilder = savedBuilders[n - 1];
			savedBuilders.RemoveAt(n - 1);
		} else {
			CurrentBuilder = null;
		}
	}

	/*
	 * Parse a name, which may also be a literal string; in the latter
	 * case, the string contents are returned.
	 */
	internal string ParseName()
	{
		string t = NextToken();

		/*
		 * When parsing a name, we accept a literal string, and
		 * then use the string contents.
		 */
		if (t.Length > 0 && t[0] == '"') {
			return t.Substring(1);
		}

		/*
		 * Literal constants are otherwise forbidden.
		 */
		if (IsLiteralConstant(t)) {
			throw new Exception(string.Format("expected a name, got a literal constant: {0}", t));
		}

		return t;
	}

	/*
	 * Parse a name. If a literal string is encountered, the string
	 * contents are returned. Otherwise, the token must be a name,
	 * and it will be expanded with the current namespace if it is not
	 * qualified.
	 */
	internal string ParseAndCompleteName()
	{
		string t = NextToken();
		if (t.Length > 0 && t[0] == '"') {
			return t.Substring(1);
		}
		if (IsLiteralConstant(t)) {
			throw new Exception(string.Format("expected a name, got a literal constant: {0}", t));
		}
		return CompleteName(t);
	}

	/*
	 * Implementation of std:::.
	 */
	internal void StartFunction(CPU cpu)
	{
		string name = ParseAndCompleteName();
		bool immediate = false;
		bool export = false;
		for (;;) {
			string t = ParseName();
			switch (t) {
			case "<immediate>":
				immediate = true;
				continue;
			case "<export>":
				export = true;
				continue;
			case "(":
				break;
			default:
				throw new Exception(string.Format("unexpected token: {0}", t));
			}
			break;
		}

		/*
		 * Create a new automatic builder for gathering the
		 * parameter types.
		 */
		cpu.PushMarker();
		savedBuilders.Add(CurrentBuilder);
		CurrentBuilder = new FunctionBuilder();
		ShareAutoLocals();
		InterpreterStep is1 = new InterpreterStep(this, ")");

		/*
		 * Make the CPU enter the loop for that first builder,
		 * with a continuation to the rest of function building.
		 */
		cpu.Enter(new Opcode[] {
				new OpcodeSpecial(cpu2 => {
					ContinueFunction(cpu2,
						name, immediate, export);
				}),
				new OpcodeRet()
			}, 0, null);
		cpu.Enter(new Opcode[] {
				new OpcodeSpecial(is1.Run),
				new OpcodeJumpUncond(-2)
			}, 0, null);
	}

	void ContinueFunction(CPU cpu, string name, bool immediate, bool export)
	{
		/*
		 * Gather parameter types left by the first builder.
		 */
		XValue[] vv = cpu.PopToMarker();
		XType[] pp = new XType[vv.Length];
		for (int i = 0; i < vv.Length; i ++) {
			pp[i] = vv[i].XTypeInstance;
		}

		if (immediate && pp.Length > 0) {
			throw new Exception(string.Format("cannot define immediate function {0} with {1} parameter(s)", name, pp.Length));
		}

		/*
		 * Make the second builder, for the function body. This is
		 * a non-automatic builder; the intended function name is
		 * used as debug name.
		 */
		savedBuilders.Add(CurrentBuilder);
		CurrentBuilder = new FunctionBuilder(name);
		InterpreterStep is2 = new InterpreterStep(this, ";");

		/*
		 * We exit the hook we had put in place to get here, then
		 * put a new hook for function registration, and create
		 * the builder loop.
		 */
		cpu.Exit();
		cpu.Enter(new Opcode[] {
				new OpcodeSpecial(cpu2 => {
					EndFunction(cpu2, name, pp, immediate,
						export, is2.BuiltFunction);
				}),
				new OpcodeRet()
			}, 0, null);
		cpu.Enter(new Opcode[] {
				new OpcodeSpecial(is2.Run),
				new OpcodeJumpUncond(-2)
			}, 0, null);
	}

	void EndFunction(CPU cpu, string name, XType[] parameters,
		bool immediate, bool export, Function f)
	{
		cpu.Exit();
		if (immediate) {
			Function.RegisterImmediate(name, f);
		} else {
			Function.Register(name, parameters, f);
		}
		if (export) {
			AddExportQualified(name);
		}
	}

	/*
	 * Implementation of std::run-interpreter.
	 */
	internal void NativeRunInterpreter(CPU cpu)
	{
		string end = cpu.Pop().String;
		cpu.PushMarker();
		savedBuilders.Add(CurrentBuilder);
		CurrentBuilder = new FunctionBuilder();
		ShareAutoLocals();
		InterpreterStep is1 = new InterpreterStep(this, ")");

		/*
		 * Make the CPU enter the loop for that first builder,
		 * with a continuation to the rest of function building.
		 */
		cpu.Enter(new Opcode[] {
				new OpcodeSpecial(cpu2 => {
					FinishInterpreter(cpu2);
				}),
				new OpcodeRet()
			}, 0, null);
		cpu.Enter(new Opcode[] {
				new OpcodeSpecial(is1.Run),
				new OpcodeJumpUncond(-2)
			}, 0, null);
	}

	void FinishInterpreter(CPU cpu)
	{
		XValue[] vv = cpu.PopToMarker();
		XArrayGeneric a = new XArrayGeneric(XType.ARRAY_OBJECT);
		a.Init(new XBufferGen(vv), 0, vv.Length);
		cpu.Push(new XValue(a));
	}

	/*
	 * Implementation of std::namespace.
	 */
	internal void NativeNamespace(CPU cpu)
	{
		string name = ParseName();
		if (Names.HasNamespace(name)) {
			throw new Exception(string.Format("not a namespace: {0}", name));
		}
		CurrentNamespace = name;
		aliases.Clear();
		Import("std");
	}

	/*
	 * Implementation of std::import.
	 */
	internal void NativeImport(CPU cpu)
	{
		string name = ParseName();
		if (Names.HasNamespace(name)) {
			throw new Exception(string.Format("not a namespace: {0}", name));
		}
		Import(name);
	}

	/*
	 * Implementation of std::{.
	 */
	internal void NativeSimpleLocals(CPU cpu)
	{
		for (;;) {
			string t = ParseName();
			if (t == "}") {
				break;
			}
			CurrentBuilder.DefLocalField(t, null);
		}
	}

	/*
	 * Implementation of std::->.
	 */
	internal void NativeTo(CPU cpu)
	{
		string t = ParseName();
		if (t == "{") {
			List<string> names = new List<string>();
			for (;;) {
				t = ParseName();
				if (t == "}") {
					break;
				}
				names.Add(t);
				CurrentBuilder.DefLocalField(t, null);
			}
			for (int i = names.Count - 1; i >= 0; i --) {
				CurrentBuilder.DoLocal("->" + names[i]);
			}
		} else if (t == "[") {
			List<string> names = new List<string>();
			for (;;) {
				t = ParseName();
				if (t == "]") {
					break;
				}
				names.Add(t);
			}
			for (int i = names.Count - 1; i >= 0; i --) {
				t = names[i];
				if (!CurrentBuilder.DoLocal("->" + t)) {
					throw new Exception(string.Format("no such local variable: {0}", t));
				}
			}
		} else {
			if (!CurrentBuilder.DoLocal("->" + t)) {
				throw new Exception(string.Format("no such local variable: {0}", t));
			}
		}
	}

	/*
	 * Implementation of std::define-typed-local.
	 */
	internal void NativeAddTypedLocal(CPU cpu)
	{
		XArrayGeneric a = cpu.Pop().XObject as XArrayGeneric;
		bool embed = cpu.Pop().Bool;
		string name = cpu.Pop().String;
		if (a.Length == 1) {
			XType xt = a[0].XTypeInstance;
			if (embed) {
				CurrentBuilder.DefLocalEmbed(name, xt);
			} else {
				CurrentBuilder.DefLocalField(name, xt);
			}
		} else if (a.Length == 2) {
			int size = a[0].Int;
			XType xt = a[1].XTypeInstance;
			if (embed) {
				CurrentBuilder.DefLocalEmbedArray(
					name, size, xt);
			} else {
				CurrentBuilder.DefLocalFieldArray(
					name, size, xt);
			}
		} else {
			throw new Exception("invalid type definition");
		}
	}

	/*
	 * Implementation of std::ret.
	 */
	internal void NativeRet(CPU cpu)
	{
		CurrentBuilder.Ret();
	}

	/*
	 * Implementation of std::ret.
	 */
	internal void NativeAhead(CPU cpu)
	{
		CurrentBuilder.Ahead();
	}

	/*
	 * Implementation of std::ret.
	 */
	internal void NativeIf(CPU cpu)
	{
		CurrentBuilder.AheadIfNot();
	}

	/*
	 * Implementation of std::ret.
	 */
	internal void NativeIfNot(CPU cpu)
	{
		CurrentBuilder.AheadIf();
	}

	/*
	 * Implementation of std::begin.
	 */
	internal void NativeThen(CPU cpu)
	{
		CurrentBuilder.Then();
	}

	/*
	 * Implementation of std::begin.
	 */
	internal void NativeBegin(CPU cpu)
	{
		CurrentBuilder.Begin();
	}

	/*
	 * Implementation of std::begin.
	 */
	internal void NativeAgain(CPU cpu)
	{
		CurrentBuilder.Again();
	}

	/*
	 * Implementation of std::begin.
	 */
	internal void NativeElse(CPU cpu)
	{
		CurrentBuilder.Ahead();
		CurrentBuilder.CSRoll(1);
		CurrentBuilder.Then();
	}

	/*
	 * Implementation of std::begin.
	 */
	internal void NativeWhile(CPU cpu)
	{
		CurrentBuilder.AheadIfNot();
		CurrentBuilder.CSRoll(1);
	}

	/*
	 * Implementation of std::begin.
	 */
	internal void NativeRepeat(CPU cpu)
	{
		CurrentBuilder.Again();
		CurrentBuilder.Then();
	}

	/*
	 * Implementation of std::'.
	 */
	internal void NativeQuote(CPU cpu)
	{
		string t = NextToken();
		XValue cv = ParseConst(t);
		if (!cv.IsUninitialized) {
			CurrentBuilder.Special(cpu2 => {
				cpu2.Interpreter.CurrentBuilder.Literal(cv);
			});
		} else {
			t = CompleteName(t);
			CurrentBuilder.Special(cpu2 => {
				Function fi = Function.LookupImmediate(t);
				if (fi != null) {
					fi.Run(cpu2);
				} else {
					cpu2.Interpreter.CurrentBuilder.Call(t);
				}
			});
		}
	}

	/*
	 * Implementation of std::next-token.
	 */
	internal void NativeNextToken(CPU cpu)
	{
		cpu.Push(new XValue(NextToken()));
	}

	/*
	 * Implementation of std::to-complete-name.
	 */
	internal void NativeToCompleteName(CPU cpu)
	{
		string t = cpu.Pop().String;
		if (t.StartsWith("\"")) {
			t = t.Substring(1);
		} else if (IsLiteralConstant(t)) {
			throw new Exception(string.Format("expected a name, got a literal constant: {0}", t));
		} else {
			t = CompleteName(t);
		}
		cpu.Push(new XValue(t));
	}

	/*
	 * Implementation of std::to-type-element.
	 */
	internal void NativeToTypeElement(CPU cpu)
	{
		string t = cpu.Pop().String;
		XValue xv;
		if (t.StartsWith("\"")) {
			xv = new XValue(XType.Lookup(t));
		} else {
			xv = ParseConst(t);
			if (xv.IsUninitialized) {
				xv = new XValue(XType.Lookup(CompleteName(t)));
			}
		}
		cpu.Push(xv);
	}

	/*
	 * Implementation of std::check-type-element.
	 */
	internal void NativeCheckTypeElements(CPU cpu)
	{
		XArrayGeneric a = cpu.Pop().XObject as XArrayGeneric;
		int n = a.Length;
		if (n == 0) {
			cpu.Push(false);
			return;
		}
		if (n == 1) {
			XValue xv = a[0];
			if (xv.VType == XType.INT) {
				cpu.Push(false);
				return;
			}
			if (xv.VType == XType.XTYPE) {
				cpu.Push(true);
				return;
			}
			throw new Exception(string.Format("unexpected object of type {0} in type definition", xv.VType.Name));
		}
		if (n == 2) {
			XValue xv = a[0];
			if (xv.VType != XType.INT) {
				throw new Exception(string.Format("unexpected object of type {0} in type definition", xv.VType.Name));
			}
			xv = a[1];
			if (xv.VType != XType.XTYPE) {
				throw new Exception(string.Format("unexpected object of type {0} in type definition", xv.VType.Name));
			}
			cpu.Push(true);
			return;
		}
		throw new Exception("too many elements for type definition");
	}

	/*
	 * Implementation of std::concat (on arrays of objects).
	 */
	internal void NativeConcatArrayObject(CPU cpu)
	{
		XArrayGeneric b = cpu.Pop().XObject as XArrayGeneric;
		XArrayGeneric a = cpu.Pop().XObject as XArrayGeneric;
		int alen = a.Length;
		int blen = b.Length;
		XBuffer xb = new XBufferGen(a.Length + b.Length);
		for (int i = 0; i < alen; i ++) {
			xb.Set(i, a[i]);
		}
		for (int i = 0; i < blen; i ++) {
			xb.Set(alen + i, b[i]);
		}
		XArrayGeneric c = new XArrayGeneric(XType.ARRAY_OBJECT);
		c.Init(xb, 0, alen + blen);
		cpu.Push(new XValue(c));
	}

	/*
	 * Implementation of std::start-struct.
	 */
	internal void NativeStartStruct(CPU cpu)
	{
		string name = cpu.Pop().String;
		XType xts = XType.Lookup(name);
		xts.Open();
		if (currentStruct != null) {
			savedStructs.Add(currentStruct);
		}
		currentStruct = xts;
	}

	/*
	 * Implementation of std::end-struct.
	 */
	internal void NativeEndStruct(CPU cpu)
	{
		bool toExport = cpu.Pop().Bool;
		if (currentStruct == null) {
			throw new Exception("no current struct");
		}
		if (toExport) {
			AddExportQualified(currentStruct.Name);
		}
		int n = savedStructs.Count;
		if (n > 0) {
			currentStruct = savedStructs[n - 1];
			savedStructs.RemoveAt(n - 1);
		} else {
			currentStruct = null;
		}
	}

	/*
	 * Implementation of std::add-struct-extend.
	 */
	internal void NativeAddStructExtend(CPU cpu)
	{
		XType type = cpu.Pop().XTypeInstance;
		if (currentStruct == null) {
			throw new Exception("no current struct");
		}
		currentStruct.AddExtension(type);
	}

	/*
	 * Implementation of std::add-struct-element.
	 */
	internal void NativeAddStructElement(CPU cpu)
	{
		XArrayGeneric a = cpu.Pop().XObject as XArrayGeneric;
		bool embed = cpu.Pop().Bool;
		string name = cpu.Pop().String;
		if (currentStruct == null) {
			throw new Exception("no current struct");
		}
		if (a.Length == 1) {
			XType xt = a[0].XTypeInstance;
			if (embed) {
				currentStruct.AddEmbed(name, xt);
			} else {
				currentStruct.AddField(name, xt);
			}
		} else if (a.Length == 2) {
			int size = a[0].Int;
			XType xt = a[1].XTypeInstance;
			if (embed) {
				currentStruct.AddArrayEmbed(name, size, xt);
			} else {
				currentStruct.AddArrayField(name, size, xt);
			}
		} else {
			throw new Exception("invalid type definition");
		}
	}

	/*
	 * Implementation of std::literal.
	 */
	internal void NativeLiteral(CPU cpu)
	{
		CurrentBuilder.Literal(cpu.Pop());
	}

	/*
	 * Static state: export lists.
	 */
	static IDictionary<string, List<string>> EXPORT_LISTS;

	static Interpreter() {
		EXPORT_LISTS = new SortedDictionary<string, List<string>>(
			StringComparer.Ordinal);
	}

	/*
	 * Each alias has a destination (the name the alias maps to) and
	 * a provenanceFlag (true if the alias comes from an explicit
	 * std::alias clause, false if it comes from an import list).
	 * The destination may be null in case of collisions between
	 * import lists (this is the "invalid-name" value from the T1
	 * specification).
	 */

	class Alias {

		internal string destination;
		internal bool provenanceFlag;

		internal Alias(string destination, bool provenanceFlag)
		{
			this.destination = destination;
			this.provenanceFlag = provenanceFlag;
		}
	}

	/*
	 * State for a function builder. This class implements the
	 * main step for an interpreter level. When its end is reached,
	 * a cpu.Exit() call is made; if the builder is not automatic,
	 * the function is then built and left in "BuiltFunction".
	 */

	class InterpreterStep {

		Interpreter interp;
		FunctionBuilder builder;
		string end;

		internal Function BuiltFunction {
			get;
			private set;
		}

		/*
		 * The building stops when the "end" token is reached.
		 */
		internal InterpreterStep(Interpreter interp, string end)
		{
			this.interp = interp;
			this.end = end;
			builder = interp.CurrentBuilder;
		}

		internal void Run(CPU cpu)
		{
			/*
			 * Automatic builders run if are non-empty and have
			 * no outstanding structure.
			 */
			if (builder.Auto) {
				Function f = builder.BuildAuto();
				if (f != null) {
					f.Run(cpu);
					return;
				}
			}

			/*
			 * Get next token. EOF is null, and the top-level
			 * interpreter has end == null.
			 */
			string t = interp.NextTokenOrEOF();
			if (t == end) {
				if (builder.Auto) {
					if (!builder.IsEmpty) {
						throw new Exception("interpreter ending with unfinished business");
					}
				} else {
					BuiltFunction = builder.Build();
				}
				interp.PopBuilder();
				cpu.Exit();
				return;
			} else if (t == null) {
				throw new Exception("unexpected end of file");
			}

			/*
			 * If the token is a literal constant, add it.
			 */
			XValue cv = Interpreter.ParseConst(t);
			if (!cv.IsUninitialized) {
				builder.Literal(cv);
				return;
			}

			/*
			 * The token may match one of the accessors for
			 * local variables / instances.
			 */
			if (builder.DoLocal(t)) {
				return;
			}

			/*
			 * Token is a name. Complete it if needed, then
			 * look it up as an immediate function. If there
			 * is an immediate function, run it; otherwise,
			 * add a call.
			 */
			t = interp.CompleteName(t);
			Function fi = Function.LookupImmediate(t);
			if (fi != null) {
				fi.Run(cpu);
			} else {
				builder.Call(t);
			}
		}
	}
}
