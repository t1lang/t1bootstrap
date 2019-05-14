using System;
using System.Collections.Generic;

/*
 * XArrayGeneric is the instance class for an array; it is used by
 * both arrays of references and arrays of embedded types.
 */

class XArrayGeneric : XObject {

	internal XBuffer dataBuf;
	internal int dataOff;
	internal int dataLen;

	internal bool IsUninitialized {
		get {
			return dataBuf == null;
		}
	}

	internal int Length {
		get {
			if (dataBuf == null) {
				throw new Exception("access to length of uninitalized array");
			}
			return dataLen;
		}
	}

	internal string String {
		get {
			return dataBuf.ToString(dataOff, dataLen);
		}
	}

	internal XArrayGeneric(XType type)
		: base(type)
	{
		dataBuf = null;
		dataOff = 0;
		dataLen = 0;
	}

	internal void Init(XBuffer dataBuf, int dataOff, int dataLen)
	{
		this.dataBuf = dataBuf;
		this.dataOff = dataOff;
		this.dataLen = dataLen;
	}

	internal void Init(int len)
	{
		XType xt = ObjectType;
		if (xt.IsRestricted) {
			if (xt == XType.BOOL) {
				dataBuf = new XBufferBool(len);
			} else if (xt == XType.U8) {
				dataBuf = new XBufferU8(len);
			} else if (xt == XType.U16) {
				dataBuf = new XBufferU16(len);
			} else if (xt == XType.U32) {
				dataBuf = new XBufferU32(len);
			} else if (xt == XType.U64) {
				dataBuf = new XBufferU64(len);
			} else if (xt == XType.I8) {
				dataBuf = new XBufferI8(len);
			} else if (xt == XType.I16) {
				dataBuf = new XBufferI16(len);
			} else if (xt == XType.I32) {
				dataBuf = new XBufferI32(len);
			} else if (xt == XType.I64) {
				dataBuf = new XBufferI64(len);
			} else {
				throw new Exception(string.Format("unknown restricted type: {0}", xt.Name));
			}
		} else {
			dataBuf = new XBufferGen(len);
		}
		dataOff = 0;
		dataLen = len;
	}

	internal void InitSub(XArrayGeneric src, int off, int len)
	{
		if (src.dataBuf == null) {
			throw new Exception("making sub-view of uninitialized array");
		}
		if (off < 0 || off > src.dataLen
			|| len < 0 || len > (src.dataLen - off))
		{
			throw new Exception(string.Format("sub-view ({0},{1}) does not fit in source array length {2}", off, len, src.dataLen));
		}
		this.dataBuf = src.dataBuf;
		this.dataOff = src.dataOff + off;
		this.dataLen = len;
	}

	void CheckIndex(int index)
	{
		if (dataBuf == null) {
			throw new Exception("access to item in uninitalized array");
		}
		if (index < 0 || index >= dataLen) {
			throw new Exception(string.Format("out-of-bounds array index: {0}", index));
		}
	}

	internal XValue this[int index] {
		get {
			CheckIndex(index);
			XValue xv = dataBuf.Get(dataOff + index);
			if (xv.IsUninitialized) {
				throw new Exception("reading uninitialized array element");
			}
			return xv;
		}
		set {
			CheckIndex(index);
			dataBuf.Set(dataOff + index, value);
		}
	}

	internal void Clear(int index)
	{
		CheckIndex(index);
		dataBuf.Clear(dataOff + index);
	}

	internal bool IsElementUninitialized(int index)
	{
		CheckIndex(index);
		return dataBuf.IsUninitialized(index);
	}

	/*
	 * Warning: this function does not check compatibility of values
	 * with the destination type.
	 */
	internal static XArrayGeneric Concat(
		XType type, params XArrayGeneric[] aa)
	{
		List<XValue> r = new List<XValue>();
		foreach (XArrayGeneric a in aa) {
			int n = a.Length;
			for (int i = 0; i < n; i ++) {
				r.Add(a[i]);
			}
		}
		XArrayGeneric xag = new XArrayGeneric(type);
		XBuffer xb = new XBufferGen(r.ToArray());
		xag.Init(xb, 0, r.Count);
		return xag;
	}

	public override string ToString()
	{
		if (ObjectType == XType.ARRAY_U8) {
			try {
				return String;
			} catch (Exception) {
				// ignored
			}
		}
		return string.Format("[type={0} len={1}]",
			ObjectType.Name, dataLen);
	}
}
