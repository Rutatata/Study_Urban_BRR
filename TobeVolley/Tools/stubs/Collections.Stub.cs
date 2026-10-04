// STUB (compile-check only): subset of Unity.Collections (FixedStringNBytes). Not the real package.
using System;
namespace Unity.Collections
{
    public interface INativeList<T> { int Length { get; set; } int Capacity { get; set; } bool IsEmpty { get; } }
    public interface IUTF8Bytes { }

    public struct FixedString32Bytes : INativeList<byte>, IUTF8Bytes, IEquatable<FixedString32Bytes>, IComparable<FixedString32Bytes>, IComparable, IConvertible
    {
        string s;
        public int Length { get => s?.Length ?? 0; set { } } public int Capacity { get => 29; set { } } public bool IsEmpty => Length == 0;
        public FixedString32Bytes(string v) { s = v; }
        public static implicit operator FixedString32Bytes(string v) => new FixedString32Bytes(v);
        public static implicit operator string(FixedString32Bytes v) => v.s ?? "";
        public override string ToString() => s ?? "";
        public bool Equals(FixedString32Bytes o) => s == o.s; public int CompareTo(FixedString32Bytes o) => string.CompareOrdinal(s, o.s); public int CompareTo(object o) => 0;
        public override bool Equals(object o) => o is FixedString32Bytes f && Equals(f); public override int GetHashCode() => s?.GetHashCode() ?? 0;
        public static bool operator ==(FixedString32Bytes a, FixedString32Bytes b) => a.Equals(b); public static bool operator !=(FixedString32Bytes a, FixedString32Bytes b) => !a.Equals(b);
        TypeCode IConvertible.GetTypeCode() => TypeCode.Object;
        bool IConvertible.ToBoolean(IFormatProvider p) => false; byte IConvertible.ToByte(IFormatProvider p) => 0; char IConvertible.ToChar(IFormatProvider p) => ' '; DateTime IConvertible.ToDateTime(IFormatProvider p) => default; decimal IConvertible.ToDecimal(IFormatProvider p) => 0; double IConvertible.ToDouble(IFormatProvider p) => 0; short IConvertible.ToInt16(IFormatProvider p) => 0; int IConvertible.ToInt32(IFormatProvider p) => 0; long IConvertible.ToInt64(IFormatProvider p) => 0; sbyte IConvertible.ToSByte(IFormatProvider p) => 0; float IConvertible.ToSingle(IFormatProvider p) => 0; string IConvertible.ToString(IFormatProvider p) => ToString(); object IConvertible.ToType(Type t, IFormatProvider p) => null; ushort IConvertible.ToUInt16(IFormatProvider p) => 0; uint IConvertible.ToUInt32(IFormatProvider p) => 0; ulong IConvertible.ToUInt64(IFormatProvider p) => 0;
    }
    public struct FixedString64Bytes : INativeList<byte>, IUTF8Bytes, IEquatable<FixedString64Bytes>, IComparable<FixedString64Bytes>, IComparable, IConvertible
    {
        string s;
        public int Length { get => s?.Length ?? 0; set { } } public int Capacity { get => 61; set { } } public bool IsEmpty => Length == 0;
        public FixedString64Bytes(string v) { s = v; }
        public static implicit operator FixedString64Bytes(string v) => new FixedString64Bytes(v);
        public static implicit operator string(FixedString64Bytes v) => v.s ?? "";
        public override string ToString() => s ?? "";
        public bool Equals(FixedString64Bytes o) => s == o.s; public int CompareTo(FixedString64Bytes o) => string.CompareOrdinal(s, o.s); public int CompareTo(object o) => 0;
        public override bool Equals(object o) => o is FixedString64Bytes f && Equals(f); public override int GetHashCode() => s?.GetHashCode() ?? 0;
        public static bool operator ==(FixedString64Bytes a, FixedString64Bytes b) => a.Equals(b); public static bool operator !=(FixedString64Bytes a, FixedString64Bytes b) => !a.Equals(b);
        TypeCode IConvertible.GetTypeCode() => TypeCode.Object;
        bool IConvertible.ToBoolean(IFormatProvider p) => false; byte IConvertible.ToByte(IFormatProvider p) => 0; char IConvertible.ToChar(IFormatProvider p) => ' '; DateTime IConvertible.ToDateTime(IFormatProvider p) => default; decimal IConvertible.ToDecimal(IFormatProvider p) => 0; double IConvertible.ToDouble(IFormatProvider p) => 0; short IConvertible.ToInt16(IFormatProvider p) => 0; int IConvertible.ToInt32(IFormatProvider p) => 0; long IConvertible.ToInt64(IFormatProvider p) => 0; sbyte IConvertible.ToSByte(IFormatProvider p) => 0; float IConvertible.ToSingle(IFormatProvider p) => 0; string IConvertible.ToString(IFormatProvider p) => ToString(); object IConvertible.ToType(Type t, IFormatProvider p) => null; ushort IConvertible.ToUInt16(IFormatProvider p) => 0; uint IConvertible.ToUInt32(IFormatProvider p) => 0; ulong IConvertible.ToUInt64(IFormatProvider p) => 0;
    }
    public struct FixedString128Bytes : INativeList<byte>, IUTF8Bytes, IEquatable<FixedString128Bytes>, IComparable<FixedString128Bytes>, IComparable, IConvertible
    {
        string s;
        public int Length { get => s?.Length ?? 0; set { } } public int Capacity { get => 125; set { } } public bool IsEmpty => Length == 0;
        public FixedString128Bytes(string v) { s = v; }
        public static implicit operator FixedString128Bytes(string v) => new FixedString128Bytes(v);
        public static implicit operator string(FixedString128Bytes v) => v.s ?? "";
        public override string ToString() => s ?? "";
        public bool Equals(FixedString128Bytes o) => s == o.s; public int CompareTo(FixedString128Bytes o) => string.CompareOrdinal(s, o.s); public int CompareTo(object o) => 0;
        public override bool Equals(object o) => o is FixedString128Bytes f && Equals(f); public override int GetHashCode() => s?.GetHashCode() ?? 0;
        public static bool operator ==(FixedString128Bytes a, FixedString128Bytes b) => a.Equals(b); public static bool operator !=(FixedString128Bytes a, FixedString128Bytes b) => !a.Equals(b);
        TypeCode IConvertible.GetTypeCode() => TypeCode.Object;
        bool IConvertible.ToBoolean(IFormatProvider p) => false; byte IConvertible.ToByte(IFormatProvider p) => 0; char IConvertible.ToChar(IFormatProvider p) => ' '; DateTime IConvertible.ToDateTime(IFormatProvider p) => default; decimal IConvertible.ToDecimal(IFormatProvider p) => 0; double IConvertible.ToDouble(IFormatProvider p) => 0; short IConvertible.ToInt16(IFormatProvider p) => 0; int IConvertible.ToInt32(IFormatProvider p) => 0; long IConvertible.ToInt64(IFormatProvider p) => 0; sbyte IConvertible.ToSByte(IFormatProvider p) => 0; float IConvertible.ToSingle(IFormatProvider p) => 0; string IConvertible.ToString(IFormatProvider p) => ToString(); object IConvertible.ToType(Type t, IFormatProvider p) => null; ushort IConvertible.ToUInt16(IFormatProvider p) => 0; uint IConvertible.ToUInt32(IFormatProvider p) => 0; ulong IConvertible.ToUInt64(IFormatProvider p) => 0;
    }
    public struct FixedString512Bytes : INativeList<byte>, IUTF8Bytes, IEquatable<FixedString512Bytes>, IComparable<FixedString512Bytes>, IComparable, IConvertible
    {
        string s;
        public int Length { get => s?.Length ?? 0; set { } } public int Capacity { get => 509; set { } } public bool IsEmpty => Length == 0;
        public FixedString512Bytes(string v) { s = v; }
        public static implicit operator FixedString512Bytes(string v) => new FixedString512Bytes(v);
        public static implicit operator string(FixedString512Bytes v) => v.s ?? "";
        public override string ToString() => s ?? "";
        public bool Equals(FixedString512Bytes o) => s == o.s; public int CompareTo(FixedString512Bytes o) => string.CompareOrdinal(s, o.s); public int CompareTo(object o) => 0;
        public override bool Equals(object o) => o is FixedString512Bytes f && Equals(f); public override int GetHashCode() => s?.GetHashCode() ?? 0;
        public static bool operator ==(FixedString512Bytes a, FixedString512Bytes b) => a.Equals(b); public static bool operator !=(FixedString512Bytes a, FixedString512Bytes b) => !a.Equals(b);
        TypeCode IConvertible.GetTypeCode() => TypeCode.Object;
        bool IConvertible.ToBoolean(IFormatProvider p) => false; byte IConvertible.ToByte(IFormatProvider p) => 0; char IConvertible.ToChar(IFormatProvider p) => ' '; DateTime IConvertible.ToDateTime(IFormatProvider p) => default; decimal IConvertible.ToDecimal(IFormatProvider p) => 0; double IConvertible.ToDouble(IFormatProvider p) => 0; short IConvertible.ToInt16(IFormatProvider p) => 0; int IConvertible.ToInt32(IFormatProvider p) => 0; long IConvertible.ToInt64(IFormatProvider p) => 0; sbyte IConvertible.ToSByte(IFormatProvider p) => 0; float IConvertible.ToSingle(IFormatProvider p) => 0; string IConvertible.ToString(IFormatProvider p) => ToString(); object IConvertible.ToType(Type t, IFormatProvider p) => null; ushort IConvertible.ToUInt16(IFormatProvider p) => 0; uint IConvertible.ToUInt32(IFormatProvider p) => 0; ulong IConvertible.ToUInt64(IFormatProvider p) => 0;
    }
    public struct FixedString4096Bytes : INativeList<byte>, IUTF8Bytes, IEquatable<FixedString4096Bytes>, IComparable<FixedString4096Bytes>, IComparable, IConvertible
    {
        string s;
        public int Length { get => s?.Length ?? 0; set { } } public int Capacity { get => 4093; set { } } public bool IsEmpty => Length == 0;
        public FixedString4096Bytes(string v) { s = v; }
        public static implicit operator FixedString4096Bytes(string v) => new FixedString4096Bytes(v);
        public static implicit operator string(FixedString4096Bytes v) => v.s ?? "";
        public override string ToString() => s ?? "";
        public bool Equals(FixedString4096Bytes o) => s == o.s; public int CompareTo(FixedString4096Bytes o) => string.CompareOrdinal(s, o.s); public int CompareTo(object o) => 0;
        public override bool Equals(object o) => o is FixedString4096Bytes f && Equals(f); public override int GetHashCode() => s?.GetHashCode() ?? 0;
        public static bool operator ==(FixedString4096Bytes a, FixedString4096Bytes b) => a.Equals(b); public static bool operator !=(FixedString4096Bytes a, FixedString4096Bytes b) => !a.Equals(b);
        TypeCode IConvertible.GetTypeCode() => TypeCode.Object;
        bool IConvertible.ToBoolean(IFormatProvider p) => false; byte IConvertible.ToByte(IFormatProvider p) => 0; char IConvertible.ToChar(IFormatProvider p) => ' '; DateTime IConvertible.ToDateTime(IFormatProvider p) => default; decimal IConvertible.ToDecimal(IFormatProvider p) => 0; double IConvertible.ToDouble(IFormatProvider p) => 0; short IConvertible.ToInt16(IFormatProvider p) => 0; int IConvertible.ToInt32(IFormatProvider p) => 0; long IConvertible.ToInt64(IFormatProvider p) => 0; sbyte IConvertible.ToSByte(IFormatProvider p) => 0; float IConvertible.ToSingle(IFormatProvider p) => 0; string IConvertible.ToString(IFormatProvider p) => ToString(); object IConvertible.ToType(Type t, IFormatProvider p) => null; ushort IConvertible.ToUInt16(IFormatProvider p) => 0; uint IConvertible.ToUInt32(IFormatProvider p) => 0; ulong IConvertible.ToUInt64(IFormatProvider p) => 0;
    }
}
