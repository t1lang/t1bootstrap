using System;
using System.Runtime.CompilerServices;
using System.Text;

/*
 * A "value" is a reference to an object instance. For the basic types
 * (booleans and fixed-size integers), the instance is virtual (i.e. it
 * does not really exist), and the actual value is held in a specialized
 * field.
 */

struct XValue {

	XType basicType;    // non-null for basic types
	XObject obj;        // null for basic types
	ulong basic;        // contains the value for basic types

	/*
	 * Create a basic type value.
	 */
	internal XValue(XType basicType, ulong basic)
	{
		this.basicType = basicType;
		this.obj = null;
		this.basic = basic;
	}

	/*
	 * Create a non-basic type value.
	 */
	internal XValue(XObject obj)
	{
		this.basicType = null;
		this.obj = obj;
		this.basic = 0;
	}

	/*
	 * Create a value for a C# string (converted to an array of bytes
	 * with UTF-8 encoding).
	 */
	internal XValue(string s)
	{
		this.basicType = null;
		this.obj = null;
		this.basic = 0;
		String = s;
	}

	/*
	 * Test if the value is uninitialized. Note: stack markers also
	 * qualify as uninitialized values.
	 */
	internal bool IsUninitialized {
		get {
			return basicType == null && obj == null;
		}
	}

	/*
	 * Test if the value is a stack marker.
	 */
	internal bool IsMarker {
		get {
			return basicType == null && obj == null && basic != 0;
		}
	}

	/*
	 * Get the runtime type of this value.
	 * (Don't call this on a potentially uninitialized value.)
	 */
	internal XType VType {
		get {
			if (basicType != null) {
				return basicType;
			} else {
				return obj.ObjectType;
			}
		}
	}

	/*
	 * Get/set the value as a bool.
	 */
	internal bool Bool {
		get {
			if (basicType != XType.BOOL) {
				throw new Exception("not a std::bool");
			}
			return basic != 0;
		}
		set {
			basicType = XType.BOOL;
			obj = null;
			basic = value ? (ulong)1 : (ulong)0;
		}
	}

	/*
	 * Get/set the value as an int.
	 */
	internal int Int {
		get {
			if (basicType != XType.INT) {
				throw new Exception("not a std::int");
			}
			return (int)basic;
		}
		set {
			basicType = XType.INT;
			obj = null;
			basic = (ulong)value;
		}
	}

	/*
	 * Get/set the value as a C# string.
	 */
	internal string String {
		get {
			/*
			XString xs = obj as XString;
			if (obj == null) {
				throw new Exception("not a std::string");
			}
			return xs.String;
			*/

			/*
			 * Only a an (u8 array) can become a C# String.
			 * The decoder enforces UTF-8.
			 */
			XArrayGeneric xag = obj as XArrayGeneric;
			if (xag == null) {
				throw new Exception("not a std::string");
			}
			return xag.String;
		}
		set {
			byte[] buf = Encoding.UTF8.GetBytes(value);
			XBuffer xb = new XBufferU8(buf);
			XArrayGeneric xag = new XArrayGeneric(XType.ARRAY_U8);
			xag.Init(xb, 0, buf.Length);
			basicType = null;
			obj = xag;
			basic = 0;
		}
	}

	/*
	 * Get the value as an XString.
	 */
	/* obsolete
	internal XString XString {
		get {
			XString xs = obj as XString;
			if (obj == null) {
				throw new Exception("not a std::string");
			}
			return xs;
		}
		set {
			basicType = null;
			obj = value;
			basic = 0;
		}
	}
	*/

	/*
	 * Get the value as a type instance.
	 */
	internal XType XTypeInstance {
		get {
			XType xt = obj as XType;
			if (xt == null) {
				throw new Exception("not a std::type");
			}
			return xt;
		}
		set {
			basicType = null;
			obj = value;
			basic = 0;
		}
	}

	/*
	 * Get the value as a non-basic object instance.
	 */
	internal XObject XObject {
		get {
			XObject xo = obj as XObject;
			if (xo == null) {
				throw new Exception("not a non-basic object instance");
			}
			return xo;
		}
		set {
			basicType = null;
			obj = value;
			basic = 0;
		}
	}

	/*
	 * Get the value as a generic object instance.
	 */
	internal XObjectGen XObjectGen {
		get {
			XObjectGen xo = obj as XObjectGen;
			if (xo == null) {
				throw new Exception("not a generic object instance");
			}
			return xo;
		}
		set {
			basicType = null;
			obj = value;
			basic = 0;
		}
	}

	/*
	 * Clear/Initialize the value with the provided intended type
	 * (if the type is boolean or an integer, then the value is set
	 * to false or zero; otherwise, it is uninitialized). Note: type
	 * std::int clears to "uninitialized", not zero.
	 */
	internal void Clear(XType xt)
	{
		if (xt == XType.INT) {
			basicType = null;
		} else if (xt.IsBasic) {
			basicType = xt;
		} else {
			basicType = null;
		}
		obj = null;
		basic = 0;
	}

	/*
	 * Clear this value to force it to uninitialize state.
	 */
	internal void Clear()
	{
		basicType = null;
		obj = null;
		basic = 0;
	}

	internal static XValue MakeMarker(int val)
	{
		return new XValue(null, ((ulong)1 << 32) | (ulong)val);
	}

	/*
	 * If this value is a stack marker, the embedded integer is
	 * returned; otherwise, Int32.MinValue is returned.
	 */
	internal int GetMarkerContents()
	{
		if (basicType == null && obj == null && (basic >> 32) != 0) {
			return (int)basic;
		} else {
			return Int32.MinValue;
		}
	}

	/*
	 * The string representation of a value is for debug purposes.
	 */
	public override string ToString()
	{
		if (IsMarker) {
			return string.Format("<marker:{0}>", (int)basic);
		}
		if (IsUninitialized) {
			return "<uninitialized>";
		}
		if (basicType != null) {
			return basicType.ToString(basic);
		} else {
			return obj.ToString();
		}
	}

	/*
	 * A pre-allocated array of no value.
	 */
	internal static XValue[] ZERO = new XValue[0];

	/*
	 * Create a value of type std::int.
	 */
	internal static XValue MakeInt(int x)
	{
		return new XValue(XType.INT, (ulong)x);
	}

	public static implicit operator XValue(bool x)
	{
		return new XValue(XType.BOOL, x ? (ulong)1 : (ulong)0);
	}

	public static implicit operator XValue(byte x)
	{
		return new XValue(XType.U8, (ulong)x);
	}

	public static implicit operator XValue(ushort x)
	{
		return new XValue(XType.U16, (ulong)x);
	}

	public static implicit operator XValue(uint x)
	{
		return new XValue(XType.U32, (ulong)x);
	}

	public static implicit operator XValue(ulong x)
	{
		return new XValue(XType.U64, (ulong)x);
	}

	public static implicit operator XValue(sbyte x)
	{
		return new XValue(XType.I8, (ulong)x);
	}

	public static implicit operator XValue(short x)
	{
		return new XValue(XType.I16, (ulong)x);
	}

	public static implicit operator XValue(int x)
	{
		return new XValue(XType.I32, (ulong)x);
	}

	public static implicit operator XValue(long x)
	{
		return new XValue(XType.I64, (ulong)x);
	}

	public static implicit operator bool(XValue xv)
	{
		if (xv.basicType != XType.BOOL) {
			throw new Exception("not a std::bool");
		}
		return xv.basic != 0;
	}

	public static implicit operator byte(XValue xv)
	{
		if (xv.basicType != XType.U8) {
			throw new Exception("not a std::u8");
		}
		return (byte)xv.basic;
	}

	public static implicit operator ushort(XValue xv)
	{
		if (xv.basicType != XType.U16) {
			throw new Exception("not a std::u16");
		}
		return (ushort)xv.basic;
	}

	public static implicit operator uint(XValue xv)
	{
		if (xv.basicType != XType.U32) {
			throw new Exception("not a std::u32");
		}
		return (uint)xv.basic;
	}

	public static implicit operator ulong(XValue xv)
	{
		if (xv.basicType != XType.U64) {
			throw new Exception("not a std::u64");
		}
		return xv.basic;
	}

	public static implicit operator sbyte(XValue xv)
	{
		if (xv.basicType != XType.I8) {
			throw new Exception("not a std::i8");
		}
		return (sbyte)xv.basic;
	}

	public static implicit operator short(XValue xv)
	{
		if (xv.basicType != XType.I16) {
			throw new Exception("not a std::i16");
		}
		return (short)xv.basic;
	}

	public static implicit operator int(XValue xv)
	{
		if (xv.basicType != XType.I32) {
			throw new Exception("not a std::i32");
		}
		return (int)xv.basic;
	}

	public static implicit operator long(XValue xv)
	{
		if (xv.basicType != XType.I64) {
			throw new Exception("not a std::i64");
		}
		return (long)xv.basic;
	}

	public static bool operator ==(XValue xv1, XValue xv2)
	{
		return object.ReferenceEquals(xv1.basicType, xv2.basicType)
			&& object.ReferenceEquals(xv1.obj, xv2.obj)
			&& xv1.basic == xv2.basic;
	}

	public static bool operator !=(XValue xv1, XValue xv2)
	{
		return !(xv1 == xv2);
	}

	public override bool Equals(object obj)
	{
		if (obj is XValue) {
			return this == (XValue)obj;
		} else {
			return false;
		}
	}

	public override int GetHashCode()
	{
		uint hc = (uint)basic;
		if (basicType != null) {
			hc = (hc << 5) | (hc >> 27);
			hc += (uint)RuntimeHelpers.GetHashCode(basicType);
		}
		if (obj != null) {
			hc = (hc << 5) | (hc >> 27);
			hc += (uint)RuntimeHelpers.GetHashCode(obj);
		}
		return (int)hc;
	}
}
