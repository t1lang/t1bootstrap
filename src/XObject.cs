using System;

abstract class XObject : XSerial {

	internal XType ObjectType {
		get {
			if (objectType == null) {
				objectType = XType.XTYPE;
			}
			return objectType;
		}
	}

	XType objectType;

	internal XObject(XType type)
	{
		this.objectType = type;
	}

	internal virtual XObject FindExt(string name)
	{
		throw new Exception(string.Format("type {0} does not extend type {1}", ObjectType.Name, name));
	}

	public override string ToString()
	{
		return "-";
	}
}
