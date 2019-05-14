using System;
using System.Text;

/*
 * An XBuffer is a wrapper around an array of values. It is used as
 * backing store for XArrayGeneric instances. An XBuffer is not a T1
 * object (T1 code cannot have references to XBuffer instances), but
 * it can nonetheless be included in generated code.
 */

abstract class XBuffer : XSerial {

	internal abstract XValue Get(int index);

	internal abstract void Set(int index, XValue xv);

	internal abstract void Clear(int index);

	internal abstract bool IsUninitialized(int index);

	internal virtual string ToString(int off, int len)
	{
		throw new Exception("not a std::string");
	}
}

class XBufferBool : XBuffer {

	bool[] buf;

	internal XBufferBool(int len)
	{
		buf = new bool[len];
	}

	internal override XValue Get(int index)
	{
		return buf[index];
	}

	internal override void Set(int index, XValue xv)
	{
		buf[index] = xv;
	}

	internal override void Clear(int index)
	{
		buf[index] = false;
	}

	internal override bool IsUninitialized(int index)
	{
		return false;
	}
}

class XBufferU8 : XBuffer {

	byte[] buf;

	internal XBufferU8(int len)
	{
		buf = new byte[len];
	}

	internal XBufferU8(byte[] buf)
	{
		this.buf = buf;
	}

	internal override XValue Get(int index)
	{
		return buf[index];
	}

	internal override void Set(int index, XValue xv)
	{
		buf[index] = xv;
	}

	internal override void Clear(int index)
	{
		buf[index] = 0;
	}

	internal override bool IsUninitialized(int index)
	{
		return false;
	}

	internal override string ToString(int off, int len)
	{
		StringBuilder sb = new StringBuilder();
		int p = off;
		int q = off + len;
		while (p < q) {
			int v = buf[p ++];
			int n;
			if (v < 0x80) {
				sb.Append((char)v);
				continue;
			} else if (v < 0xC0) {
				throw new Exception("invalid UTF-8 encoding");
			} else if (v < 0xE0) {
				n = 1;
				v &= 0x1F;
			} else if (v < 0xF0) {
				n = 2;
				v &= 0x0F;
			} else if (v < 0xF8) {
				n = 3;
				v &= 0x07;
			} else {
				throw new Exception("invalid UTF-8 encoding");
			}
			if ((p + n) > q) {
				throw new Exception("invalid UTF-8 encoding");
			}
			while (n -- > 0) {
				v <<= 6;
				int w = buf[p ++];
				if (w < 0x80 || w >= 0xC0) {
					throw new Exception("invalid UTF-8 encoding");
				}
				v |= (w & 0x3F);
			}
			if ((v >= 0xFDD0 && v <= 0xFDEF)
				|| (v >= 0xD800 && v <= 0xDFFF)
				|| ((v & 0xFFFF) >= 0xFFFE)
				|| v > 0x10FFFF)
			{
				throw new Exception("invalid UTF-8 encoding");
			}
			if (v >= 0x10000) {
				v -= 0x10000;
				sb.Append((char)(0xD800 + (v >> 10)));
				sb.Append((char)(0xDC00 + (v & 0x3FF)));
			} else {
				sb.Append((char)v);
			}
		}
		return sb.ToString();
	}
}

class XBufferU16 : XBuffer {

	ushort[] buf;

	internal XBufferU16(int len)
	{
		buf = new ushort[len];
	}

	internal override XValue Get(int index)
	{
		return buf[index];
	}

	internal override void Set(int index, XValue xv)
	{
		buf[index] = xv;
	}

	internal override void Clear(int index)
	{
		buf[index] = 0;
	}

	internal override bool IsUninitialized(int index)
	{
		return false;
	}
}

class XBufferU32 : XBuffer {

	uint[] buf;

	internal XBufferU32(int len)
	{
		buf = new uint[len];
	}

	internal override XValue Get(int index)
	{
		return buf[index];
	}

	internal override void Set(int index, XValue xv)
	{
		buf[index] = xv;
	}

	internal override void Clear(int index)
	{
		buf[index] = 0;
	}

	internal override bool IsUninitialized(int index)
	{
		return false;
	}
}

class XBufferU64 : XBuffer {

	ulong[] buf;

	internal XBufferU64(int len)
	{
		buf = new ulong[len];
	}

	internal override XValue Get(int index)
	{
		return buf[index];
	}

	internal override void Set(int index, XValue xv)
	{
		buf[index] = xv;
	}

	internal override void Clear(int index)
	{
		buf[index] = 0;
	}

	internal override bool IsUninitialized(int index)
	{
		return false;
	}
}

class XBufferI8 : XBuffer {

	sbyte[] buf;

	internal XBufferI8(int len)
	{
		buf = new sbyte[len];
	}

	internal override XValue Get(int index)
	{
		return buf[index];
	}

	internal override void Set(int index, XValue xv)
	{
		buf[index] = xv;
	}

	internal override void Clear(int index)
	{
		buf[index] = 0;
	}

	internal override bool IsUninitialized(int index)
	{
		return false;
	}
}

class XBufferI16 : XBuffer {

	short[] buf;

	internal XBufferI16(int len)
	{
		buf = new short[len];
	}

	internal override XValue Get(int index)
	{
		return buf[index];
	}

	internal override void Set(int index, XValue xv)
	{
		buf[index] = xv;
	}

	internal override void Clear(int index)
	{
		buf[index] = 0;
	}

	internal override bool IsUninitialized(int index)
	{
		return false;
	}
}

class XBufferI32 : XBuffer {

	int[] buf;

	internal XBufferI32(int len)
	{
		buf = new int[len];
	}

	internal override XValue Get(int index)
	{
		return buf[index];
	}

	internal override void Set(int index, XValue xv)
	{
		buf[index] = xv;
	}

	internal override void Clear(int index)
	{
		buf[index] = 0;
	}

	internal override bool IsUninitialized(int index)
	{
		return false;
	}
}

class XBufferI64 : XBuffer {

	long[] buf;

	internal XBufferI64(int len)
	{
		buf = new long[len];
	}

	internal override XValue Get(int index)
	{
		return buf[index];
	}

	internal override void Set(int index, XValue xv)
	{
		buf[index] = xv;
	}

	internal override void Clear(int index)
	{
		buf[index] = 0;
	}

	internal override bool IsUninitialized(int index)
	{
		return false;
	}
}

class XBufferGen : XBuffer {

	XValue[] buf;

	internal XBufferGen(int len)
	{
		buf = new XValue[len];
	}

	internal XBufferGen(XValue[] buf)
	{
		this.buf = buf;
	}

	internal override XValue Get(int index)
	{
		return buf[index];
	}

	internal override void Set(int index, XValue xv)
	{
		buf[index] = xv;
	}

	internal override void Clear(int index)
	{
		/*
		 * We here assume that XBufferGen is not used for arrays
		 * of restricted types.
		 */
		buf[index].Clear();
	}

	internal override bool IsUninitialized(int index)
	{
		return buf[index].IsUninitialized;
	}
}
