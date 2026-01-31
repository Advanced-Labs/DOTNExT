# T02: VUID Infrastructure

> **Work Package:** WP2.0 (Infrastructure)
> **Dependencies:** T01 (VContext Enhancement)
> **Estimated Complexity:** Medium
> **Status:** Code Complete - Awaiting TAI Build Verification

---

## Objective

Implement VUID (Virtual Object Unique Identifier) infrastructure for globally unique object identity that survives process restarts.

---

## Background

VUID requirements (from Phase 2 doc):
- Global across the Internet (not cluster-local)
- UUID v7 preferred (128-bit, time-sortable)
- Stored in durable Body layer + optionally cached in activation
- Used as key for storage lookup (Voron tree key)

---

## Implementation

### 1. VUID Structure (Native)

**File:** `src/runtime/src/coreclr/vm/tds/vuid.h` (new)

```cpp
#ifndef _VUID_H_
#define _VUID_H_

#include "common.h"

// VUID - Virtual Object Unique Identifier
// UUID v7 format: 128 bits, time-sortable
struct VUID
{
    uint64_t hi;  // Timestamp + version bits
    uint64_t lo;  // Random bits

    bool IsValid() const { return hi != 0 || lo != 0; }
    bool operator==(const VUID& other) const { return hi == other.hi && lo == other.lo; }
    bool operator!=(const VUID& other) const { return !(*this == other); }
    bool operator<(const VUID& other) const {
        return hi < other.hi || (hi == other.hi && lo < other.lo);
    }

    static VUID Empty() { return VUID{0, 0}; }
};

// VUID generation and utilities
VUID TDS_GenerateVUID();
void TDS_VUIDToBytes(const VUID& vuid, uint8_t* buffer);  // buffer must be 16 bytes
VUID TDS_VUIDFromBytes(const uint8_t* buffer);

// String representation for debugging
void TDS_VUIDToString(const VUID& vuid, char* buffer, size_t bufferLen);

#endif // _VUID_H_
```

### 2. UUID v7 Generation

**File:** `src/runtime/src/coreclr/vm/tds/vuid.cpp` (new)

```cpp
#include "vuid.h"
#include <chrono>
#include <random>

// UUID v7 format (RFC draft):
// Bits 0-47:   Unix timestamp in milliseconds
// Bits 48-51:  Version (0111 = 7)
// Bits 52-63:  Random
// Bit 64-65:   Variant (10)
// Bits 66-127: Random

VUID TDS_GenerateVUID()
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        MODE_ANY;
    }
    CONTRACTL_END;

    // Get Unix timestamp in milliseconds
    auto now = std::chrono::system_clock::now();
    auto ms = std::chrono::duration_cast<std::chrono::milliseconds>(
        now.time_since_epoch()).count();

    // Random bits
    static thread_local std::mt19937_64 rng(std::random_device{}());
    uint64_t rand1 = rng();
    uint64_t rand2 = rng();

    VUID vuid;

    // High 64 bits: timestamp (48 bits) + version (4 bits) + random (12 bits)
    vuid.hi = ((uint64_t)ms << 16) |     // Timestamp in high 48 bits
              (0x7ULL << 12) |            // Version 7 in bits 48-51
              (rand1 & 0x0FFF);           // Random in bits 52-63

    // Low 64 bits: variant (2 bits) + random (62 bits)
    vuid.lo = (0x2ULL << 62) |           // Variant 10 in bits 64-65
              (rand2 & 0x3FFFFFFFFFFFFFFFULL);  // Random in remaining bits

    return vuid;
}

void TDS_VUIDToBytes(const VUID& vuid, uint8_t* buffer)
{
    // Big-endian for sortability in storage
    for (int i = 0; i < 8; i++) {
        buffer[i] = (uint8_t)(vuid.hi >> (56 - i * 8));
    }
    for (int i = 0; i < 8; i++) {
        buffer[8 + i] = (uint8_t)(vuid.lo >> (56 - i * 8));
    }
}

VUID TDS_VUIDFromBytes(const uint8_t* buffer)
{
    VUID vuid;
    vuid.hi = 0;
    vuid.lo = 0;

    for (int i = 0; i < 8; i++) {
        vuid.hi = (vuid.hi << 8) | buffer[i];
    }
    for (int i = 0; i < 8; i++) {
        vuid.lo = (vuid.lo << 8) | buffer[8 + i];
    }

    return vuid;
}

void TDS_VUIDToString(const VUID& vuid, char* buffer, size_t bufferLen)
{
    // Standard UUID format: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
    if (bufferLen < 37) return;

    uint8_t bytes[16];
    TDS_VUIDToBytes(vuid, bytes);

    snprintf(buffer, bufferLen,
        "%02x%02x%02x%02x-%02x%02x-%02x%02x-%02x%02x-%02x%02x%02x%02x%02x%02x",
        bytes[0], bytes[1], bytes[2], bytes[3],
        bytes[4], bytes[5],
        bytes[6], bytes[7],
        bytes[8], bytes[9],
        bytes[10], bytes[11], bytes[12], bytes[13], bytes[14], bytes[15]);
}
```

### 3. VUID Storage in OpsRoot

Extend OpsRootEntry or add side-table for VUID → Object mapping:

**File:** `src/runtime/src/coreclr/vm/tds/opsroottable.h` (modify)

```cpp
struct OpsRootEntry
{
    DWORD syncBlockIndex;   // Key: SyncBlockIndex of the object
    OpsRoot* ops;           // Value: Pointer to OpsRoot dispatch table
    UINT32 generationTag;   // Safety net: validates entry is not stale
    VUID vuid;              // NEW: Object's VUID (if persisted)
};
```

### 4. Managed VUID Class

**File:** `System.Private.CoreLib/src/System/OS/VUID.cs` (new)

```csharp
namespace System.OS
{
    /// <summary>
    /// Virtual Object Unique Identifier - UUID v7 format.
    /// Globally unique, time-sortable, survives process restarts.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct VUID : IEquatable<VUID>, IComparable<VUID>
    {
        private readonly ulong _hi;
        private readonly ulong _lo;

        public bool IsEmpty => _hi == 0 && _lo == 0;
        public static VUID Empty => default;

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern VUID New();

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

        public override string ToString()
        {
            Span<byte> bytes = stackalloc byte[16];
            WriteBytes(bytes);
            return $"{bytes[0]:x2}{bytes[1]:x2}{bytes[2]:x2}{bytes[3]:x2}-" +
                   $"{bytes[4]:x2}{bytes[5]:x2}-{bytes[6]:x2}{bytes[7]:x2}-" +
                   $"{bytes[8]:x2}{bytes[9]:x2}-" +
                   $"{bytes[10]:x2}{bytes[11]:x2}{bytes[12]:x2}{bytes[13]:x2}{bytes[14]:x2}{bytes[15]:x2}";
        }

        public void WriteBytes(Span<byte> destination)
        {
            if (destination.Length < 16) throw new ArgumentException("Buffer too small");
            BinaryPrimitives.WriteUInt64BigEndian(destination, _hi);
            BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(8), _lo);
        }

        public static VUID FromBytes(ReadOnlySpan<byte> source)
        {
            if (source.Length < 16) throw new ArgumentException("Buffer too small");
            return new VUID(
                BinaryPrimitives.ReadUInt64BigEndian(source),
                BinaryPrimitives.ReadUInt64BigEndian(source.Slice(8)));
        }

        private VUID(ulong hi, ulong lo) { _hi = hi; _lo = lo; }
    }
}
```

---

## QCall Registration

**File:** `src/runtime/src/coreclr/vm/tds/tdsqcalls.cpp` (add)

```cpp
extern "C" void QCALLTYPE TDSNative_GenerateVUID(VUID* result)
{
    QCALL_CONTRACT;
    BEGIN_QCALL;
    *result = TDS_GenerateVUID();
    END_QCALL;
}
```

---

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `vm/tds/vuid.h` | Create | VUID structure and functions |
| `vm/tds/vuid.cpp` | Create | VUID implementation |
| `vm/tds/opsroottable.h` | Modify | Add VUID field to entry |
| `vm/CMakeLists.txt` | Modify | Add vuid.cpp |
| `System/OS/VUID.cs` | Create | Managed VUID struct |
| `vm/tds/tdsqcalls.cpp` | Modify | Add VUID QCall |
| `vm/qcallentrypoints.cpp` | Modify | Register VUID QCall |

---

## Acceptance Criteria

- [ ] Native VUID struct with UUID v7 format
- [ ] TDS_GenerateVUID produces valid, time-sortable UUIDs
- [ ] VUIDs are unique (test with 1M generations)
- [ ] VUID serialization to/from bytes works correctly
- [ ] Managed VUID struct with ToString() and comparison
- [ ] VUID.New() QCall works from managed code
- [ ] OpsRootEntry can store VUID

---

## Testing

```csharp
[Fact]
public void VUID_Generation_IsUnique()
{
    var set = new HashSet<VUID>();
    for (int i = 0; i < 10000; i++)
    {
        var vuid = VUID.New();
        Assert.True(set.Add(vuid), "Duplicate VUID generated");
    }
}

[Fact]
public void VUID_IsTimeSortable()
{
    var v1 = VUID.New();
    Thread.Sleep(10);
    var v2 = VUID.New();

    Assert.True(v1.CompareTo(v2) < 0, "VUIDs should be time-sortable");
}

[Fact]
public void VUID_RoundTripsBytes()
{
    var original = VUID.New();
    var bytes = new byte[16];
    original.WriteBytes(bytes);
    var restored = VUID.FromBytes(bytes);

    Assert.Equal(original, restored);
}
```

---

## References

- Phase 2 Main Doc: Section 5 (VUID)
- UUID v7: IETF draft-peabody-dispatch-new-uuid-format
