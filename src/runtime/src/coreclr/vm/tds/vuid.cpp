// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// VUID.CPP
//
// TypeDriver System (TDS) - Virtual Object Unique Identifier Implementation

#include "common.h"
#include "vuid.h"

#ifdef _WIN32
#include <windows.h>
#else
#include <time.h>
#include <sys/time.h>
#endif

namespace TDS
{
    //=========================================================================
    // Platform-specific timestamp (milliseconds since Unix epoch)
    //=========================================================================
    static uint64_t GetCurrentTimestampMs()
    {
#ifdef _WIN32
        FILETIME ft;
        GetSystemTimeAsFileTime(&ft);
        // FILETIME is 100-nanosecond intervals since Jan 1, 1601
        // Convert to milliseconds since Jan 1, 1970
        uint64_t time100ns = ((uint64_t)ft.dwHighDateTime << 32) | ft.dwLowDateTime;
        // Subtract Windows epoch to Unix epoch (11644473600 seconds)
        uint64_t unixTime100ns = time100ns - 116444736000000000ULL;
        return unixTime100ns / 10000;  // Convert to milliseconds
#else
        struct timeval tv;
        gettimeofday(&tv, nullptr);
        return (uint64_t)tv.tv_sec * 1000 + (uint64_t)tv.tv_usec / 1000;
#endif
    }

    //=========================================================================
    // Thread-local random state for VUID generation
    //=========================================================================
    struct RandomState
    {
        uint64_t state[2];
        bool initialized;

        RandomState() : initialized(false) { state[0] = state[1] = 0; }

        void Initialize()
        {
            if (initialized) return;

            // Seed with timestamp and address entropy
            uint64_t seed = GetCurrentTimestampMs();
            seed ^= (uint64_t)(uintptr_t)this;
            seed ^= (uint64_t)(uintptr_t)&seed;

#ifdef _WIN32
            // Additional entropy from QueryPerformanceCounter
            LARGE_INTEGER perf;
            QueryPerformanceCounter(&perf);
            seed ^= (uint64_t)perf.QuadPart;
#endif

            // Initialize xorshift128+ state
            state[0] = seed;
            state[1] = seed ^ 0x9E3779B97F4A7C15ULL;

            // Warm up the generator
            for (int i = 0; i < 20; i++)
            {
                Next();
            }

            initialized = true;
        }

        uint64_t Next()
        {
            // xorshift128+ algorithm
            uint64_t s1 = state[0];
            uint64_t s0 = state[1];
            state[0] = s0;
            s1 ^= s1 << 23;
            state[1] = s1 ^ s0 ^ (s1 >> 18) ^ (s0 >> 5);
            return state[1] + s0;
        }
    };

#ifdef _MSC_VER
    __declspec(thread) static RandomState t_randomState;
#else
    __thread static RandomState t_randomState;
#endif

    //=========================================================================
    // VUID generation - UUID v7 format
    //=========================================================================
    VUID GenerateVUID()
    {
        CONTRACTL
        {
            NOTHROW;
            GC_NOTRIGGER;
            MODE_ANY;
        }
        CONTRACTL_END;

        // Initialize thread-local random state if needed
        t_randomState.Initialize();

        // Get current timestamp in milliseconds
        uint64_t timestamp = GetCurrentTimestampMs();

        // Get random bits
        uint64_t rand1 = t_randomState.Next();
        uint64_t rand2 = t_randomState.Next();

        VUID vuid;

        // UUID v7 format:
        // High 64 bits: timestamp (48 bits) + version (4 bits) + random (12 bits)
        vuid.hi = ((timestamp & 0xFFFFFFFFFFFFULL) << 16) |  // Timestamp in high 48 bits
                  (0x7ULL << 12) |                            // Version 7 in bits 48-51
                  (rand1 & 0x0FFFULL);                        // Random in bits 52-63

        // Low 64 bits: variant (2 bits) + random (62 bits)
        vuid.lo = (0x2ULL << 62) |                            // Variant 10 in bits 64-65
                  (rand2 & 0x3FFFFFFFFFFFFFFFULL);            // Random in remaining bits

        return vuid;
    }

    //=========================================================================
    // Serialization - Big-endian for lexicographic sorting in storage
    //=========================================================================
    void VUIDToBytes(const VUID& vuid, uint8_t* buffer)
    {
        LIMITED_METHOD_CONTRACT;

        // Big-endian encoding for sortability
        buffer[0] = (uint8_t)(vuid.hi >> 56);
        buffer[1] = (uint8_t)(vuid.hi >> 48);
        buffer[2] = (uint8_t)(vuid.hi >> 40);
        buffer[3] = (uint8_t)(vuid.hi >> 32);
        buffer[4] = (uint8_t)(vuid.hi >> 24);
        buffer[5] = (uint8_t)(vuid.hi >> 16);
        buffer[6] = (uint8_t)(vuid.hi >> 8);
        buffer[7] = (uint8_t)(vuid.hi);

        buffer[8] = (uint8_t)(vuid.lo >> 56);
        buffer[9] = (uint8_t)(vuid.lo >> 48);
        buffer[10] = (uint8_t)(vuid.lo >> 40);
        buffer[11] = (uint8_t)(vuid.lo >> 32);
        buffer[12] = (uint8_t)(vuid.lo >> 24);
        buffer[13] = (uint8_t)(vuid.lo >> 16);
        buffer[14] = (uint8_t)(vuid.lo >> 8);
        buffer[15] = (uint8_t)(vuid.lo);
    }

    VUID VUIDFromBytes(const uint8_t* buffer)
    {
        LIMITED_METHOD_CONTRACT;

        VUID vuid;

        vuid.hi = ((uint64_t)buffer[0] << 56) |
                  ((uint64_t)buffer[1] << 48) |
                  ((uint64_t)buffer[2] << 40) |
                  ((uint64_t)buffer[3] << 32) |
                  ((uint64_t)buffer[4] << 24) |
                  ((uint64_t)buffer[5] << 16) |
                  ((uint64_t)buffer[6] << 8) |
                  ((uint64_t)buffer[7]);

        vuid.lo = ((uint64_t)buffer[8] << 56) |
                  ((uint64_t)buffer[9] << 48) |
                  ((uint64_t)buffer[10] << 40) |
                  ((uint64_t)buffer[11] << 32) |
                  ((uint64_t)buffer[12] << 24) |
                  ((uint64_t)buffer[13] << 16) |
                  ((uint64_t)buffer[14] << 8) |
                  ((uint64_t)buffer[15]);

        return vuid;
    }

    //=========================================================================
    // String conversion
    //=========================================================================
    void VUIDToString(const VUID& vuid, char* buffer, size_t bufferLen)
    {
        LIMITED_METHOD_CONTRACT;

        // Standard UUID format: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx (36 chars + null)
        if (bufferLen < 37)
        {
            if (bufferLen > 0) buffer[0] = '\0';
            return;
        }

        uint8_t bytes[16];
        VUIDToBytes(vuid, bytes);

        sprintf_s(buffer, bufferLen,
            "%02x%02x%02x%02x-%02x%02x-%02x%02x-%02x%02x-%02x%02x%02x%02x%02x%02x",
            bytes[0], bytes[1], bytes[2], bytes[3],
            bytes[4], bytes[5],
            bytes[6], bytes[7],
            bytes[8], bytes[9],
            bytes[10], bytes[11], bytes[12], bytes[13], bytes[14], bytes[15]);
    }

    // Helper to parse hex digit
    static int ParseHexDigit(char c)
    {
        if (c >= '0' && c <= '9') return c - '0';
        if (c >= 'a' && c <= 'f') return c - 'a' + 10;
        if (c >= 'A' && c <= 'F') return c - 'A' + 10;
        return -1;
    }

    VUID VUIDFromString(const char* str)
    {
        LIMITED_METHOD_CONTRACT;

        if (str == nullptr) return VUID::Empty();

        // Expected format: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx (36 chars)
        // Or compact: xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx (32 chars)

        uint8_t bytes[16];
        int byteIndex = 0;

        for (int i = 0; str[i] != '\0' && byteIndex < 16; i++)
        {
            // Skip dashes
            if (str[i] == '-') continue;

            int hi = ParseHexDigit(str[i]);
            if (hi < 0) return VUID::Empty();

            i++;
            if (str[i] == '\0') return VUID::Empty();

            int lo = ParseHexDigit(str[i]);
            if (lo < 0) return VUID::Empty();

            bytes[byteIndex++] = (uint8_t)((hi << 4) | lo);
        }

        if (byteIndex != 16) return VUID::Empty();

        return VUIDFromBytes(bytes);
    }

} // namespace TDS
