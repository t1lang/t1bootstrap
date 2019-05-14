using System;
using System.Collections.Generic;

/*
 * A CCStruct instance contains the types of fields in a given structure
 * type. Instances are mutable: type analysis adds new possible types
 * as it progresses.
 *
 * Recorded types do not include the "uninitialized" status: all fields
 * (except for booleans and modular integers) start at "uninitialized",
 * and are tested dynamically.
 *
 * Nodes that depend on field types (e.g. nodes for accessor functions)
 * register on this instance to be updated when new types are set in
 * the relevant field.
 *
 * CCStruct instances are also used for arrays of references. In that
 * case, the instance keeps track of a single field, which represents
 * the array contents.
 */

class CCStruct {

	CCType owner;
	CCTypeSet[] fields;
	SortedSet<CCNode>[] regNodes;

	CCStruct(CCType owner)
	{
		owner.xType.Close();
		this.owner = owner;
		XType[] fti = owner.xType.GetFieldInitTypes();
		int size = fti.Length;
		fields = new CCTypeSet[size];
		regNodes = new SortedSet<CCNode>[size];
		for (int i = 0; i < fti.Length; i ++) {
			if (fti[i] == null) {
				continue;
			}
			fields[i] = new CCTypeSet(new CCType(fti[i]));
		}
	}

	static SortedDictionary<CCType, CCStruct> ALL =
		new SortedDictionary<CCType, CCStruct>();

	internal static CCStruct Lookup(CCType ct)
	{
		CCStruct cs;
		if (!ALL.TryGetValue(ct, out cs)) {
			cs = new CCStruct(ct);
			ALL[ct] = cs;
		}
		return cs;
	}

	/*
	 * Get the current type for a field. This may be null if there is
	 * no known type yet.
	 */
	internal CCTypeSet Get(int index)
	{
		return fields[index];
	}

	internal void Merge(int index, CCTypeSet cts)
	{
		/*
		 * Escape analysis: we must not write in a field of
		 * a structure a value that pertains to a descendent
		 * allocator node.
		 */
		CCNodeEntry oa = owner.allocationNode;
		if (oa != null) {
			foreach (CCType ct in cts) {
				if (oa.IsDescendentOf(ct.allocationNode)) {
					continue;
				}
				throw new Exception(string.Format("reference to local instance of type {0} may escape through writing in field of type {1}", ct, owner));
			}
		}

		CCTypeSet octs = fields[index];
		CCTypeSet ncts;
		if (octs == null) {
			ncts = cts;
		} else {
			ncts = CCTypeSet.Merge(octs, cts);
		}
		if (!object.ReferenceEquals(octs, ncts)) {
			SortedSet<CCNode> rn = regNodes[index];
			if (rn != null) {
				foreach (CCNode node in rn) {
					node.MarkUpdate();
				}
			}
		}
	}

	internal void Register(int index, CCNode node)
	{
		SortedSet<CCNode> rn = regNodes[index];
		if (rn == null) {
			rn = new SortedSet<CCNode>();
			regNodes[index] = rn;
		}
		rn.Add(node);
	}

	public override string ToString()
	{
		return owner.ToString();
	}
}
