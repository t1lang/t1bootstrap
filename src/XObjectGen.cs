using System;

class XObjectGen : XObject {

	internal XValue[] fields;
	internal XObject[] embeds;

	internal XObjectGen(XType type, XValue[] fields, XObject[] embeds)
		: base(type)
	{
		this.fields = fields;
		this.embeds = embeds;
	}

	internal override XObject FindExt(string name)
	{
		XType xt = ObjectType;
		if (name == xt.Name) {
			return this;
		}
		int[] p;
		if (!xt.extensionPath.TryGetValue(name, out p)) {
			throw new Exception(string.Format("type {0} does not extend type {1}", xt.Name, name));
		}
		if (p.Length == 0) {
			throw new Exception(string.Format("ambiguous type extension {0} -> {1}", xt.Name, name));
		}
		XObject xo = this;
		for (int i = 0; i < p.Length; i ++) {
			XObjectGen xog = xo as XObjectGen;
			if (xog == null) {
				throw new Exception(string.Format("invalid extension path {0} -> {1}", xt.Name, name));
			}
			xo = xog.embeds[p[i]];
		}
		return xo;
	}
}
