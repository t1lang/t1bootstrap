using System;
using System.Collections.Generic;

/*
 * Size / alignment rules:
 *
 *  - A pointer or a restricted type value have a size which is a power of 2,
 *    and an alignment requirement which is a power of 2.
 *
 *  - Not element has an alignment requirement greater than its size.
 *
 *  - bool, i8 and u8 have size 1 and alignment 1.
 *
 *  - i16 and u16 have size 2 and alignment 2.
 *
 *  - i32 and u32 have size 4 and alignment 2 or 4.
 *
 *  - i64 and u64 have size 8 and alignment 2, 4 or 8.
 *
 *  - Pointers have size 2, 4 or 8, and alignment at least 2. The
 *    alignment of a pointer may be less than its size; it may also
 *    differ from the alignment of restricted integer types of the same
 *    size as pointers.
 *
 * The three main classes are:
 *
 *  - P32: pointers are 32-bit, and every type has the same alignment as
 *    its size. This is the case of most 32-bit architectures.
 *
 *  - I386: special case for 32-bit x86: same as P32, except for 64-bit
 *    integer types (i64 and u64) which have 4-byte alignment.
 *
 *  - P64: pointers are 64-bit, and every type has the same alignment as
 *    its size. This is the case of most 64-bit architectures.
 */

class GAlign : IComparable<GAlign> {

	/*
	 * 32-bit pointers, 32-bit alignment for 32-bit values, 64-bit
	 * alignment for 64-bit values.
	 */
	internal static GAlign P32 = new GAlign(4, 4, 4, 8);

	/*
	 * 32-bit pointers, 32-bit alignment for 32-bit and 64-bit values.
	 */
	internal static GAlign I386 = new GAlign(4, 4, 4, 4);

	/*
	 * 64-bit pointers, 32-bit alignment for 32-bit values, 64-bit
	 * alignment for 64-bit values.
	 */
	internal static GAlign P64 = new GAlign(8, 8, 4, 8);

	SortedDictionary<XType, int> sizeRef;
	SortedDictionary<XType, int> alignRef;
	int sizePtr, alignPtr;
	int carac;

	GAlign(int sizePtr, int alignPtr, int align32, int align64)
	{
		int logSizePtr = Log2(sizePtr);
		int logAlignPtr = Log2(alignPtr);
		int logAlign32 = Log2(align32);
		int logAlign64 = Log2(align64);
		if (alignPtr < 2 || alignPtr > sizePtr) {
			throw new Exception(string.Format("invalid pointer alignment (size={0}, align={1})", sizePtr, alignPtr));
		}
		if (align32 < 2 || align32 > 4) {
			throw new Exception(string.Format("invalid 32-bit word alignment ({0})", align32));
		}
		if (align64 < 2 || align64 > 8) {
			throw new Exception(string.Format("invalid 64-bit word alignment ({0})", align64));
		}
		carac = (logSizePtr << 15) | (logAlignPtr << 10)
			| (logAlign32 << 5) | logAlign64;

		sizeRef = new SortedDictionary<XType, int>();
		sizeRef[XType.BOOL] = 1;
		sizeRef[XType.U8] = 1;
		sizeRef[XType.U16] = 2;
		sizeRef[XType.U32] = 4;
		sizeRef[XType.U64] = 8;
		sizeRef[XType.I8] = 1;
		sizeRef[XType.I16] = 2;
		sizeRef[XType.I32] = 4;
		sizeRef[XType.I64] = 8;
		this.sizePtr = sizePtr;
		this.alignPtr = alignPtr;
		alignRef = new SortedDictionary<XType, int>();
		alignRef[XType.BOOL] = 1;
		alignRef[XType.U8] = 1;
		alignRef[XType.U16] = 2;
		alignRef[XType.U32] = align32;
		alignRef[XType.U64] = align64;
		alignRef[XType.I8] = 1;
		alignRef[XType.I16] = 2;
		alignRef[XType.I32] = align32;
		alignRef[XType.I64] = align64;
	}

	static int Log2(int x)
	{
		int orig = x;
		int n = 0;
		for (int k = 16; k > 0; k >>= 1) {
			int y = x >> k;
			if (y != 0) {
				n += k;
				x = y;
			}
		}
		if (orig != (1 << n)) {
			throw new Exception(string.Format("{0} is not a power of 2", orig));
		}
		return n;
	}

	internal int SizeRef(XType xt)
	{
		if (xt.IsRestricted) {
			return sizeRef[xt];
		} else {
			return sizePtr;
		}
	}

	internal int AlignRef(XType xt)
	{
		if (xt.IsRestricted) {
			return alignRef[xt];
		} else {
			return alignPtr;
		}
	}

	public int CompareTo(GAlign ga)
	{
		return carac.CompareTo(ga.carac);
	}

	public override bool Equals(object other)
	{
		GAlign ga = other as GAlign;
		if (ga == null) {
			return false;
		} else {
			return carac == ga.carac;
		}
	}

	public override int GetHashCode()
	{
		return carac;
	}
}
