using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

/*
 * A CCStack instance represents the possible types of values on the
 * stack at a given point.
 */

class CCStack {

	StackElt tos;

	CCStack(StackElt tos)
	{
		this.tos = tos;
	}

	internal CCStack()
		: this(null)
	{
	}

	internal int Depth {
		get {
			if (tos == null) {
				return 0;
			} else {
				return tos.rank + 1;
			}
		}
	}

	internal CCTypeSet Peek(int depth)
	{
		if (depth > tos.rank) {
			throw new Exception("stack underflow");
		}
		StackElt e = tos;
		while (depth -- > 0) {
			e = e.upper;
		}
		return e.cts;
	}

	internal CCStack Push(CCTypeSet cts)
	{
		return new CCStack(new StackElt(tos, cts));
	}

	internal CCStack Push(CCTypeSet[] ctss)
	{
		StackElt e = tos;
		for (int i = 0; i < ctss.Length; i ++) {
			e = new StackElt(e, ctss[i]);
		}
		return new CCStack(e);
	}

	internal CCStack Push(CCType ct)
	{
		return Push(new CCTypeSet(ct));
	}

	internal CCStack Push(CCType[] ctt)
	{
		StackElt e = tos;
		for (int i = 0; i < ctt.Length; i ++) {
			e = new StackElt(e, new CCTypeSet(ctt[i]));
		}
		return new CCStack(e);
	}

	internal CCStack Pop()
	{
		if (tos == null) {
			throw new Exception("stack underflow");
		}
		return new CCStack(tos.upper);
	}

	internal CCStack Pop(int num)
	{
		StackElt e = tos;
		while (num -- > 0) {
			if (e == null) {
				throw new Exception("stack underflow");
			}
			e = e.upper;
		}
		return new CCStack(e);
	}

	internal void CheckAllocNode(CCNodeEntry node)
	{
		for (StackElt e = tos; e != null; e = e.upper) {
			e.cts.CheckAllocNode(node);
		}
	}

	/*
	 * This returns true if there is a combination of stack types
	 * that are sub-types of the types in xts[].
	 */
	internal bool MayMatch(XType[] xts)
	{
		if (xts.Length > Depth) {
			return false;
		}
		int j = xts.Length - 1;
		StackElt e = tos;
		for (;;) {
			if (!e.cts.ContainsSubTypeOf(xts[j])) {
				return false;
			}
			j --;
			if (j < 0) {
				break;
			}
			e = e.upper;
		}
		return true;
	}

	internal CCTypeSet[] GetTopElements(int depth)
	{
		CCTypeSet[] ctss = new CCTypeSet[depth];
		StackElt e = tos;
		for (int j = 0; j < depth; j ++) {
			ctss[depth - 1 - j] = e.cts;
			e = e.upper;
		}
		return ctss;
	}

	internal void CheckMultipleSubTypeOf(XType[] xts)
	{
		if (xts.Length > Depth) {
			throw new Exception("stack underflow");
		}
		StackElt e = tos;
		for (int j = xts.Length - 1; j >= 0; j --, e = e.upper) {
			e.cts.CheckSubTypeOf(xts[j]);
		}
	}

	internal static CCStack Merge(CCStack s1, CCStack s2)
	{
		if (object.ReferenceEquals(s1, s2)) {
			return s1;
		}
		int n1 = s1.Depth;
		int n2 = s2.Depth;
		if (n1 != n2) {
			throw new Exception(string.Format("stack merge depth mismatch ({0} / {1})", n1, n2));
		}

		/*
		 * Find the common root (it may be null). We retain in
		 * 'root' the topmost element at which the two stacks have
		 * equal contents.
		 */
		StackElt e1 = s1.tos, e2 = s2.tos;
		StackElt root = null;
		for (;;) {
			/*
			 * When we have reached the same StackElt instance,
			 * no further divergence may occur.
			 */
			if (object.ReferenceEquals(e1, e2)) {
				if (root == null) {
					root = e1;
				}
				break;
			}
			if (e1.cts.Equals(e2.cts)) {
				if (root == null) {
					root = e1;
				}
			} else {
				root = null;
			}
			e1 = e1.upper;
			e2 = e2.upper;
		}

		int rootRank = (root == null) ? -1 : root.rank;

		/*
		 * If the root has maximal rank, then the two stacks are
		 * identical.
		 */
		if (rootRank == n1 - 1) {
			return s1;
		}

		/*
		 * Everything above the root must be instantiated anew. We
		 * create the instances in a top-down fashion, fixing links
		 * on the way.
		 */
		StackElt tos = null;
		StackElt e3 = null;
		e1 = s1.tos;
		e2 = s2.tos;
		for (int i = n1 - 1; i > rootRank; i --) {
			StackElt e4 = new StackElt(null,
				CCTypeSet.Merge(e1.cts, e2.cts));
			e4.rank = i;
			if (e3 == null) {
				tos = e4;
			} else {
				e3.upper = e4;
			}
			e3 = e4;
			e1 = e1.upper;
			e2 = e2.upper;
		}
		e3.upper = root;

		return new CCStack(tos);
	}

	internal IEnumerator<CCTypeSet> EnumerateFromTop()
	{
		return new StackEnumerator(tos);
	}

	public override string ToString()
	{
		List<string> r = new List<string>();
		for (StackElt e = tos; e != null; e = e.upper) {
			r.Add(e.cts.ToString());
		}
		StringBuilder sb = new StringBuilder();
		for (int n = r.Count - 1; n >= 0; n --) {
			if (sb.Length > 0) {
				sb.Append(" ");
			}
			sb.Append(r[n].ToString());
		}
		return sb.ToString();
	}

	class StackElt {

		internal StackElt upper;
		internal CCTypeSet cts;
		internal int rank;

		internal StackElt(StackElt upper, CCTypeSet cts)
		{
			this.upper = upper;
			this.cts = cts;
			if (upper == null) {
				rank = 0;
			} else {
				rank = upper.rank + 1;
			}
		}
	}

	class StackEnumerator : IEnumerator<CCTypeSet> {

		public CCTypeSet Current {
			get; private set;
		}

		object IEnumerator.Current {
			get {
				return this.Current;
			}
		}

		StackElt ptr;
		StackElt tos;
		bool start;

		internal StackEnumerator(StackElt tos)
		{
			Current = null;
			this.tos = tos;
			ptr = null;
			start = true;
		}

		public bool MoveNext()
		{
			if (start) {
				start = false;
				ptr = tos;
			} else {
				if (ptr == null) {
					return false;
				}
				ptr = ptr.upper;
			}
			if (ptr == null) {
				return false;
			} else {
				Current = ptr.cts;
				return true;
			}
		}

		public void Reset()
		{
			ptr = null;
			start = true;
		}

		public void Dispose()
		{
		}
	}
}
