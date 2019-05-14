using System;

/*
 * A CCLocals instance contains the types of local variables (fields).
 * Instances of CCLocals are immutable.
 */

class CCLocals {

	/*
	 * Index: coalesced offsets (for an array of locals, only one
	 * item is used).
	 */
	CCTypeSet[] locals;

	CCLocals(CCTypeSet[] locals)
	{
		this.locals = locals;
	}

	/*
	 * Create a new instance with all local variables being
	 * uninitialized. This is the normal state upon function
	 * entry.
	 */
	internal CCLocals(int size)
	{
		locals = new CCTypeSet[size];
		for (int i = 0; i < size; i ++) {
			locals[i] = CCTypeSet.NULL;
		}
	}

	internal CCTypeSet Get(int index)
	{
		CCTypeSet cts = locals[index];
		if (cts.MayBeNull) {
			throw new Exception("potentially reading uninitialized local variable");
		}
		return cts;
	}

	internal CCLocals Set(int index, CCTypeSet cts)
	{
		CCTypeSet octs = locals[index];
		if (!cts.IsSubsetOf(octs)) {
			CCLocals r = new CCLocals(locals.Length);
			Array.Copy(locals, 0, r.locals, 0, locals.Length);
			r.locals[index] = cts;
			return r;
		} else {
			return this;
		}
	}

	internal static CCLocals Merge(CCLocals l1, CCLocals l2)
	{
		string err;
		CCLocals ccl = MergeNF(l1, l2, out err);
		if (ccl == null) {
			throw new Exception(err);
		}
		return ccl;
	}

	internal static CCLocals MergeNF(
		CCLocals l1, CCLocals l2, out string err)
	{
		err = null;
		if (object.ReferenceEquals(l1, l2)) {
			return l1;
		}
		int n = l1.locals.Length;
		if (n != l2.locals.Length) {
			err = string.Format("internal error: mismatch on number of locals ({0} / {1})", n, l2.locals.Length);
			return null;
		}

		/*
		 * Merge entries one by one. The destination is lazily
		 * updated to avoid allocation when not needed.
		 */
		bool r1ok = true, r2ok = true;
		CCTypeSet[] nlocals = null;
		for (int i = 0; i < n; i ++) {
			CCTypeSet cts1 = l1.locals[i];
			CCTypeSet cts2 = l2.locals[i];
			if (cts1.Equals(cts2)) {
				if (nlocals != null) {
					nlocals[i] = cts1;
				}
			} else if (cts1.IsSubsetOf(cts2)) {
				r1ok = false;
				if (r2ok) {
					continue;
				}
				if (nlocals != null) {
					nlocals[i] = cts2;
				}
			} else if (cts2.IsSubsetOf(cts1)) {
				r2ok = false;
				if (r1ok) {
					continue;
				}
				if (nlocals != null) {
					nlocals[i] = cts1;
				}
			} else {
				if (nlocals == null) {
					r1ok = false;
					r2ok = false;
					nlocals = new CCTypeSet[n];
					Array.Copy(l1.locals, 0, nlocals, 0, i);
				}
				CCTypeSet cts = CCTypeSet.MergeNF(
					cts1, cts2, out err);
				if (cts == null) {
					return null;
				}
				nlocals[i] = cts;
			}
		}

		if (r1ok) {
			return l1;
		}
		if (r2ok) {
			return l2;
		}
		return new CCLocals(nlocals);
	}
}
