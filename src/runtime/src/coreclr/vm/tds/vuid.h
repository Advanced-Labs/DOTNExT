// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// VUID.H
//
// TypeDriver System (TDS) - Virtual Object Unique Identifier
// UUID v7 format: 128 bits, time-sortable, globally unique

#ifndef _TDS_VUID_H_
#define _TDS_VUID_H_

#include "common.h"

namespace TDS
{
    //=========================================================================
    // VUID - Virtual Object Unique Identifier
    //
    // UUID v7 format (RFC draft-peabody-dispatch-new-uuid-format):
    // Bits 0-47:   Unix timestamp in milliseconds
    // Bits 48-51:  Version (0111 = 7)
    // Bits 52-63:  Random
    // Bits 64-65:  Variant (10)
    // Bits 66-127: Random
    //
    // Properties:
    // - Globally unique across the Internet
    // - Time-sortable (chronological ordering)
    // - 128-bit for storage as Voron key
    //=========================================================================
    struct VUID
    {
        uint64_t hi;  // Timestamp (48 bits) + version (4 bits) + random (12 bits)
        uint64_t lo;  // Variant (2 bits) + random (62 bits)

        // Check if VUID is valid (non-empty)
        bool IsValid() const { return hi != 0 || lo != 0; }

        // Check if VUID is empty
        bool IsEmpty() const { return hi == 0 && lo == 0; }

        // Comparison operators
        bool operator==(const VUID& other) const
        {
            return hi == other.hi && lo == other.lo;
        }

        bool operator!=(const VUID& other) const
        {
            return !(*this == other);
        }

        bool operator<(const VUID& other) const
        {
            return hi < other.hi || (hi == other.hi && lo < other.lo);
        }

        bool operator<=(const VUID& other) const
        {
            return *this < other || *this == other;
        }

        bool operator>(const VUID& other) const
        {
            return other < *this;
        }

        bool operator>=(const VUID& other) const
        {
            return other <= *this;
        }

        // Create an empty VUID
        static VUID Empty() { return VUID{0, 0}; }
    };

    //=========================================================================
    // VUID generation and utilities
    //=========================================================================

    // Generate a new UUID v7
    VUID GenerateVUID();

    // Serialize VUID to bytes (big-endian, 16 bytes)
    // buffer must be at least 16 bytes
    void VUIDToBytes(const VUID& vuid, uint8_t* buffer);

    // Deserialize VUID from bytes (big-endian, 16 bytes)
    VUID VUIDFromBytes(const uint8_t* buffer);

    // Convert to standard UUID string format (xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx)
    // buffer must be at least 37 bytes (36 chars + null terminator)
    void VUIDToString(const VUID& vuid, char* buffer, size_t bufferLen);

    // Parse VUID from string
    // Returns Empty VUID on parse failure
    VUID VUIDFromString(const char* str);

    //=========================================================================
    // VUID hash for use in hash tables
    //=========================================================================
    inline size_t VUIDHash(const VUID& vuid)
    {
        // Combine hi and lo with XOR and bit mixing
        return (size_t)(vuid.hi ^ (vuid.lo * 0x9E3779B97F4A7C15ULL));
    }

} // namespace TDS

#endif // _TDS_VUID_H_
