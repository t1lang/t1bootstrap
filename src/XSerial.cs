using System;

/*
 * An XSerial instance is an object that can be included into generated
 * code as a statically allocated constant object.
 */

abstract class XSerial : IComparable<XSerial> {

	static ulong currentSerial = 0;

	internal ulong Serial {
		get;
		private set;
	}

	internal XSerial()
	{
		Serial = currentSerial ++;
	}

	public int CompareTo(XSerial other)
	{
		return Serial.CompareTo(other.Serial);
	}

	public override bool Equals(object other)
	{
		return object.ReferenceEquals(this, other);
	}

	public override int GetHashCode()
	{
		return (int)Serial;
	}
}
