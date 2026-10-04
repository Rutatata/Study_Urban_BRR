// STUB (compile-check only): subset of Unity.Collections (FixedStringNBytes). Not the real package.
// Real ones are unmanaged structs; so are these (UTF-8 bytes in a fixed buffer).
using System;
using System.Text;
namespace Unity.Collections
{
    public interface INativeList<T> { int Length { get; set; } int Capacity { get; set; } bool IsEmpty { get; } }
    public interface IUTF8Bytes { }

    public unsafe struct FixedString32Bytes : INativeList<byte>, IUTF8Bytes, IEquatable<FixedString32Bytes>, IComparable<FixedString32Bytes>
    {
        ushort len; fixed byte buf[30];
        public int Length { get => len; set { len = (ushort)value; } } public int Capacity { get => 29; set { } } public bool IsEmpty => len == 0;
        public FixedString32Bytes(string v) { len = 0; var b = Encoding.UTF8.GetBytes(v ?? ""); int n = Math.Min(b.Length, 29); for (int i = 0; i < n; i++) buf[i] = b[i]; len = (ushort)n; }
        public static implicit operator FixedString32Bytes(string v) => new FixedString32Bytes(v);
        public static implicit operator string(FixedString32Bytes v) => v.ToString();
        public override string ToString() { var b = new byte[len]; for (int i = 0; i < len; i++) b[i] = buf[i]; return Encoding.UTF8.GetString(b); }
        public bool Equals(FixedString32Bytes o) => ToString() == o.ToString(); public int CompareTo(FixedString32Bytes o) => string.CompareOrdinal(ToString(), o.ToString());
        public override bool Equals(object o) => o is FixedString32Bytes f && Equals(f); public override int GetHashCode() => ToString().GetHashCode();
        public static bool operator ==(FixedString32Bytes a, FixedString32Bytes b) => a.Equals(b); public static bool operator !=(FixedString32Bytes a, FixedString32Bytes b) => !a.Equals(b);
    }

    public unsafe struct FixedString64Bytes : INativeList<byte>, IUTF8Bytes, IEquatable<FixedString64Bytes>, IComparable<FixedString64Bytes>
    {
        ushort len; fixed byte buf[62];
        public int Length { get => len; set { len = (ushort)value; } } public int Capacity { get => 61; set { } } public bool IsEmpty => len == 0;
        public FixedString64Bytes(string v) { len = 0; var b = Encoding.UTF8.GetBytes(v ?? ""); int n = Math.Min(b.Length, 61); for (int i = 0; i < n; i++) buf[i] = b[i]; len = (ushort)n; }
        public static implicit operator FixedString64Bytes(string v) => new FixedString64Bytes(v);
        public static implicit operator string(FixedString64Bytes v) => v.ToString();
        public override string ToString() { var b = new byte[len]; for (int i = 0; i < len; i++) b[i] = buf[i]; return Encoding.UTF8.GetString(b); }
        public bool Equals(FixedString64Bytes o) => ToString() == o.ToString(); public int CompareTo(FixedString64Bytes o) => string.CompareOrdinal(ToString(), o.ToString());
        public override bool Equals(object o) => o is FixedString64Bytes f && Equals(f); public override int GetHashCode() => ToString().GetHashCode();
        public static bool operator ==(FixedString64Bytes a, FixedString64Bytes b) => a.Equals(b); public static bool operator !=(FixedString64Bytes a, FixedString64Bytes b) => !a.Equals(b);
    }

    public unsafe struct FixedString128Bytes : INativeList<byte>, IUTF8Bytes, IEquatable<FixedString128Bytes>, IComparable<FixedString128Bytes>
    {
        ushort len; fixed byte buf[126];
        public int Length { get => len; set { len = (ushort)value; } } public int Capacity { get => 125; set { } } public bool IsEmpty => len == 0;
        public FixedString128Bytes(string v) { len = 0; var b = Encoding.UTF8.GetBytes(v ?? ""); int n = Math.Min(b.Length, 125); for (int i = 0; i < n; i++) buf[i] = b[i]; len = (ushort)n; }
        public static implicit operator FixedString128Bytes(string v) => new FixedString128Bytes(v);
        public static implicit operator string(FixedString128Bytes v) => v.ToString();
        public override string ToString() { var b = new byte[len]; for (int i = 0; i < len; i++) b[i] = buf[i]; return Encoding.UTF8.GetString(b); }
        public bool Equals(FixedString128Bytes o) => ToString() == o.ToString(); public int CompareTo(FixedString128Bytes o) => string.CompareOrdinal(ToString(), o.ToString());
        public override bool Equals(object o) => o is FixedString128Bytes f && Equals(f); public override int GetHashCode() => ToString().GetHashCode();
        public static bool operator ==(FixedString128Bytes a, FixedString128Bytes b) => a.Equals(b); public static bool operator !=(FixedString128Bytes a, FixedString128Bytes b) => !a.Equals(b);
    }

    public unsafe struct FixedString512Bytes : INativeList<byte>, IUTF8Bytes, IEquatable<FixedString512Bytes>, IComparable<FixedString512Bytes>
    {
        ushort len; fixed byte buf[510];
        public int Length { get => len; set { len = (ushort)value; } } public int Capacity { get => 509; set { } } public bool IsEmpty => len == 0;
        public FixedString512Bytes(string v) { len = 0; var b = Encoding.UTF8.GetBytes(v ?? ""); int n = Math.Min(b.Length, 509); for (int i = 0; i < n; i++) buf[i] = b[i]; len = (ushort)n; }
        public static implicit operator FixedString512Bytes(string v) => new FixedString512Bytes(v);
        public static implicit operator string(FixedString512Bytes v) => v.ToString();
        public override string ToString() { var b = new byte[len]; for (int i = 0; i < len; i++) b[i] = buf[i]; return Encoding.UTF8.GetString(b); }
        public bool Equals(FixedString512Bytes o) => ToString() == o.ToString(); public int CompareTo(FixedString512Bytes o) => string.CompareOrdinal(ToString(), o.ToString());
        public override bool Equals(object o) => o is FixedString512Bytes f && Equals(f); public override int GetHashCode() => ToString().GetHashCode();
        public static bool operator ==(FixedString512Bytes a, FixedString512Bytes b) => a.Equals(b); public static bool operator !=(FixedString512Bytes a, FixedString512Bytes b) => !a.Equals(b);
    }

    public unsafe struct FixedString4096Bytes : INativeList<byte>, IUTF8Bytes, IEquatable<FixedString4096Bytes>, IComparable<FixedString4096Bytes>
    {
        ushort len; fixed byte buf[4094];
        public int Length { get => len; set { len = (ushort)value; } } public int Capacity { get => 4093; set { } } public bool IsEmpty => len == 0;
        public FixedString4096Bytes(string v) { len = 0; var b = Encoding.UTF8.GetBytes(v ?? ""); int n = Math.Min(b.Length, 4093); for (int i = 0; i < n; i++) buf[i] = b[i]; len = (ushort)n; }
        public static implicit operator FixedString4096Bytes(string v) => new FixedString4096Bytes(v);
        public static implicit operator string(FixedString4096Bytes v) => v.ToString();
        public override string ToString() { var b = new byte[len]; for (int i = 0; i < len; i++) b[i] = buf[i]; return Encoding.UTF8.GetString(b); }
        public bool Equals(FixedString4096Bytes o) => ToString() == o.ToString(); public int CompareTo(FixedString4096Bytes o) => string.CompareOrdinal(ToString(), o.ToString());
        public override bool Equals(object o) => o is FixedString4096Bytes f && Equals(f); public override int GetHashCode() => ToString().GetHashCode();
        public static bool operator ==(FixedString4096Bytes a, FixedString4096Bytes b) => a.Equals(b); public static bool operator !=(FixedString4096Bytes a, FixedString4096Bytes b) => !a.Equals(b);
    }
}
