using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/*
 * Functions in this static class keep track of values to emit as
 * statically allocated constant instances.
 *
 * All instances are recorded here, then the output is produced in a
 * single call. Whenever an instance is record, a symbolic reference
 * (as an "int" value) is returned; that reference can be used later
 * on (when generating code) to point to the values.
 */

class CCValues {

	static CCValues()
	{
	}

	/*
	 * Layout structures are for object types. We keep track of the
	 * layouts to produce in a set (to check for duplicates) and in
	 * an ordered list. The list takes care to include embedded
	 * types before the types that embed them, so that definition
	 * order in the resulting C file is correct.
	 */
	static SortedSet<XType> layouts = new SortedSet<XType>();
	static List<XType> orderedLayouts = new List<XType>();

	/*
	 * Add a type for which corresponding layout structure(s) must
	 * be emitted.
	 */
	internal static void AddTypeLayout(XType xt)
	{
		/*
		 * All arrays share the same structure. If an array value
		 * is to be generated, it is up to the caller to call
		 * AddTypeLayout() with the type of embedded elements.
		 */
		if (xt.IsArray) {
			return;
		}

		/*
		 * No layout structure for restricted types and for
		 * std::int.
		 */
		if (xt.IsRestricted || xt == XType.INT) {
			return;
		}

		/*
		 * std::type has special treatment.
		 */
		if (xt == XType.XTYPE) {
			return;
		}

		/*
		 * We now have a normal structure type. We must add
		 * layouts for all embedded elements, and do it
		 * _before_ adding this type.
		 */
		if (layouts.Contains(xt)) {
			return;
		}
		foreach (XType xt2 in xt.GetEmbeddedTypes()) {
			AddTypeLayout(xt2);
		}
		layouts.Add(xt);
		orderedLayouts.Add(xt);
	}

	/*
	 * Add the provided value, and get the index to use to reference
	 * that value from generated code. If the value is uninitialized,
	 * restricted, or virtual (i.e. std::int), then no extra bytes
	 * will appear in the output, and this method returns -1.
	 */
	internal static int AddValue(XValue xv)
	{
		EmitInstance ei = AddValueInner(xv);
		if (ei == null) {
			return -1;
		}
		int r = valueRefs.Count;
		valueRefs.Add(ei);
		return r;
	}

	static EmitInstance AddValueInner(XValue xv)
	{
		if (xv.IsUninitialized) {
			return null;
		}
		XType vt = xv.VType;
		if (vt.IsRestricted || vt == XType.INT) {
			return null;
		}
		return AddInstance(xv.XObject);
	}

	static List<EmitInstance> valueRefs = new List<EmitInstance>();
	static SortedDictionary<XSerial, EmitInstance> toEmit =
		new SortedDictionary<XSerial, EmitInstance>();

	static EmitInstance AddInstance(XObject xo)
	{
		return AddInstance(xo, null, 0);
	}

	static EmitInstance AddInstance(XObject xo,
		EmitInstance container, int index)
	{
		EmitInstance ei;
		if (toEmit.TryGetValue(xo, out ei)) {
			/*
			 * We already have an EmitInstance for this object.
			 *
			 * If container is null, then we can simply keep
			 * the existing instance, and we do not have to
			 * propagate things any further: the object is
			 * already handled.
			 *
			 * If container is not null, but the existing
			 * instance is top-level, then we must convert it
			 * to an EmitInstanceEmbedded; all its embedded
			 * values automatically follow.
			 *
			 * Since embedding is a tree, there should be no
			 * case where container is not null AND the
			 * existing EmitInstance is also of embedded type.
			 */
			if (container != null) {
				if (!(ei is EmitInstanceTopLevel)) {
					throw new Exception("internal error: instance already embedded, or not embeddable");
				}
				ei = new EmitInstanceEmbedded(xo,
					container, index);
				toEmit[xo] = ei;
			}
			return ei;
		}

		/*
		 * We have a new instance. We create the object, then
		 * recursively walk over its fields and embedded values.
		 */
		if (xo is XObjectGen) {
			if (container != null) {
				ei = new EmitInstanceEmbedded(xo,
					container, index);
			} else {
				ei = new EmitInstanceTopLevel(xo);
			}
			XObjectGen xog = (XObjectGen)xo;
			int n;
			n = xog.fields.Length;
			for (int i = 0; i < n; i ++) {
				AddValueInner(xog.fields[i]);
			}
			n = xog.embeds.Length;
			for (int i = 0; i < n; i ++) {
				AddInstance(xog.embeds[i], ei, i);
			}
		} else if (xo is XArrayGeneric) {
			if (container != null) {
				ei = new EmitInstanceEmbedded(xo,
					container, index);
			} else {
				ei = new EmitInstanceTopLevel(xo);
			}
			XArrayGeneric xag = (XArrayGeneric)xo;
			AddBuffer(xag.dataBuf,
				xag.ObjectType.GetArrayElementType(),
				xag.ObjectType.arrayElementEmbedded,
				xag.dataOff, xag.dataLen);
		/* obsolete
		} else if (xo is XString) {
			throw new Exception("NYI");
		*/
		} else if (xo is XType) {
			ei = new EmitXType((XType)xo);
		} else {
			throw new Exception(string.Format("unsupported object type for serialization: {0}", xo.ObjectType.Name));
		}
		toEmit[xo] = ei;
		return ei;
	}

	static EmitInstanceBuffer AddBuffer(XBuffer xb,
		XType et, bool embed, int off, int len)
	{
		EmitInstanceBuffer eib;
		EmitInstance ei;
		if (toEmit.TryGetValue(xb, out ei)) {
			eib = ei as EmitInstanceBuffer;
			if (eib == null) {
				throw new Exception(string.Format("buffer not associated with an appropriate EmitInstanceBuffer"));
			}
		} else {
			eib = new EmitInstanceBuffer(xb, et, embed);
			toEmit[xb] = eib;
		}
		eib.AddRange(off, len);

		/*
		 * All values in the referenced range must be added.
		 */
		if (embed) {
			for (int i = 0; i < len; i ++) {
				int j = off + i;
				AddInstance(xb.Get(j).XObject, eib, j);
			}
		} else {
			for (int i = 0; i < len; i ++) {
				int j = off + i;
				AddValueInner(xb.Get(j));
			}
		}
		return eib;
	}

	internal static void PrintAll(TextWriter tw)
	{
		GatherLayouts();
		PrintLayouts(tw);
		PrintValues(tw);
		PrintIntGuard(tw);
	}

	static void GatherLayouts()
	{
		foreach (EmitInstance ei in toEmit.Values) {
			ei.GatherLayouts();
		}
	}

	static void PrintLayouts(TextWriter tw)
	{
		foreach (XType xt in orderedLayouts) {
			xt.PrintLayout(tw);
		}
	}

	static void PrintValues(TextWriter tw)
	{
		/*
		 * Pre-declare all top-level values, so that 
		 * cross-references work.
		 */
		tw.WriteLine();
		foreach (EmitInstance ei in toEmit.Values) {
			ei.PrintDeclare(tw);
		}

		/*
		 * Print the values.
		 */
		foreach (EmitInstance ei in toEmit.Values) {
			ei.PrintValue(tw);
		}
	}

	static int minInt = 0;
	static int maxInt = 0;

	/*
	 * Get the minimum number of bits needed to represent the provided
	 * signed value (that length includes the sign bit, so the smallest
	 * returned value is 1, when x == 0 or -1, and may range up to 32).
	 */
	static int BitLength(int x)
	{
		uint ux = (uint)x;
		uint s = ux >> 31;
		for (int i = 30; i >= 0; i --) {
			if (((ux >> i) & 1U) != s) {
				return i + 2;
			}
		}
		return 1;
	}

	static void PrintIntGuard(TextWriter tw)
	{
		/*
		 * We output a guard construct that will block compilation
		 * if any of our std::int values exceeds that which can
		 * fit in the target system.
		 *
		 * TODO: add a compile-time warning as well.
		 */
		int s = Math.Max(BitLength(minInt), BitLength(maxInt));
		if (s <= 15) {
			return;
		}
		int s1 = s >> 1;
		int s2 = s - s1;
		tw.WriteLine();
		tw.WriteLine("#if ((UINTPTR_MAX >> {0}) >> {1}) < 1", s1, s2);
		tw.WriteLine("#error Constant std::int value exceeds supported range on this system.");
		tw.WriteLine("#endif");
	}

	/*
	 * Print the provided value (reference). Indentation has already
	 * been applied. Separators from previous values have already been
	 * printed. This function should not add a terminating newline.
	 */
	static internal void PrintRef(TextWriter tw, XValue xv)
	{
		if (xv.IsUninitialized) {
			tw.Write("0");
			return;
		}
		XType xt = xv.VType;
		if (xt.IsRestricted) {
			if (xt == XType.BOOL) {
				tw.Write("{0}", (bool)xv ? "1" : "0");
			} else if (xt == XType.U8) {
				tw.Write("{0}", (byte)xv);
			} else if (xt == XType.U16) {
				tw.Write("{0}", (ushort)xv);
			} else if (xt == XType.U32) {
				tw.Write("{0}", (uint)xv);
			} else if (xt == XType.U64) {
				tw.Write("{0}U", (ulong)xv);
			} else if (xt == XType.I8) {
				tw.Write("{0}", (sbyte)xv);
			} else if (xt == XType.I16) {
				tw.Write("{0}", (short)xv);
			} else if (xt == XType.I32) {
				tw.Write("{0}", (int)xv);
			} else if (xt == XType.I64) {
				long m = (long)xv;
				if (m == -9223372036854775808) {
					tw.Write("-9223372036854775807 - 1");
				} else {
					tw.Write("{0}", m);
				}
			} else {
				throw new Exception(string.Format("unknown restricted type: {0}", xt.Name));
			}
		} else if (xt == XType.INT) {
			int z = xv.Int;
			minInt = Math.Min(minInt, z);
			maxInt = Math.Max(maxInt, z);
			tw.Write("(void *)(((uintptr_t){0} << 1) + 1)", z);
		/* obsolete
		} else if (xt == XType.STRING) {
			throw new Exception("NYI");
		*/
		} else if (xt == XType.XTYPE) {
			throw new Exception("NYI");
		} else {
			XObject xo = xv.XObject;
			EmitInstance ei = toEmit[xo];
			ei.PrintRef(tw);
		}
	}

	internal static void PrintObjectGen(
		TextWriter tw, int indent, XObjectGen xog)
	{
		XType xt = xog.ObjectType;
		tw.WriteLine("{");
		Compiler.Indent(tw, indent + 1);
		tw.Write("(void *)&t1t_{0}", Compiler.Encode(xt.Name));
		xt.PrintContents(tw, indent + 1, xog);
		tw.WriteLine();
		Compiler.Indent(tw, indent);
		tw.Write("}");
	}

	internal static void PrintArray(
		TextWriter tw, int indent, XArrayGeneric xag)
	{
		XType xt = xag.ObjectType;
		tw.WriteLine("{");
		Compiler.Indent(tw, indent + 1);
		tw.WriteLine("(void *)&t1t_{0},", Compiler.Encode(xt.Name));
		XBuffer xb = xag.dataBuf;
		if (xb == null) {
			/*
			 * Uninitialized array.
			 */
			Compiler.Indent(tw, indent + 1);
			tw.WriteLine("0, 0, 0");
		} else {
			EmitInstance ei;
			if (!toEmit.TryGetValue(xb, out ei)) {
				throw new Exception("internal error: buffer is not scheduled for emission");
			}
			EmitInstanceBuffer eib = ei as EmitInstanceBuffer;
			if (eib == null) {
				throw new Exception("buffer not associated with EmitInstanceBuffer");
			}
			int range;
			int off;
			eib.FindRange(xag.dataOff, out range, out off);
			Compiler.Indent(tw, indent + 1);
			tw.WriteLine("(void *)&t1b_{0}_{1},",
				xb.Serial, range);
			Compiler.Indent(tw, indent + 1);
			tw.WriteLine("{0},", off);
			Compiler.Indent(tw, indent + 1);
			tw.WriteLine("{0}", xag.dataLen);
		}
		Compiler.Indent(tw, indent);
		tw.Write("}");
	}

	abstract class EmitInstance {

		internal abstract XType InstanceType {
			get;
		}

		internal abstract void GatherLayouts();

		internal abstract void PrintDeclare(TextWriter tw);

		internal abstract void PrintValue(TextWriter tw);

		/*
		 * Print an expression that points to that instance.
		 */
		internal abstract void PrintRef(TextWriter tw);
	}

	/*
	 * Tracking class for a top-level instance (XObject).
	 */
	class EmitInstanceTopLevel : EmitInstance {

		XObject xo;

		internal EmitInstanceTopLevel(XObject xo)
		{
			this.xo = xo;
		}

		internal override XType InstanceType {
			get {
				return xo.ObjectType;
			}
		}

		internal override void GatherLayouts()
		{
			if (xo is XObjectGen) {
				AddTypeLayout(xo.ObjectType);
			} else if (xo is XArrayGeneric) {
				return;
			} else {
				throw new Exception(string.Format("internal error: EmitInstanceTopLevel on {0}", xo.GetType()));
			}
		}

		internal override void PrintDeclare(TextWriter tw)
		{
			string tname = Compiler.Encode(xo.ObjectType.Name);
			string name = string.Format("t1v_{0}_{1}",
				xo.Serial, tname);
			if (xo is XObjectGen) {
				tw.WriteLine("static const struct t1s_{0} {1};",
					tname, name);
			} else if (xo is XArrayGeneric) {
				tw.WriteLine("static const t1x_array {0};",
					name);
			} else {
				throw new Exception(string.Format("internal error: EmitInstanceTopLevel on {0}", xo.GetType()));
			}
		}

		internal override void PrintValue(TextWriter tw)
		{
			tw.WriteLine();
			string tname = Compiler.Encode(xo.ObjectType.Name);
			string name = string.Format("t1v_{0}_{1}",
				xo.Serial, tname);
			if (xo is XObjectGen) {
				XObjectGen xog = (XObjectGen)xo;
				tw.Write("static const struct t1s_{0} {1} = ",
					tname, name);
				PrintObjectGen(tw, 0, xog);
			} else if (xo is XArrayGeneric) {
				XArrayGeneric xag = (XArrayGeneric)xo;
				tw.WriteLine("static const t1x_array {0} = ",
					name);
				PrintArray(tw, 0, xag);
			} else {
				throw new Exception(string.Format("internal error: EmitInstanceTopLevel on {0}", xo.GetType()));
			}
			tw.WriteLine(";");
		}

		internal override void PrintRef(TextWriter tw)
		{
			tw.Write("(void *)&t1v_{0}_{1}", xo.Serial,
				Compiler.Encode(xo.ObjectType.Name));
		}
	}

	/*
	 * Tracking class for an instance (XObject) which is embedded in
	 * another instance which is to be emitted.
	 */
	class EmitInstanceEmbedded : EmitInstance {

		XObject xo;
		EmitInstance container;
		int index;

		internal EmitInstanceEmbedded(XObject xo,
			EmitInstance container, int index)
		{
			this.xo = xo;
			this.container = container;
			this.index = index;
		}

		internal override XType InstanceType {
			get {
				return xo.ObjectType;
			}
		}

		internal override void GatherLayouts()
		{
			if (xo is XObjectGen) {
				AddTypeLayout(xo.ObjectType);
			} else if (xo is XArrayGeneric) {
				return;
			} else {
				throw new Exception(string.Format("internal error: EmitInstanceEmbedded on {0}", xo.GetType()));
			}
		}

		internal override void PrintDeclare(TextWriter tw)
		{
			/*
			 * No value for embedded elements.
			 */
		}

		internal override void PrintValue(TextWriter tw)
		{
			/*
			 * No value for embedded elements.
			 */
		}

		internal override void PrintRef(TextWriter tw)
		{
			container.PrintRef(tw);
			tw.Write("{0}", container.InstanceType
				.GetEmbeddedSelector(index));
		}
	}

	/*
	 * Tracking class for a buffer. Buffers are not T1 objects and
	 * cannot be embedded. This class keeps tracks of which ranges
	 * in the buffer are actually used.
	 */
	class EmitInstanceBuffer : EmitInstance {

		XBuffer buf;
		XType et;
		bool embed;

		/*
		 * ranges contains an ordered list of disjoint ranges.
		 * No two ranges in this list overlap or are adjacent.
		 */
		List<IndexRange> ranges;

		internal EmitInstanceBuffer(XBuffer buf, XType et, bool embed)
		{
			this.buf = buf;
			this.et = et;
			this.embed = embed;
			this.ranges = new List<IndexRange>();
		}

		internal override XType InstanceType {
			get {
				throw new Exception("XBuffer does not have a type");
			}
		}

		internal void AddRange(int off, int len)
		{
			/*
			 * Empty ranges have no effect.
			 */
			if (len == 0) {
				return;
			}

			/*
			 * Skip ranges that end before the new range.
			 */
			int end = off + len;
			int n = ranges.Count;
			int p = 0;
			while (p < n && ranges[p].End < off) {
				p ++;
			}

			/*
			 * If we skipped them all, then the new range
			 * is simply injected at the end.
			 */
			if (p >= n) {
				ranges.Add(new IndexRange(off, len));
				return;
			}

			/*
			 * If the new range ends before the found range
			 * starts, then the new range is simply inserted
			 * there.
			 */
			if (ranges[p].Start > end) {
				ranges.Insert(p, new IndexRange(off, len));
				return;
			}

			/*
			 * Now we know that ranges[p] overlaps with the
			 * new range. We adjust the start of the new range
			 * to account for the merging with ranges[p].
			 */
			if (ranges[p].Start < off) {
				off = ranges[p].Start;
				len = end - off;
			}

			/*
			 * Find the next range that starts beyond the end
			 * of the new range (there may be none).
			 */
			int q = p + 1;
			while (q < n && ranges[q].Start <= end) {
				q ++;
			}

			/*
			 * ranges[q-1] is the last range that gets merged
			 * into the new range.
			 */
			if (ranges[q - 1].End > end) {
				end = ranges[q - 1].End;
				len = end - off;
			}

			/*
			 * Remove all ranges that were merged into the
			 * new range (ranges p to q-1, inclusive) and
			 * insert the new range instead.
			 */
			ranges.RemoveRange(p, q - p);
			ranges.Insert(p, new IndexRange(off, len));
		}

		/*
		 * For offset aoff (counted from the start of the buffer),
		 * find the relevant range and offset in that range.
		 */
		internal void FindRange(int aoff, out int range, out int off)
		{
			int n = ranges.Count;
			for (int i = 0; i < n; i ++) {
				IndexRange ir = ranges[i];
				if (aoff >= ir.Start && aoff < ir.End) {
					range = i;
					off = aoff - ir.Start;
					return;
				}
			}
			throw new Exception(string.Format("no range covers absolute offset {0}", aoff));
		}

		internal override void GatherLayouts()
		{
			/*
			 * If we have values to emit, then they have all
			 * been registered, and are responsible for adding
			 * their own layout. For an array of embedded
			 * objects, all these embedded objects have the
			 * exact right type, i.e. its layout will be
			 * available. For an array of references, the
			 * emitted buffer will have a generic type.
			 *
			 * If there are no values to emit, then the buffer
			 * won't be produced at all.
			 */
		}

		string GetEltCType()
		{
			if (embed) {
				if (et.IsArray) {
					return "t1x_array ";
				} else {
					return string.Format(
						"struct t1s_{0} ",
						Compiler.Encode(et.Name));
				}
			} else {
				if (et.IsRestricted) {
					return Compiler.RestrictedCType(et)
						+ " ";
				} else {
					return "void *";
				}
			}
		}

		internal override void PrintDeclare(TextWriter tw)
		{
			string eCType = GetEltCType();
			int n = ranges.Count;
			for (int i = 0; i < n; i ++) {
				IndexRange ir = ranges[i];
				tw.WriteLine("static const {0}t1b_{1}_{2}[];",
					eCType, buf.Serial, i);
			}
		}

		void PrintValueElementEmbed(TextWriter tw, XObject xo)
		{
			if (xo is XObjectGen) {
				PrintObjectGen(tw, 1, (XObjectGen)xo);
			} else if (xo is XArrayGeneric) {
				PrintArray(tw, 1, (XArrayGeneric)xo);
			} else {
				throw new Exception(string.Format("unsupported embedded type: {0}", xo.GetType()));
			}
		}

		internal override void PrintValue(TextWriter tw)
		{
			string eCType = GetEltCType();
			int n = ranges.Count;
			for (int i = 0; i < n; i ++) {
				IndexRange ir = ranges[i];
				tw.WriteLine();
				tw.Write(
					"static const {0}t1b_{1}_{2}[] = {{",
					eCType, buf.Serial, i);
				for (int j = ir.Start; j < ir.End; j ++) {
					if (j == ir.Start) {
						tw.WriteLine();
					} else {
						tw.WriteLine(",");
					}
					Compiler.Indent(tw, 1);
					XValue xv = buf.Get(j);
					if (embed) {
						XObject xo = xv.XObject;
						PrintValueElementEmbed(tw, xo);
					} else {
						CCValues.PrintRef(tw, xv);
					}
				}
				tw.WriteLine();
				tw.WriteLine("};");
			}
		}

		internal override void PrintRef(TextWriter tw)
		{
			throw new Exception("PrintRef() called on an EmitInstanceBuffer");
		}
	}

	struct IndexRange {

		int off;
		int len;

		internal int Start {
			get {
				return off;
			}
		}

		internal int End {
			get {
				return off + len;
			}
		}

		internal IndexRange(int off, int len)
		{
			this.off = off;
			this.len = len;
		}
	}

	/*
	 * Class for std::type instances. These are only placeholders
	 * that are used for dynamic allocation and exploration with
	 * a GC.
	 */
	class EmitXType : EmitInstance {

		XType xType;

		internal EmitXType(XType xType)
		{
			this.xType = xType;
		}

		internal override XType InstanceType {
			get {
				return XType.XTYPE;
			}
		}

		internal override void GatherLayouts()
		{
			/*
			 * Nothing to do for std::type layout.
			 */
		}

		internal override void PrintDeclare(TextWriter tw)
		{
			/*
			 * FIXME
			 */
		}

		internal override void PrintValue(TextWriter tw)
		{
			/*
			 * FIXME
			 */
		}

		internal override void PrintRef(TextWriter tw)
		{
			/*
			 * FIXME
			 */
		}
	}
}
