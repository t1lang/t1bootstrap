using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

class XType : XObject, IComparable<XType> {

	/*
	 * Type name.
	 */
	internal string Name {
		get; private set;
	}

	/*
	 * Basic types have a special representation in XValue.
	 */
	internal bool IsBasic {
		get; private set;
	}

	/*
	 * Restricted types have a dedicated representation in compiled
	 * output, which cannot hold arbitrary values. These are
	 * booleans and modular integers.
	 */
	internal bool IsRestricted {
		get; private set;
	}

	/*
	 * If true, then a slot containing values of this type may also
	 * have uninitialized state in compiled output.
	 */
	internal bool MayBeUninitialized {
		get {
			return !IsRestricted;
		}
	}

	/*
	 * If true, then this type can be sub-typed.
	 */
	internal bool AllowsSubTypes {
		get; private set;
	}

	/*
	 * Direct super-types of this type, indexed by name.
	 */
	IDictionary<string, XType> directSuperTypes;

	/*
	 * State:
	 *
	 *  - NAMED: the type name has been encountered, but was not
	 *    explicitly opened. It cannot be automatically closed (this
	 *    helps detect typographic errors).
	 *
	 *  - OPEN: the type has been explicitly opened; new elements
	 *    can be added to it.
	 *
	 *  - FULL: the type has been opened, and is not closed yet, but
	 *    new elements cannot be added to it.
	 *
	 *  - CLOSING: the type is being closed right now.
	 *
	 *  - CLOSED: type is closed.
	 *
	 * "CLOSING" is a transient state similar to "CLOSED", and used for
	 * detection of cyclic embedding.
	 */
	int state;

	const int NAMED = 0;
	const int OPEN = 1;
	const int FULL = 2;
	const int CLOSING = 3;
	const int CLOSED = 4;

	/*
	 * Object contents are referenced twice:
	 *  - In declaration order (for native in-memory ordering).
	 *  - By element name (to detect collisions).
	 */
	List<XTypeElt> orderedContents;
	IDictionary<string, XTypeElt> contents;

	/*
	 * Total number of fields.
	 */
	internal int numFields;

	/*
	 * Total number of embedded elements.
	 */
	internal int numEmbeds;

	/*
	 * Mapping of field offset to coalesced index.
	 */
	internal int[] fieldInverseMapping;
	int numFieldsCoalesced;
	List<int> fieldMapping;

	/*
	 * Paths for extensions: for each type T that this type extends
	 * directly or indirectly, the map contains the path (sequence
	 * of offsets in embeds[] fields) that leads to the embedded
	 * instance of T. If the path has length 0, then this means that
	 * T is extended several times, therefore ambiguous.
	 */
	internal IDictionary<string, int[]> extensionPath;

	/*
	 * Array of one element, being the type itself.
	 */
	XType[] mono;

	/*
	 * The instance creation function is provided as a delegate, so
	 * that special types can have a dedicated function.
	 */
	internal delegate XObject InstanceCreator();
	InstanceCreator instanceCreator;

	/*
	 * The type of elements, for array types; null for non-array
	 * types. Also the "embedding" flag.
	 */
	internal XType arrayElementType;
	internal bool arrayElementEmbedded;

	SortedDictionary<GAlign, ulong> layout;

	internal bool IsArray {
		get {
			return arrayElementType != null;
		}
	}

	XType(string name)
		: base(null)
	{
		this.Name = name;
		ALL[name] = this;
		IsBasic = false;
		IsRestricted = false;
		AllowsSubTypes = true;
		directSuperTypes = new SortedDictionary<string, XType>(
			StringComparer.Ordinal);
		state = NAMED;
		orderedContents = new List<XTypeElt>();
		contents = new SortedDictionary<string, XTypeElt>(
			StringComparer.Ordinal);
		numFields = 0;
		numEmbeds = 0;
		fieldMapping = new List<int>();
		fieldInverseMapping = null;
		extensionPath = new SortedDictionary<string, int[]>(
			StringComparer.Ordinal);
		mono = new XType[] { this };
		instanceCreator = InstanceCreatorGeneric;
		arrayElementType = null;
		arrayElementEmbedded = false;
		layout = new SortedDictionary<GAlign, ulong>();

		Function.Register(name, ZERO, new NativeType(this));
	}

	/*
	 * Find a type by name. If the type is not found, a new type
	 * is created with status NAMED.
	 */
	internal static XType Lookup(string name)
	{
		/*
		 * std::string is an alias for (std::u8 std::array).
		 * TODO: design and implement a more generic alias system
		 * for types.
		 */
		if (name == "std::string") {
			return ARRAY_U8;
		}

		XType xt;
		if (!ALL.TryGetValue(name, out xt)) {
			xt = new XType(name);
			ALL[name] = xt;
		}
		return xt;
	}

	/*
	 * Find an array type, for a given element type. If the array type
	 * is not found, it is automatically created.
	 */
	internal static XType LookupArray(XType elementType, bool embed)
	{
		string name = string.Format("({0} {1})",
			elementType.Name,
			embed ? "std::array&" : "std::array");
		XType xt;
		if (!ALL.TryGetValue(name, out xt)) {
			if (embed && !elementType.IsEmbeddable) {
				throw new Exception(string.Format("type {0} is not embeddable", elementType.Name));
			}
			xt = new XType(name);
			xt.arrayElementType = elementType;
			xt.arrayElementEmbedded = embed;
			xt.CloseArray();
			ALL[name] = xt;

			XType[] tArray = new XType[] { xt };
			XType[] tIntArray = new XType[] { INT, xt };
			XType[] tIntIntArray = new XType[] { INT, INT, xt };
			XType[] tIntIntArrayArray = new XType[] {
				INT, INT, xt, xt
			};
			XType[] tEltIntArray = new XType[] {
				elementType, INT, xt
			};
			XType[] tArrayArray = new XType[] { xt, xt };

			Function.Register("std::sub", tIntIntArrayArray,
				new NativeArrayAccessorSub(xt));
			Function.Register("std::subself", tIntIntArray,
				new NativeArrayAccessorSubSelf(xt));
			Function.Register("std::init?", tArray,
				new NativeArrayAccessorIsInit(xt));
			Function.Register("std::length", tArray,
				new NativeArrayAccessorLength(xt));
			if (embed) {
				Function.Register("std::make", tIntArray,
					new NativeArrayAccessorMakeEmbed(xt));
				Function.Register("std::@&", tIntArray,
					new NativeArrayAccessorRef(xt));
			} else {
				Function.Register("std::make", tIntArray,
					new NativeArrayAccessorMakeRef(xt));
				Function.Register("std::@", tIntArray,
					new NativeArrayAccessorGet(xt));
				Function.Register("std::->@", tEltIntArray,
					new NativeArrayAccessorPut(xt));
				Function.Register("std::Z->@", tIntArray,
					new NativeArrayAccessorPut(xt));
				Function.Register("std::@?", tIntArray,
					new NativeArrayAccessorIsEltInit(xt));

				/*
				 * Arrays of references implement '=' by
				 * running that function on all elements.
				 * This is implemented with an interpreted
				 * function since it may call interpreted
				 * functions.
				 *
				 * : = (...)
				 *     ->{ a1 a2 }
				 *     a1 length ->{ len }
				 *     len a2 length = ifnot false ret then
				 *     0 ->{ i }
				 *     begin i len < while
				 *         i a1 @ i a2 @ = ifnot false ret then
				 *         i ++ ->i
				 *     repeat
				 *     true ;
				 */
				FunctionBuilder fb = new FunctionBuilder(
					"std::=");
				/* { a1 a2 len i } */
				fb.DefLocalField("a1", null);
				fb.DefLocalField("a2", null);
				fb.DefLocalField("len", null);
				fb.DefLocalField("i", null);
				/* ->{ a1 a2 } */
				fb.DoLocal("->a2");
				fb.DoLocal("->a1");
				/* a1 length ->len */
				fb.DoLocal("a1");
				fb.Call("std::length");
				fb.DoLocal("->len");
				/* len a2 length = ifnot false ret then */
				fb.DoLocal("len");
				fb.DoLocal("a2");
				fb.Call("std::length");
				fb.Call("std::=");
				fb.AheadIf();
				fb.Literal(false);
				fb.Ret();
				fb.Then();
				/* 0 ->{ i } */
				fb.Literal(XValue.MakeInt(0));
				fb.DoLocal("->i");
				/* begin i len < while */
				fb.Begin();
				fb.DoLocal("i");
				fb.DoLocal("len");
				fb.Call("std::<");
				fb.AheadIfNot();
				fb.CSRoll(1);
				/*     i a1 @ i a2 @ = ifnot false ret then */
				fb.DoLocal("i");
				fb.DoLocal("a1");
				fb.Call("std::@");
				fb.DoLocal("i");
				fb.DoLocal("a2");
				fb.Call("std::@");
				fb.Call("std::=");
				fb.AheadIf();
				fb.Literal(false);
				fb.Ret();
				fb.Then();
				/*     i ++ ->i */
				fb.DoLocal("i");
				fb.Call("std::++");
				fb.DoLocal("->i");
				/* repeat */
				fb.Again();
				fb.Then();
				/* true ; */
				fb.Literal(true);
				fb.Ret();

				Function f = fb.Build();
				Function.Register("std::=", tArrayArray, f);

				/*
				 * The std::<> function is implemented by
				 * calling std::=, then negating.
				 */
				fb = new FunctionBuilder(
					"std::<>");
				fb.Call("std::=");
				fb.Call("std::not");
				fb.Ret();
				f = fb.Build();
				Function.Register("std::<>", tArrayArray, f);
			}
		} else if (xt.arrayElementType != elementType) {
			throw new Exception(string.Format("type {0} already exists and is not an array of {1}", name, elementType.Name));
		} else if (xt.arrayElementEmbedded != embed) {
			throw new Exception(string.Format("elements of type {0} are {1}embedded", name, embed ? "not " : ""));
		}
		return xt;
	}

	internal bool IsSubTypeOf(XType other)
	{
		if (other == this || other == XType.OBJECT) {
			return true;
		}
		foreach (XType xt in directSuperTypes.Values) {
			if (xt.IsSubTypeOf(other)) {
				return true;
			}
		}
		return false;
	}

	internal void AddSuperType(XType superType)
	{
		/*
		 * Don't add the super-type if it is already there,
		 * directly or indirectly.
		 */
		if (IsSubTypeOf(superType)) {
			return;
		}

		/*
		 * The super-type must allow sub-types.
		 */
		if (!superType.AllowsSubTypes) {
			throw new Exception(string.Format("type {0} does not accept {1} as sub-type", superType.Name, Name));
		}

		/*
		 * If the super-type is a sub-type of this type, then this
		 * is a cycle, which is forbidden.
		 */
		if (superType.IsSubTypeOf(this)) {
			throw new Exception(string.Format("sub-typing cycle {0} -> {1}", superType.Name, Name));
		}

		directSuperTypes[superType.Name] = superType;
	}

	internal string ToString(ulong basic)
	{
		if (!IsBasic) {
			throw new Exception(string.Format("type {0} is not basic", Name));
		}
		if (this == BOOL) {
			return (basic == 0) ? "false" : "true";
		}
		if (this == INT || this == I8 || this == I16
			|| this == I32 || this == I64)
		{
			return string.Format("{0}", (long)basic);
		} else {
			return string.Format("{0}", basic);
		}
	}

	internal void AddField(string name, XType ft)
	{
		AddElement(name, new XTypeEltField(this, name, ft));
	}

	internal void AddEmbed(string name, XType et)
	{
		if (!et.IsEmbeddable) {
			throw new Exception(string.Format("cannot embed type {0}", et.Name));
		}
		AddElement(name, new XTypeEltEmbed(this, name, et, false));
	}

	internal void AddArrayField(string name, int size, XType ft)
	{
		if (size <= 0) {
			throw new Exception("nonpositive array size");
		}
		AddElement(name, new XTypeEltArrayField(this, name, size, ft));
	}

	internal void AddArrayEmbed(string name, int size, XType et)
	{
		if (size <= 0) {
			throw new Exception("nonpositive array size");
		}
		if (!et.IsEmbeddable) {
			throw new Exception(string.Format("cannot embed type {0}", et.Name));
		}
		AddElement(name, new XTypeEltArrayEmbed(this, name, size, et));
	}

	void AddElement(string name, XTypeElt elt)
	{
		string reason;
		switch (state) {
		case NAMED:
			reason = "type is not open yet";
			break;
		case OPEN:
			if (contents.ContainsKey(name)) {
				reason = "duplicate type element name";
				break;
			}
			orderedContents.Add(elt);
			contents[name] = elt;
			return;
		case FULL:
			reason = "type is full";
			break;
		case CLOSING:
		case CLOSED:
			reason = "type is closed";
			break;
		default:
			reason = "unknown internal state: " + state;
			break;
		}
		throw new Exception(string.Format("cannot add element {0} to type {1}: {2}", name, Name, reason));
	}

	/*
	 * Get the types of field contents when cleared / initialized.
	 * This is null for most fields; however, for basic types
	 * (boolean and modular integers), that cannot be in uninitialized
	 * state, this returns the relevant type.
	 *
	 * The returned array is indexed by coalesced offset.
	 *
	 * For array types (arrays of references), an array of length 1
	 * is returned.
	 */
	internal XType[] GetFieldInitTypes()
	{
		Close();
		if (arrayElementType != null && !arrayElementEmbedded) {
			XType[] xts = new XType[1];
			if (!arrayElementType.MayBeUninitialized) {
				xts[0] = arrayElementType;
			}
			return xts;
		} else {
			XType[] xts = new XType[numFieldsCoalesced];
			foreach (XTypeElt e in orderedContents) {
				e.GetFieldInitType(xts);
			}
			return xts;
		}
	}

	/*
	 * Get types embedded by this one. Each embedded type appears
	 * only once.
	 */
	internal XType[] GetEmbeddedTypes()
	{
		Close();
		SortedSet<string> s = new SortedSet<string>(
			StringComparer.Ordinal);
		List<XType> r = new List<XType>();
		foreach (XTypeElt e in orderedContents) {
			XType xt = e.GetEmbeddedType();
			if (xt == null) {
				continue;
			}
			if (!s.Contains(xt.Name)) {
				s.Add(xt.Name);
				r.Add(xt);
			}
		}
		return r.ToArray();
	}

	internal void AddExtension(XType superType)
	{
		string name = superType.Name;
		AddSuperType(superType);
		AddElement(name,
			new XTypeEltEmbed(this, name, superType, true));
	}

	internal void Open()
	{
		if (state == NAMED) {
			state = OPEN;
		} else if (state != OPEN) {
			throw new Exception(string.Format("cannot reopen type {0}", Name));
		}
	}

	internal void Full()
	{
		if (state == OPEN) {
			state = FULL;
		} else if (state != FULL) {
			throw new Exception(string.Format("cannot mark as full type {0} which is not open", Name));
		}
	}

	internal void Close()
	{
		switch (state) {
		case OPEN:
		case FULL:
			state = CLOSING;
			foreach (XTypeElt elt in orderedContents) {
				elt.PropagateClose();
			}
			ComputeExtensionPaths();
			foreach (XTypeElt elt in orderedContents) {
				elt.MakeAccessors();
			}
			fieldInverseMapping = new int[numFields];
			numFieldsCoalesced = fieldMapping.Count;
			for (int i = 0; i < numFieldsCoalesced; i ++) {
				fieldInverseMapping[fieldMapping[i]] = i;
			}
			state = CLOSED;
			break;
		case CLOSED:
			return;
		case CLOSING:
			throw new Exception(string.Format("embedding cycle on type {0}", Name));
		default:
			throw new Exception(string.Format("impossible state for type {0}: {1}", Name, state));
		}
	}

	static int[] I0 = new int[0];

	void ComputeExtensionPaths()
	{
		foreach (XTypeElt elt in orderedContents) {
			XTypeEltEmbed xte = elt as XTypeEltEmbed;
			if (xte == null || !xte.IsExtension) {
				continue;
			}
			int[] p0;
			if (extensionPath.TryGetValue(xte.Name, out p0)) {
				extensionPath[xte.Name] = I0;
			} else {
				extensionPath[xte.Name] =
					new int[] { xte.Offset };
			}
			IDictionary<string, int[]> eep =
				xte.Embed.extensionPath;
			foreach (string name in eep.Keys) {
				int[] p2 = eep[name];
				int[] p1;
				if (extensionPath.TryGetValue(name, out p1)) {
					extensionPath[name] = I0;
				} else if (p2.Length == 0) {
					extensionPath[name] = I0;
				} else {
					p1 = new int[1 + p2.Length];
					p1[0] = xte.Offset;
					Array.Copy(p2, 0, p1, 1, p2.Length);
					extensionPath[name] = p1;
				}
			}
		}
	}

	/*
	 * Verify that this type extends the provided type in a
	 * non-ambiguous way. This is used for type analysis of accessors.
	 */
	internal void CheckExtends(XType target)
	{
		if (state != CLOSED) {
			throw new Exception(string.Format("type {0} is not closed", Name));
		}
		int[] pp;
		if (!extensionPath.TryGetValue(target.Name, out pp)) {
			throw new Exception(string.Format("accessor for type {0} cannot be used on type {1}, which does not extend it", target.Name, Name));
		}
		if (pp.Length == 0) {
			throw new Exception(string.Format("accessor for type {0} cannot be used on type {1} because extension is ambiguous", target.Name, Name));
		}
	}

	/*
	 * Given a value, and a target type, find the relevant object
	 * instance that the value extends. This is for accessors; an
	 * exception is thrown on error.
	 */
	internal static XObject FindExtended(XObject xo, XType target)
	{
		XType xt = xo.ObjectType;
		if (xt == target) {
			return xo;
		}
		int[] pp;
		if (!xt.extensionPath.TryGetValue(target.Name, out pp)) {
			throw new Exception(string.Format("accessor for an element of type {0} cannot be used on an instance of type {1}", target.Name, xt.Name));
		}
		if (pp.Length == 0) {
			throw new Exception(string.Format("extension of type {0} by type {1} is ambiguous", target.Name, xt.Name));
		}
		foreach (int p in pp) {
			XObjectGen xog = xo as XObjectGen;
			if (xog == null) {
				throw new Exception(string.Format("accessor applied to a non-generic object type {0}", xo.ObjectType.Name));
			}
			xo = xog.embeds[p];
		}
		return xo;
	}

	/*
	 * Given a value, and a target type, find the relevant object
	 * instance that the value extends. This is for field accessors:
	 * the final instance must be a generic object (XObjectGen).
	 */
	internal static XObjectGen FindExtendedGen(XObject xo, XType target)
	{
		xo = FindExtended(xo, target);
		XObjectGen xog = xo as XObjectGen;
		if (xog == null) {
			throw new Exception(string.Format("accessor applied to a non-generic object type {0}", xo.ObjectType.Name));
		}
		return xog;
	}

	internal XObject NewInstance()
	{
		return instanceCreator();
	}

	bool IsEmbeddable {
		get {
			if (IsBasic || this == OBJECT
				|| this == XTYPE /* || this == STRING */)
			{
				return false;
			}
			return true;
		}
	}

	XObject InstanceCreatorGeneric()
	{
		Close();

		/*
		 * We can create instances for types which are embeddable,
		 * and vice versa.
		 */
		if (!IsEmbeddable) {
			throw new Exception(string.Format("type {0} does not allow creating generic instances", Name));
		}
		XValue[] fields = new XValue[numFields];
		XObject[] embeds = new XObject[numEmbeds];
		XObjectGen xo = new XObjectGen(this, fields, embeds);
		foreach (XTypeElt elt in orderedContents) {
			elt.Initialize(xo);
		}
		return xo;
	}

	XObject InstanceCreatorRefuse()
	{
		throw new Exception(string.Format("instances of type {0} cannot be created", Name));
	}

	XObject InstanceCreatorArray()
	{
		return new XArrayGeneric(this);
	}

	void CloseArray()
	{
		state = CLOSED;
		instanceCreator = InstanceCreatorArray;
	}

	void LockDown()
	{
		state = CLOSED;
		instanceCreator = InstanceCreatorRefuse;
	}

	internal XType GetArrayElementType()
	{
		if (arrayElementType == null) {
			throw new Exception(string.Format("type {0} is not an array type", Name));
		}
		return arrayElementType;
	}

	internal void PrintLayout(TextWriter tw)
	{
		tw.WriteLine();
		tw.WriteLine("struct t1s_{0} {{", Compiler.Encode(Name));
		Compiler.Indent(tw, 1);
		tw.WriteLine("void *t1h_header;");
		foreach (XTypeElt e in orderedContents) {
			e.PrintLayout(tw);
		}
		tw.WriteLine("};");
	}

	internal void PrintContents(TextWriter tw, int indent, XObjectGen xog)
	{
		foreach (XTypeElt e in orderedContents) {
			tw.WriteLine(",");
			Compiler.Indent(tw, indent);
			e.PrintContents(tw, indent, xog);
		}
	}

	internal string GetEmbeddedSelector(int index)
	{
		foreach (XTypeElt e in orderedContents) {
			string s = e.GetEmbeddedSelector(index);
			if (s != null) {
				return s;
			}
		}
		throw new Exception(string.Format("no embedded element with index {0}", index));
	}

	internal int AlignAndSize(GAlign ga, out int sz)
	{
		if (IsBasic) {
			throw new Exception(string.Format("basic type {0} does not have alignment or size", Name));
		}

		ulong v;
		if (layout.TryGetValue(ga, out v)) {
			sz = (int)(v >> 32);
			return (int)v;
		}

		Close();

		/*
		 * Each object starts with a pointer-sized header.
		 */
		int addr = ga.SizeRef(OBJECT);
		int ma = ga.AlignRef(OBJECT);
		foreach (XTypeElt e in orderedContents) {
			int a, s;
			a = e.AlignAndSize(ga, out s);
			addr = (addr + a - 1) & ~(a - 1);
			addr += s;
			ma = Math.Max(ma, a);
		}
		sz = (addr + ma - 1) & ~(ma - 1);
		layout[ga] = ((ulong)sz << 32) | (ulong)(uint)ma;
		return ma;
	}

	public int CompareTo(XType other)
	{
		return StringComparer.Ordinal.Compare(Name, other.Name);
	}

	public override bool Equals(object obj)
	{
		XType xt = obj as XType;
		if (xt == null) {
			return false;
		} else {
			return Name == xt.Name;
		}
	}

	public override int GetHashCode()
	{
		return Name.GetHashCode();
	}

	internal static XType OBJECT;
	internal static XType BOOL;
	internal static XType INT;
	internal static XType U8;
	internal static XType U16;
	internal static XType U32;
	internal static XType U64;
	internal static XType I8;
	internal static XType I16;
	internal static XType I32;
	internal static XType I64;
	// internal static XType STRING;
	internal static XType XTYPE;

	internal static XType ARRAY_OBJECT;
	internal static XType ARRAY_BOOL;
	internal static XType ARRAY_U8;
	internal static XType ARRAY_U16;
	internal static XType ARRAY_U32;
	internal static XType ARRAY_U64;
	internal static XType ARRAY_I8;
	internal static XType ARRAY_I16;
	internal static XType ARRAY_I32;
	internal static XType ARRAY_I64;

	static IDictionary<string, XType> ALL;

	internal static XType[] ZERO = new XType[0];

	static XType()
	{
		ALL = new SortedDictionary<string, XType>(
			StringComparer.Ordinal);

		OBJECT = new XType("std::object");
		BOOL = new XType("std::bool");
		INT = new XType("std::int");
		U8 = new XType("std::u8");
		U16 = new XType("std::u16");
		U32 = new XType("std::u32");
		U64 = new XType("std::u64");
		I8 = new XType("std::i8");
		I16 = new XType("std::i16");
		I32 = new XType("std::i32");
		I64 = new XType("std::i64");
		// STRING = new XType("std::string");
		XTYPE = new XType("std::type");

		Interpreter.AddExportQualified("std::object");
		Interpreter.AddExportQualified("std::bool");
		Interpreter.AddExportQualified("std::int");
		Interpreter.AddExportQualified("std::u8");
		Interpreter.AddExportQualified("std::u16");
		Interpreter.AddExportQualified("std::u32");
		Interpreter.AddExportQualified("std::u64");
		Interpreter.AddExportQualified("std::i8");
		Interpreter.AddExportQualified("std::i16");
		Interpreter.AddExportQualified("std::i32");
		Interpreter.AddExportQualified("std::i64");
		Interpreter.AddExportQualified("std::string");
		Interpreter.AddExportQualified("std::type");

		BOOL.IsBasic = true;
		INT.IsBasic = true;
		U8.IsBasic = true;
		U16.IsBasic = true;
		U32.IsBasic = true;
		U64.IsBasic = true;
		I8.IsBasic = true;
		I16.IsBasic = true;
		I32.IsBasic = true;
		I64.IsBasic = true;

		BOOL.IsRestricted = true;
		U8.IsRestricted = true;
		U16.IsRestricted = true;
		U32.IsRestricted = true;
		U64.IsRestricted = true;
		I8.IsRestricted = true;
		I16.IsRestricted = true;
		I32.IsRestricted = true;
		I64.IsRestricted = true;

		BOOL.AllowsSubTypes = false;
		U8.AllowsSubTypes = false;
		U16.AllowsSubTypes = false;
		U32.AllowsSubTypes = false;
		U64.AllowsSubTypes = false;
		I8.AllowsSubTypes = false;
		I16.AllowsSubTypes = false;
		I32.AllowsSubTypes = false;
		I64.AllowsSubTypes = false;

		OBJECT.LockDown();
		BOOL.LockDown();
		INT.LockDown();
		U8.LockDown();
		U16.LockDown();
		U32.LockDown();
		U64.LockDown();
		I8.LockDown();
		I16.LockDown();
		I32.LockDown();
		I64.LockDown();
		// STRING.LockDown();
		XTYPE.LockDown();

		ARRAY_OBJECT = LookupArray(OBJECT, false);
		ARRAY_BOOL = LookupArray(BOOL, false);
		ARRAY_U8 = LookupArray(U8, false);
		ARRAY_U16 = LookupArray(U16, false);
		ARRAY_U32 = LookupArray(U32, false);
		ARRAY_U64 = LookupArray(U64, false);
		ARRAY_I8 = LookupArray(I8, false);
		ARRAY_I16 = LookupArray(I16, false);
		ARRAY_I32 = LookupArray(I32, false);
		ARRAY_I64 = LookupArray(I64, false);
	}

	abstract class XTypeElt {

		internal string Name {
			get;
			private set;
		}

		internal XType Owner {
			get;
			private set;
		}

		internal XTypeElt(XType owner, string name)
		{
			this.Owner = owner;
			this.Name = name;
		}

		internal abstract void PropagateClose();

		internal abstract void MakeAccessors();

		internal abstract void Initialize(XObjectGen obj);

		internal virtual void GetFieldInitType(XType[] xts)
		{
		}

		internal virtual XType GetEmbeddedType()
		{
			return null;
		}

		internal abstract void PrintLayout(TextWriter tw);

		internal string CName {
			get {
				return "t1e_" + Compiler.Encode(Name);
			}
		}

		internal abstract void PrintContents(
			TextWriter tw, int indent, XObjectGen xog);

		internal abstract string GetEmbeddedSelector(int index);

		SortedDictionary<GAlign, ulong> layout =
			new SortedDictionary<GAlign, ulong>();

		internal int AlignAndSize(GAlign ga, out int sz)
		{
			ulong v;
			if (layout.TryGetValue(ga, out v)) {
				sz = (int)(v >> 32);
				return (int)v;
			}
			int a = AlignAndSizeInner(ga, out sz);
			layout[ga] = ((ulong)sz << 32) | (ulong)(uint)a;
			return a;
		}

		internal abstract int AlignAndSizeInner(GAlign ga, out int sz);
	}

	class XTypeEltField : XTypeElt {

		XType ft;
		int off;

		internal XTypeEltField(XType owner, string name, XType ft)
			: base(owner, name)
		{
			this.ft = ft;
		}

		internal override void PropagateClose()
		{
			off = Owner.numFields ++;
			Owner.fieldMapping.Add(off);
		}

		internal override void MakeAccessors()
		{
			string name = Name;
			XType owner = Owner;
			string nameGet = name;
			string namePut = Names.Decorate("->", name, null);
			string nameClear = Names.Decorate("Z->", name, null);
			string nameTest = Names.Decorate(null, name, "?");
			Function.Register(nameGet, owner.mono,
				new NativeFieldGet(owner, name,
					ft, off, nameGet));
			Function.Register(namePut, new XType[] { ft, owner },
				new NativeFieldPut(owner, name,
					ft, off, namePut));
			Function.Register(nameClear, owner.mono,
				new NativeFieldClear(owner, name,
					ft, off, nameClear));
			Function.Register(nameTest, owner.mono,
				new NativeFieldTest(owner, name,
					ft, off, nameTest));
		}

		internal override void Initialize(XObjectGen obj)
		{
			obj.fields[off].Clear(ft);
		}

		internal override void GetFieldInitType(XType[] xts)
		{
			if (!ft.MayBeUninitialized) {
				xts[Owner.fieldInverseMapping[off]] = ft;
			}
		}

		internal override void PrintLayout(TextWriter tw)
		{
			Compiler.Indent(tw, 1);
			if (ft.IsRestricted) {
				tw.WriteLine("{0} {1};",
					Compiler.RestrictedCType(ft), CName);
			} else {
				tw.WriteLine("void *{0};", CName);
			}
		}

		internal override void PrintContents(
			TextWriter tw, int indent, XObjectGen xog)
		{
			CCValues.PrintRef(tw, xog.fields[off]);
		}

		internal override string GetEmbeddedSelector(int index)
		{
			return null;
		}

		internal override int AlignAndSizeInner(GAlign ga, out int sz)
		{
			sz = ga.SizeRef(ft);
			return ga.AlignRef(ft);
		}
	}

	class XTypeEltEmbed : XTypeElt {

		internal XType Embed {
			get;
			private set;
		}

		internal bool IsExtension {
			get;
			private set;
		}

		internal int Offset {
			get;
			private set;
		}

		internal XTypeEltEmbed(XType owner, string name,
			XType et, bool isExt)
			: base(owner, name)
		{
			this.Embed = et;
			this.IsExtension = isExt;
		}

		internal override void PropagateClose()
		{
			Embed.Close();
			Offset = Owner.numEmbeds ++;
		}

		internal override void MakeAccessors()
		{
			string name = Name;
			XType owner = Owner;
			string nameRef = Names.Decorate(null, name, "&");
			Function.Register(nameRef, owner.mono,
				new NativeEmbedRef(owner, name,
					Embed, Offset, nameRef));
		}

		internal override void Initialize(XObjectGen obj)
		{
			obj.embeds[Offset] = Embed.NewInstance();
		}

		internal override XType GetEmbeddedType()
		{
			return Embed;
		}

		internal override void PrintLayout(TextWriter tw)
		{
			Compiler.Indent(tw, 1);
			if (Embed.IsArray) {
				tw.WriteLine("t1x_array {0};", CName);
			} else {
				tw.WriteLine("struct t1s_{0} {1};",
					Compiler.Encode(Embed.Name),
					CName);
			}
		}

		internal override void PrintContents(
			TextWriter tw, int indent, XObjectGen xog)
		{
			XObject xo = xog.embeds[Offset];
			if (xo is XObjectGen) {
				CCValues.PrintObjectGen(tw, indent,
					(XObjectGen)xo);
			} else if (xo is XArrayGeneric) {
				CCValues.PrintArray(tw, indent,
					(XArrayGeneric)xo);
			} else {
				throw new Exception(string.Format("unsupported embedded type: {0}", xo.GetType()));
			}
		}

		internal override string GetEmbeddedSelector(int index)
		{
			if (index == Offset) {
				return string.Format(".{0}", CName);
			}
			return null;
		}

		internal override int AlignAndSizeInner(GAlign ga, out int sz)
		{
			return Embed.AlignAndSize(ga, out sz);
		}
	}

	class XTypeEltArrayField : XTypeElt {

		int size;
		XType ft;
		int off;

		internal XTypeEltArrayField(XType owner, string name,
			int size, XType ft)
			: base(owner, name)
		{
			this.size = size;
			this.ft = ft;
		}

		internal override void PropagateClose()
		{
			off = Owner.numFields;
			Owner.numFields += size;
			Owner.fieldMapping.Add(off);
		}

		internal override void MakeAccessors()
		{
			string name = Name;
			XType owner = Owner;
			XType[] tIntOwner = new XType[] { XType.INT, owner };
			string nameGet = Names.Decorate(null, name, "@");
			string namePut = Names.Decorate("->", name, "@");
			string nameClear = Names.Decorate("Z->", name, "@");
			string nameTest = Names.Decorate(null, name, "@?");
			Function.Register(nameGet, tIntOwner,
				new NativeFieldArrayGet(owner, name,
					ft, off, size, nameGet));
			Function.Register(namePut,
				new XType[] { ft, XType.INT, owner },
				new NativeFieldArrayPut(owner, name,
					ft, off, size, namePut));
			Function.Register(nameClear, tIntOwner,
				new NativeFieldArrayClear(owner, name,
					ft, off, size, nameClear));
			Function.Register(nameTest, tIntOwner,
				new NativeFieldArrayTest(owner, name,
					ft, off, size, nameTest));
			// FIXME: x*
		}

		internal override void Initialize(XObjectGen obj)
		{
			for (int i = 0; i < size; i ++) {
				obj.fields[off + i].Clear(ft);
			}
		}

		internal override void GetFieldInitType(XType[] xts)
		{
			if (!ft.MayBeUninitialized) {
				xts[Owner.fieldInverseMapping[off]] = ft;
			}
		}

		internal override void PrintLayout(TextWriter tw)
		{
			Compiler.Indent(tw, 1);
			if (ft.IsRestricted) {
				tw.WriteLine("{0} {1}[{2}];",
					Compiler.RestrictedCType(ft),
					CName, size);
			} else {
				tw.WriteLine("void *{0}[{1}];", CName, size);
			}
		}

		internal override void PrintContents(
			TextWriter tw, int indent, XObjectGen xog)
		{
			tw.WriteLine("{");
			for (int i = 0; i < size; i ++) {
				if (i != 0) {
					tw.WriteLine(",");
				}
				Compiler.Indent(tw, indent + 1);
				CCValues.PrintRef(tw, xog.fields[off + i]);
			}
			tw.WriteLine();
			Compiler.Indent(tw, indent);
			tw.Write("}");
		}

		internal override string GetEmbeddedSelector(int index)
		{
			return null;
		}

		internal override int AlignAndSizeInner(GAlign ga, out int sz)
		{
			sz = size * ga.SizeRef(ft);
			return ga.AlignRef(ft);
		}
	}

	class XTypeEltArrayEmbed : XTypeElt {

		int size;
		XType et;
		int off;

		internal XTypeEltArrayEmbed(XType owner, string name,
			int size, XType et)
			: base(owner, name)
		{
			this.size = size;
			this.et = et;
		}

		internal override void PropagateClose()
		{
			et.Close();
			off = Owner.numEmbeds;
			Owner.numEmbeds += size;
		}

		internal override void MakeAccessors()
		{
			string name = Name;
			XType owner = Owner;
			string nameRef = Names.Decorate(null, name, "@&");
			Function.Register(nameRef, owner.mono,
				new NativeEmbedArrayRef(owner, name,
					et, off, size, nameRef));
			// FIXME: x*
		}

		internal override void Initialize(XObjectGen obj)
		{
			for (int i = 0; i < size; i ++) {
				obj.embeds[off + i] = et.NewInstance();
			}
		}

		internal override XType GetEmbeddedType()
		{
			return et;
		}

		internal override void PrintLayout(TextWriter tw)
		{
			Compiler.Indent(tw, 1);
			if (et.IsArray) {
				tw.WriteLine("t1x_array {0}[{2}];",
					CName, size);
			} else {
				tw.WriteLine("struct t1s_{0} {1}[{2}];",
					Compiler.Encode(et.Name),
					CName, size);
			}
		}

		internal override void PrintContents(
			TextWriter tw, int indent, XObjectGen xog)
		{
			tw.WriteLine("{");
			for (int i = 0; i < size; i ++) {
				if (i != 0) {
					tw.WriteLine(",");
				}
				Compiler.Indent(tw, indent + 1);
				XObject xo = xog.embeds[off + i];
				if (xo is XObjectGen) {
					CCValues.PrintObjectGen(tw, indent + 1,
						(XObjectGen)xo);
				} else if (xo is XArrayGeneric) {
					CCValues.PrintArray(tw, indent + 1,
						(XArrayGeneric)xo);
				} else {
					throw new Exception(string.Format("unsupported embedded type: {0}", xo.GetType()));
				}
			}
			tw.WriteLine();
			Compiler.Indent(tw, indent);
			tw.Write("}");
		}

		internal override string GetEmbeddedSelector(int index)
		{
			if (index >= off && index < (off + size)) {
				return string.Format(".{0}[{1}]",
					CName, index - off);
			}
			return null;
		}

		internal override int AlignAndSizeInner(GAlign ga, out int sz)
		{
			int a = et.AlignAndSize(ga, out sz);
			sz *= size;
			return a;
		}
	}
}

class XTypeSet : IComparable<XTypeSet>, IEnumerable<XType> {

	internal static XTypeSet EMPTY = new XTypeSet();

	internal int Count {
		get {
			return types.Count;
		}
	}

	internal bool IsRestricted {
		get; private set;
	}

	SortedSet<XType> types;
	int cachedHashCode;

	internal XTypeSet()
	{
		types = new SortedSet<XType>();
		cachedHashCode = 0;
		IsRestricted = false;
	}

	internal XType First()
	{
		IEnumerator<XType> e = types.GetEnumerator();
		if (!e.MoveNext()) {
			throw new Exception("type set is empty, no first element");
		}
		return e.Current;
	}

	internal bool Add(XType xt)
	{
		if (types.Count == 0) {
			IsRestricted = xt.IsRestricted;
		} else if (IsRestricted) {
			XType xto = First();
			if (!xt.Equals(xto)) {
				throw new Exception(string.Format("invalid type set construction: {0} / {1}", xto, xt));
			}
			return false;
		} else if (xt.IsRestricted) {
			throw new Exception(string.Format("adding restricted type {0} to a non-empty set", xt));
		}
		return types.Add(xt);
	}

	internal bool Contains(XType xt)
	{
		return types.Contains(xt);
	}

	/*
	 * Return the intersection of two type sets. The returned instance
	 * MUST NOT be modified; it may be equal to either of the operands,
	 * or to other shared instances.
	 */
	internal static XTypeSet Intersect(XTypeSet xts1, XTypeSet xts2)
	{
		XTypeSet xts3 = null;
		if (xts1.Count > xts2.Count) {
			XTypeSet t = xts1;
			xts1 = xts2;
			xts2 = t;
		}
		if (xts1.Count == xts2.Count && xts1.Equals(xts2)) {
			return xts1;
		}
		foreach (XType xt in xts1) {
			if (xts2.Contains(xt)) {
				continue;
			}
			if (xts3 == null) {
				xts3 = new XTypeSet();
			}
			xts3.Add(xt);
		}
		if (xts3 == null) {
			xts3 = EMPTY;
		}
		return xts3;
	}

	public IEnumerator<XType> GetEnumerator()
	{
		return types.GetEnumerator();
	}

	IEnumerator GetEnumeratorNoType()
	{
		return this.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumeratorNoType();
	}

	public int CompareTo(XTypeSet xts)
	{
		IEnumerator<XType> e1 = GetEnumerator();
		IEnumerator<XType> e2 = xts.GetEnumerator();
		for (;;) {
			bool b1 = e1.MoveNext();
			bool b2 = e2.MoveNext();
			if (b1 && b2) {
				int r = e1.Current.CompareTo(e2.Current);
				if (r != 0) {
					return r;
				}
			} else if (b1) {
				return 1;
			} else if (b2) {
				return -1;
			} else {
				return 0;
			}
		}
	}

	public override bool Equals(object other)
	{
		if (object.ReferenceEquals(this, other)) {
			return true;
		}
		XTypeSet xts = other as XTypeSet;
		if (xts == null) {
			return false;
		}
		if (types.Count != xts.types.Count) {
			return false;
		}
		foreach (XType xt in xts.types) {
			if (!types.Contains(xt)) {
				return false;
			}
		}
		return true;
	}

	public override int GetHashCode()
	{
		int hc = cachedHashCode;
		if (hc == 0) {
			foreach (XType xt in types) {
				hc = (hc << 5) | (int)((uint)hc >> 27);
				hc += xt.GetHashCode();
			}
			cachedHashCode = hc;
		}
		return hc;
	}
}
