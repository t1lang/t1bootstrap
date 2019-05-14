using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

/*
 * A CCType instance represents an XType along with some extra
 * information:
 *
 *   - allocationNode: entry point of the function which allocated it,
 *     for local instances; null for static and heap-allocated instances.
 *
 *   - variant: optional integer to keep track of local instances
 *     separately. Multiple local instances within the same function
 *     invocation will have the same allocationNode, but distinct
 *     variant numbers.
 *
 *   - constant: set to true for types whose in-memory representation
 *     cannot be changed (this is for statically-allocated instances).
 *
 *   - ofType: non-null when the CCType is for the std::type instance
 *     of the specific type 'ofType'.
 *
 * A special CCType is the "uninitialized" type; it is used for instance
 * fields and local variables. Its xType and allocationNode fields are
 * both null, and its variant 0.
 *
 * CCType instances have a defined order. The uninitialized type is the
 * minimal value in that order.
 */

class CCType : IComparable<CCType> {

	internal XType xType;
	internal CCNodeEntry allocationNode;
	internal int variant;
	internal bool constant;
	internal XType ofType;

	CCType()
	{
		this.xType = null;
		this.allocationNode = null;
		this.variant = 0;
		this.constant = false;
		this.ofType = null;
	}

	internal CCType(XType xType)
		: this(xType, null, 0, false)
	{
	}

	internal CCType(XType xType, CCNodeEntry allocationNode,
		int variant, bool constant)
	{
		this.xType = xType;
		this.allocationNode = allocationNode;
		this.variant = variant;
		this.constant = constant;
	}

	/*
	 * Get a CCType for an embedded type: this returns a CCType for
	 * xte, that shares the allocation node and variant of this instance.
	 */
	internal CCType GetEmbedded(XType xte)
	{
		if (xte == xType) {
			return this;
		}
		if (ofType != null) {
			throw new Exception("std::type does not embed another type");
		}
		return new CCType(xte, allocationNode, variant, constant);
	}

	internal static CCType NULL = new CCType();

	internal static CCType BOOL = new CCType(XType.BOOL);
	internal static CCType INT = new CCType(XType.INT);

	internal static CCType TypeOfType(XType xt)
	{
		CCType ct = new CCType();
		ct.xType = XType.XTYPE;
		ct.ofType = xt;
		ct.constant = true;
		return ct;
	}

	internal bool IsNull {
		get {
			return xType == null;
		}
	}

	internal bool IsBool {
		get {
			return xType == XType.BOOL;
		}
	}

	internal bool IsInt {
		get {
			return xType == XType.INT;
		}
	}

	public int CompareTo(CCType other)
	{
		if (xType == null) {
			if (other.xType == null) {
				return 0;
			}
			return -1;
		} else if (other.xType == null) {
			return 1;
		}
		int x = xType.Name.CompareTo(other.xType.Name);
		if (x != 0) {
			return x;
		}
		if (object.ReferenceEquals(
			allocationNode, other.allocationNode ))
		{
			x = 0;
		} else if (allocationNode == null) {
			x = -1;
		} else if (other.allocationNode == null) {
			x = 1;
		} else {
			x = allocationNode.CompareTo(other.allocationNode);
		}
		if (x != 0) {
			return x;
		}
		x = variant.CompareTo(other.variant);
		if (x != 0) {
			return x;
		}
		if (constant != other.constant) {
			return constant ? -1 : 1;
		}
		if (object.ReferenceEquals(ofType, other.ofType)) {
			return 0;
		} else if (ofType == null) {
			return -1;
		} else if (other.ofType == null) {
			return 1;
		} else {
			return ofType.Name.CompareTo(other.ofType.Name);
		}
	}

	public override bool Equals(object other)
	{
		if (object.ReferenceEquals(this, other)) {
			return true;
		}
		CCType cct = other as CCType;
		if (cct == null) {
			return false;
		}
		return CompareTo(cct) == 0;
	}

	public override int GetHashCode()
	{
		if (xType == null) {
			return 0;
		}
		uint hc = (uint)xType.Name.GetHashCode();
		if (allocationNode != null) {
			hc = ((hc << 5) | (hc >> 27))
				+ (uint)allocationNode.GetHashCode();
		}
		hc ^= (uint)variant;
		if (ofType != null) {
			hc -= (uint)ofType.Name.GetHashCode();
		}
		return (int)hc;
	}

	public override string ToString()
	{
		if (xType == null) {
			return "NONE";
		}
		StringBuilder sb = new StringBuilder();
		sb.Append(xType.Name);
		if (allocationNode != null) {
			sb.AppendFormat("(anode={0})", allocationNode);
		}
		if (variant != 0) {
			sb.AppendFormat("(variant={0})", variant);
		}
		if (constant) {
			sb.Append("(const)");
		}
		if (ofType != null) {
			sb.AppendFormat("(type={0})", ofType);
		}
		return sb.ToString();
	}
}

/*
 * A CCTypeSet is a set of CCType: it represents all possible types for
 * a value (stack slot, local variable...) at a given point in the
 * execution tree. It is never empty (as long as the slot exists, it
 * must have contents, if only the "uninitialized" state).
 */

class CCTypeSet : IEnumerable<CCType> {

	/*
	 * A CCTypeSet contains annotated types (CCType) but the set
	 * of unannotated types (XType) is also computed in this set.
	 * This set is not modified after construction of the CCTypeSet
	 * instance.
	 */
	internal XTypeSet xTypes;

	SortedSet<CCType> types;
	int cachedHashCode;

	CCTypeSet()
	{
		types = new SortedSet<CCType>();
		xTypes = new XTypeSet();
		cachedHashCode = Int32.MinValue;
	}

	internal CCTypeSet(CCType cct)
		: this()
	{
		types.Add(cct);
		if (cct.xType != null) {
			xTypes.Add(cct.xType);
		}
	}

	internal static CCTypeSet NULL = new CCTypeSet(CCType.NULL);

	internal static CCTypeSet BOOL = new CCTypeSet(CCType.BOOL);
	internal static CCTypeSet INT = new CCTypeSet(CCType.INT);

	internal static CCTypeSet[] ZERO = new CCTypeSet[0];

	internal bool MayBeNull {
		get {
			return types.Min.IsNull;
		}
	}

	/*
	 * If this set contains a single restricted type, then that
	 * type is returned; otherwise, null is returned.
	 */
	internal XType GetRestricted()
	{
		if (xTypes.Count == 1) {
			XType xt = xTypes.First();
			if (xt.IsRestricted) {
				return xt;
			}
		}
		return null;
	}

	internal bool IsSubsetOf(CCTypeSet ss)
	{
		if (object.ReferenceEquals(this, ss)) {
			return true;
		}
		if (types.Count > ss.types.Count) {
			return false;
		}
		foreach (CCType t in types) {
			if (!ss.types.Contains(t)) {
				return false;
			}
		}
		return true;
	}

	internal void CheckExact(XType xt)
	{
		List<string> r = null;
		foreach (CCType ct in types) {
			if (ct.xType == xt) {
				continue;
			}
			if (r == null) {
				r = new List<string>();
			}
			r.Add(ct.ToString());
		}
		if (r == null) {
			return;
		}
		throw UnexpectedType(xt.Name, r);
	}

	internal void CheckSubTypeOf(XType xt)
	{
		List<string> r = null;
		foreach (CCType ct in types) {
			if (ct.xType.IsSubTypeOf(xt)) {
				continue;
			}
			if (r == null) {
				r = new List<string>();
			}
			r.Add(ct.ToString());
		}
		if (r == null) {
			return;
		}
		throw UnexpectedType(xt.Name, r);
	}

	internal void CheckBool()
	{
		CheckExact(XType.BOOL);
	}

	internal void CheckInt()
	{
		CheckExact(XType.INT);
	}

	static Exception UnexpectedType(string exp, List<string> unexp)
	{
		StringBuilder sb = new StringBuilder();
		sb.AppendFormat("expecting {0}, but may get:");
		foreach (string s in unexp) {
			sb.Append(Environment.NewLine);
			sb.Append("   ");
			sb.Append(s);
		}
		return new Exception(sb.ToString());
	}

	internal void CheckAllocNode(CCNodeEntry node)
	{
		foreach (CCType ct in types) {
			CCNodeEntry e = ct.allocationNode;
			if (e == null) {
				continue;
			}
			if (e.IsDescendentOf(node)) {
				throw new Exception(string.Format("reference to local instance of type {0} may escape function node {1}", ct, node));
			}
		}
	}

	internal bool ContainsSubTypeOf(XType xt)
	{
		foreach (CCType ct in types) {
			if (ct.xType.IsSubTypeOf(xt)) {
				return true;
			}
		}
		return false;
	}

	/*
	 * Check that all values in this set are for std::type with
	 * a specific type target (i.e. "types of types").
	 */
	internal CCTypeSet ToNewInstance()
	{
		CCTypeSet r = new CCTypeSet();
		foreach (CCType ct in types) {
			if (ct.xType != XType.XTYPE) {
				throw new Exception(string.Format("instance allocation expects a std::type instance, got {0} instead", ct.xType.Name));
			}
			if (ct.ofType == null) {
				throw new Exception("instance allocation needs a specific type target, but got a generic std::type");
			}
			if (ct.ofType == XType.XTYPE) {
				throw new Exception("cannot dynamically allocate new std::type instances");
			}
			r.types.Add(new CCType(ct.ofType, null, 0, false));
			r.xTypes.Add(ct.ofType);
		}
		return r;
	}

	/*
	 * Merge two type sets into an aggregate set. If one of the sets
	 * is a subset of the other, then that other subset is returned;
	 * if the two sets are equal, then the ss1 instance is returned.
	 *
	 * This method traps on any attempt at merging incompatible type
	 * sets (ones with basic types).
	 */
	internal static CCTypeSet Merge(CCTypeSet ss1, CCTypeSet ss2)
	{
		string err;
		CCTypeSet cts = MergeNF(ss1, ss2, out err);
		if (cts == null) {
			throw new Exception(err);
		}
		return cts;
	}

	/*
	 * Merge two type sets into an aggregate set. If one of the sets
	 * is a subset of the other, then that other subset is returned;
	 * if the two sets are equal, then the ss1 instance is returned.
	 *
	 * If merging is not possible (incompatible basic types), then
	 * null is returned, and err is set to an appropriate error
	 * message. Otherwise, the merged type is returned, and err is
	 * set to null.
	 */
	internal static CCTypeSet MergeNF(CCTypeSet ss1, CCTypeSet ss2,
		out string err)
	{
		err = null;
		if (ss2.IsSubsetOf(ss1)) {
			return ss1;
		}
		if (ss1.IsSubsetOf(ss2)) {
			return ss2;
		}

		/*
		 * Handling of restricted types relies on the following
		 * features:
		 *
		 *  - A CCTypeSet that contains a restricted type can
		 *    only contain that restricted type, none other.
		 *
		 *  - Restricted types cannot have allocation nodes,
		 *    variants, or be std::type.
		 *
		 *  - The tests with IsSubsetOf() above have already
		 *    ruled out the case of two identical restricted types.
		 */
		if (ss1.types.Count == 1 && ss2.types.Count == 1) {
			CCType ct1 = ss1.types.Min;
			CCType ct2 = ss2.types.Min;
			if (ct1.xType.IsRestricted || ct2.xType.IsRestricted) {
				err = string.Format("forbidden type merging between {0} and {1}", ct1, ct2);
				return null;
			}
		}

		CCTypeSet ss3 = new CCTypeSet();
		foreach (CCType t in ss1.types) {
			ss3.types.Add(t);
			ss3.xTypes.Add(t.xType);
		}
		foreach (CCType t in ss2.types) {
			ss3.types.Add(t);
			ss3.xTypes.Add(t.xType);
		}
		return ss3;
	}

	public IEnumerator<CCType> GetEnumerator()
	{
		return types.GetEnumerator();
	}

	private IEnumerator GetEnumeratorNoType()
	{
		return this.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumeratorNoType();
	}

	public override bool Equals(object other)
	{
		if (object.ReferenceEquals(this, other)) {
			return true;
		}
		CCTypeSet ss = other as CCTypeSet;
		if (ss == null) {
			return false;
		}
		if (GetHashCode() != ss.GetHashCode()) {
			return false;
		}
		int n = types.Count;
		if (n != ss.types.Count) {
			return false;
		}
		SortedSet<CCType>.Enumerator e = ss.types.GetEnumerator();
		foreach (CCType t in types) {
			if (!e.MoveNext() || !t.Equals(e.Current)) {
				return false;
			}
		}
		return true;
	}

	public override int GetHashCode()
	{
		if (cachedHashCode != Int32.MinValue) {
			return cachedHashCode;
		}
		int hc = 0;
		foreach (CCType ct in types) {
			uint uhc = (uint)hc;
			hc = (int)((uhc << 3) | (uhc >> 29)) + ct.GetHashCode();
		}
		cachedHashCode = hc;
		return hc;
	}

	public override string ToString()
	{
		StringBuilder sb = new StringBuilder();
		foreach (CCType ct in types) {
			if (sb.Length == 0) {
				sb.Append("<");
			} else {
				sb.Append(",");
			}
			sb.Append(ct.ToString());
		}
		sb.Append(">");
		return sb.ToString();
	}

	internal class ComboEnumerator {

		CCTypeSet[] ctss;
		SortedSet<CCType>.Enumerator[] enums;
		bool first, last;

		internal ComboEnumerator(CCTypeSet[] ctss)
		{
			this.ctss = ctss;
			int n = ctss.Length;
			enums = new SortedSet<CCType>.Enumerator[n];
			for (int i = 0; i < n; i ++) {
				enums[i] = ctss[i].types.GetEnumerator();
				if (!enums[i].MoveNext()) {
					throw new Exception("empty type set");
				}
			}
			first = true;
			last = false;
		}

		/*
		 * Get the next combination. The destination array MUST
		 * have the same length as the source array of type sets.
		 */
		internal bool Next(CCType[] dst)
		{
			if (last) {
				return false;
			}
			int n = enums.Length;
			if (first) {
				first = false;
			} else {
				int i;
				for (i = 0; i < n; i ++) {
					if (enums[i].MoveNext()) {
						break;
					}
					enums[i] = ctss[i].types
						.GetEnumerator();
					enums[i].MoveNext();
				}
				if (i >= n) {
					last = true;
					return false;
				}
			}
			for (int j = 0; j < n; j ++) {
				dst[j] = enums[j].Current;
			}
			return true;
		}
	}
}
