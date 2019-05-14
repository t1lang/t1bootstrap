using System;
using System.Collections.Generic;
using System.Text;

class Dispatcher {

	List<DCall> calls;
	DNode root;

	Dispatcher(List<DCall> calls, DNode root)
	{
		this.calls = calls;
		this.root = root;
	}

	/*
	 * Make a dispatcher out of the provided list of calls. If the
	 * dispatcher cannot be solved, null is returned. The provided
	 * list MUST NOT be modified afterwards.
	 */
	internal static Dispatcher Make(List<DCall> calls)
	{
		DNode root = MakeNode(calls, 0);
		if (root == null) {
			return null;
		}
		return new Dispatcher(calls, root);
	}

	/*
	 * Make a new dispatcher that merges the two provided instances.
	 * If a merged dispatcher is not feasible, null is returned. The
	 * original dispatchers are not modified.
	 */
	internal static Dispatcher Merge(Dispatcher d1, Dispatcher d2)
	{
		List<DCall> calls = new List<DCall>();
		calls.AddRange(d1.calls);
		calls.AddRange(d2.calls);
		DNode root = MakeNode(calls, 0);
		if (root == null) {
			return null;
		}
		return new Dispatcher(calls, root);
	}

	/*
	 * Make a decision tree node. On input, all currently matching
	 * calls are provided, up to the specified depth (excluded).
	 * If the tree cannot be built, then null is returned.
	 *
	 * TODO: this can suffer from combinatorial explosion, so there
	 * should be some safeguard here.
	 */
	static DNode MakeNode(List<DCall> calls, int depth)
	{
		/*
		 * If all calls map to the same function, then make a leaf.
		 */
		Function f = null;
		bool single = true;
		foreach (DCall dc in calls) {
			if (f == null) {
				f = dc.f;
			} else if (f != dc.f) {
				single = false;
				break;
			}
		}
		if (single) {
			if (f == null) {
				throw new Exception("internal error: no calls at all");
			}
			return new DNodeLeaf(f);
		}

		/*
		 * There are multiple targets. We must read the next
		 * stack element. Failure conditions:
		 *  - The bottom of one of the stacks has been reached.
		 *  - Reading on one stack yields a restricted type,
		 *    and not all stacks have that exact restricted type
		 *    at that emplacement.
		 */
		XTypeSet xtsAll = new XTypeSet();
		foreach (DCall dc in calls) {
			if (depth >= dc.gs.Depth) {
				return null;
			}
			XTypeSet xts = dc.gs.Peek(depth);
			if (xts.IsRestricted || xtsAll.IsRestricted) {
				if (!xtsAll.Equals(xts)) {
					return null;
				}
				continue;
			}
			foreach (XType xt in xts) {
				xtsAll.Add(xt);
			}
		}

		/*
		 * For all possible types at this point, get the
		 * corresponding child subtree. For that, we must prune
		 * the list of calls of non-matching elements.
		 */
		SortedDictionary<XType, DNode> children =
			new SortedDictionary<XType, DNode>();
		foreach (XType xt in xtsAll) {
			List<DCall> calls2 = new List<DCall>();
			foreach (DCall dc in calls) {
				if (dc.gs.Peek(depth).Contains(xt)) {
					calls2.Add(dc);
				}
			}
			children[xt] = MakeNode(calls2, depth + 1);
		}
		return new DNodeInner(children);
	}
}

abstract class DNode {
}

class DNodeLeaf : DNode {

	internal Function f;

	internal DNodeLeaf(Function f)
	{
		this.f = f;
	}
}

class DNodeInner : DNode {

	internal SortedDictionary<XType, DNode> children;

	internal DNodeInner(SortedDictionary<XType, DNode> children)
	{
		this.children = children;
	}
}

class DCall {

	internal GStack gs;
	internal Function f;

	internal DCall(GStack gs, Function f)
	{
		this.gs = gs;
		this.f = f;
	}
}
