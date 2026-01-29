// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.OS
{
    /// <summary>
    /// Virtual Object Unique Identifier - UUID v7 format.
    /// Globally unique, time-sortable, survives process restarts.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct VUID : IEquatable<VUID>, IComparable<VUID>
    {
        private readonly ulong _hi;  // Timestamp (48 bits) + version (4 bits) + random (12 bits)
        private readonly ulong _lo;  // Variant (2 bits) + random (62 bits)

        /// <summary>
        /// Check if VUID is empty (all zeros).
        /// </summary>
        public bool IsEmpty => _hi == 0 && _lo == 0;

        /// <summary>
        /// Empty VUID constant.
        /// </summary>
        public static VUID Empty => default;

        /// <summary>
        /// Generate a new UUID v7.
        /// </summary>
        public static VUID New()
        {
            GenerateVUIDInternal(out ulong hi, out ulong lo);
            return new VUID(hi, lo);
        }

        /// <summary>
        /// Create VUID from high and low parts.
        /// </summary>
        private VUID(ulong hi, ulong lo)
        {
            _hi = hi;
            _lo = lo;
        }

        /// <summary>
        /// Create VUID from bytes (big-endian, 16 bytes).
        /// </summary>
        public static VUID FromBytes(ReadOnlySpan<byte> source)
        {
            if (source.Length < 16)
                throw new ArgumentException("Buffer must be at least 16 bytes", nameof(source));

            ulong hi = BinaryPrimitives.ReadUInt64BigEndian(source);
            ulong lo = BinaryPrimitives.ReadUInt64BigEndian(source.Slice(8));
            return new VUID(hi, lo);
        }

        /// <summary>
        /// Write VUID to bytes (big-endian, 16 bytes).
        /// </summary>
        public void WriteBytes(Span<byte> destination)
        {
            if (destination.Length < 16)
                throw new ArgumentException("Buffer must be at least 16 bytes", nameof(destination));

            BinaryPrimitives.WriteUInt64BigEndian(destination, _hi);
            BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(8), _lo);
        }

        /// <summary>
        /// Parse VUID from standard UUID string format.
        /// </summary>
        public static VUID Parse(string s)
        {
            ArgumentNullException.ThrowIfNull(s);

            if (!TryParse(s, out VUID result))
                throw new FormatException("Invalid VUID format");

            return result;
        }

        /// <summary>
        /// Try to parse VUID from standard UUID string format.
        /// </summary>
        public static bool TryParse(string? s, out VUID result)
        {
            result = default;
            if (string.IsNullOrEmpty(s))
                return false;

            // Expected format: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx (36 chars)
            // Or compact: xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx (32 chars)

            Span<byte> bytes = stackalloc byte[16];
            int byteIndex = 0;

            for (int i = 0; i < s.Length && byteIndex < 16; i++)
            {
                char c = s[i];
                if (c == '-') continue;

                int hi = ParseHexDigit(c);
                if (hi < 0) return false;

                i++;
                if (i >= s.Length) return false;

                int lo = ParseHexDigit(s[i]);
                if (lo < 0) return false;

                bytes[byteIndex++] = (byte)((hi << 4) | lo);
            }

            if (byteIndex != 16)
                return false;

            result = FromBytes(bytes);
            return true;
        }

        private static int ParseHexDigit(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }

        /// <summary>
        /// Convert to standard UUID string format.
        /// </summary>
        public override string ToString()
        {
            Span<byte> bytes = stackalloc byte[16];
            WriteBytes(bytes);

            return string.Create(36, bytes.ToArray(), (span, b) =>
            {
                int pos = 0;
                for (int i = 0; i < 16; i++)
                {
                    if (i == 4 || i == 6 || i == 8 || i == 10)
                    {
                        span[pos++] = '-';
                    }
                    span[pos++] = GetHexChar(b[i] >> 4);
                    span[pos++] = GetHexChar(b[i] & 0xF);
                }
            });
        }

        private static char GetHexChar(int value) =>
            (char)(value < 10 ? '0' + value : 'a' + value - 10);

        #region Equality and Comparison

        public bool Equals(VUID other) => _hi == other._hi && _lo == other._lo;

        public override bool Equals(object? obj) => obj is VUID v && Equals(v);

        public override int GetHashCode() => HashCode.Combine(_hi, _lo);

        public int CompareTo(VUID other)
        {
            int cmp = _hi.CompareTo(other._hi);
            return cmp != 0 ? cmp : _lo.CompareTo(other._lo);
        }

        public static bool operator ==(VUID left, VUID right) => left.Equals(right);
        public static bool operator !=(VUID left, VUID right) => !left.Equals(right);
        public static bool operator <(VUID left, VUID right) => left.CompareTo(right) < 0;
        public static bool operator <=(VUID left, VUID right) => left.CompareTo(right) <= 0;
        public static bool operator >(VUID left, VUID right) => left.CompareTo(right) > 0;
        public static bool operator >=(VUID left, VUID right) => left.CompareTo(right) >= 0;

        #endregion

        #region Native Interop

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSNative_GenerateVUID")]
        private static partial void GenerateVUIDInternal(out ulong hi, out ulong lo);

        #endregion
    }
}
