using System;
using System.Collections.Generic;
using System.Text;

/*
 * A GStack instance represents the possible types of values on the
 * stack at a given point. Compared to CCStack, GStack is simpler, in
 * that it does not distinguishes between allocations; each element in
 * a GStack is a set of XType values. GStack is used for code generation.
 */

class GStack {

	GStackElt tos;

	GStack(GStackElt tos)
	{
		this.tos = tos;
	}

	internal GStack()
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

	internal XTypeSet Peek(int depth)
	{
		if (depth > tos.rank) {
			throw new Exception("stack underflow");
		}
		GStackElt e = tos;
		while (depth -- > 0) {
			e = e.upper;
		}
		return e.xts;
	}

	internal static GStack Make(XType[] topCombo, GStack tail)
	{
		GStackElt tos = tail.tos;
		for (int i = topCombo.Length - 1; i >= 0; i --) {
			XTypeSet xts = new XTypeSet();
			xts.Add(topCombo[i]);
			tos = new GStackElt(tos, xts);
		}
		return new GStack(tos);
	}

	internal static GStack Make(CCStack src)
	{
		return new GStack(MakeTOS(src));
	}

	static GStackElt MakeTOS(CCStack src)
	{
		if (src == null) {
			return null;
		}
		GStackElt tos = null;
		GStackElt r = null;
		int num = 0;
		IEnumerator<CCTypeSet> iter = src.EnumerateFromTop();
		for (;;) {
			if (!iter.MoveNext()) {
				break;
			}
			XTypeSet ss = new XTypeSet();
			foreach (CCType ct in iter.Current) {
				ss.Add(ct.xType);
			}
			if (ss.Count == 0) {
				throw new Exception(string.Format("internal error: no element in CCStack at depth {0}", num));
			}
			GStackElt r2 = new GStackElt(null, ss);
			if (r == null) {
				tos = r2;
			} else {
				r.upper = r2;
			}
			r = r2;
			num ++;
		}

		/*
		 * We assembled the GStack in reverse order, so all
		 * ranks are wrong and we must fix them.
		 */
		for (r = tos; r != null; r = r.upper) {
			r.rank = -- num;
		}

		return tos;
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
		GStackElt e = tos;
		for (;;) {
			bool found = false;
			foreach (XType xt in e.xts) {
				if (xt.IsSubTypeOf(xts[j])) {
					found = true;
					break;
				}
			}
			if (!found) {
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

	public override string ToString()
	{
		StringBuilder sb;
		List<string> r = new List<string>();
		for (GStackElt e = tos; e != null; e = e.upper) {
			sb = new StringBuilder();
			foreach (XType xt in e.xts) {
				if (sb.Length == 0) {
					sb.Append("<");
				} else {
					sb.Append(",");
				}
				sb.Append(xt.Name);
			}
			sb.Append(">");
			r.Add(sb.ToString());
		}
		sb = new StringBuilder();
		for (int n = r.Count - 1; n >= 0; n --) {
			if (sb.Length > 0) {
				sb.Append(" ");
			}
			sb.Append(r[n]);
		}
		return sb.ToString();
	}

	class GStackElt {

		internal GStackElt upper;
		internal XTypeSet xts;
		internal int rank;

		internal GStackElt(GStackElt upper, XTypeSet xts)
		{
			this.upper = upper;
			this.xts = xts;
			if (upper == null) {
				rank = 0;
			} else {
				rank = upper.rank + 1;
			}
		}
	}

	/*
	internal class ComboEnumerator {

		SortedSet<XType>.Enumerator[] enums;
		bool first, last;

		internal ComboEnumerator(GStack gs, int depth)
		{
			enums = new SortedSet<XType>.Enumerator[depth];
			GStackElt e = gs.tos;
			for (int i = n - 1; i >= 0; i --) {
				enums[i] = e.xts.GetEnumerator();
				if (!enums[i].MoveNext()) {
					throw new Exception("empty type set");
				}
				e = e.upper;
			}
			first = true;
			last = false;
		}

		internal bool Next(XType[] dst)
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
					enums[i].Reset();
					if (!enums[i].MoveNext()) {
						throw new Exception("empty type set");
					}
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
	*/
}
