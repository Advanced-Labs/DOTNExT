# VAYRON SOS Extension Commands

> Documentation for SOS debugger extension commands for VAYRON persistent handles.

## Overview

VAYRON handles are marked with bit 31 (`BIT_SBLK_IS_VAYRON_HANDLE = 0x80000000`) in the object
header's sync block value. This enables SOS extensions to identify and inspect VAYRON handles
during debugging sessions.

## Proposed SOS Commands

### !vayronhandle

Display VAYRON handle information for an object.

```
!vayronhandle <address>
```

**Output:**
```
VAYRON Handle at 0x00007fff12345678
  Type:           Person
  OID:            42
  Epoch:          1234567890
  State:          Materialized
  Storage:        0x00001234 (Container entry)
  Dirty:          No

Header Info:
  SyncBlock:      0x80000000 (VAYRON bit set)
  IsVayronHandle: Yes
```

### !vayronlist

List all VAYRON handles in the heap.

```
!vayronlist [-type <typename>] [-dirty] [-stale]
```

**Options:**
- `-type <typename>`: Filter by type name
- `-dirty`: Show only dirty handles
- `-stale`: Show only stale handles

**Output:**
```
Address            Type            OID      Epoch      State
----------------   --------------  -------  ---------  -----------
0x00007fff1234     Person          42       1234567    Materialized
0x00007fff5678     Product         108      1234568    Dirty
0x00007fff9abc     Order           256      1234500    Stale

Total: 3 VAYRON handles
```

### !vayronenv

Display VAYRON environment information.

```
!vayronenv <address>
```

**Output:**
```
VAYRON Environment at 0x00007fff00001000
  Voron Path:     /data/myapp/storage
  Next OID:       1024
  Dirty Handles:  5
  Is New:         No

Storage Statistics:
  OID Index:      1000 entries
  Body Container: 256 MB used
```

### !vayronoid

Lookup a VAYRON handle by OID.

```
!vayronoid <oid>
```

## Implementation Notes

### Detecting VAYRON Handles

SOS can detect VAYRON handles by checking bit 31 of the sync block value:

```cpp
bool IsVayronHandle(CLRDATA_ADDRESS objAddr)
{
    // Read object header (at objAddr - sizeof(ObjHeader))
    DWORD syncBlockValue = 0;
    if (FAILED(g_ExtData->ReadVirtual(
        objAddr - sizeof(ObjHeader) + offsetof(ObjHeader, m_SyncBlockValue),
        &syncBlockValue, sizeof(syncBlockValue), nullptr)))
    {
        return false;
    }

    return (syncBlockValue & 0x80000000) != 0;
}
```

### Heap Enumeration

To find all VAYRON handles, enumerate the GC heap and check each object:

```cpp
void EnumerateVayronHandles(ISOSCallback* callback)
{
    // Use existing heap enumeration APIs
    // For each object, check if IsVayronHandle(objAddr)
    // If so, invoke callback
}
```

### Managed Diagnostics API

The managed `VayronRuntime.GetHeaderInfo()` method provides the same information
accessible to SOS, allowing tools like Visual Studio Diagnostics to inspect handles.

## Integration with Visual Studio

### Natvis Visualizers

Custom Natvis visualizers can display VAYRON handle information in the debugger:

```xml
<Type Name="Vayron.VayronHandle">
  <DisplayString>VAYRON[OID={_oid.Value}] {_isDirty ? "Dirty" : "Clean"}</DisplayString>
  <Expand>
    <Item Name="OID">_oid.Value</Item>
    <Item Name="Epoch">_epoch</Item>
    <Item Name="IsDirty">_isDirty</Item>
    <Item Name="IsMaterialized">_cachedBody != nullptr</Item>
  </Expand>
</Type>
```

### Debug Attributes

The Vayron assembly includes `[DebuggerDisplay]` and `[DebuggerTypeProxy]` attributes
for improved debugging experience.

## Future Extensions

1. **Transaction Visualization**: Show active transactions and their handles
2. **Body Inspection**: Display serialized body contents
3. **Relationship Graph**: Visualize handle relationships
4. **Performance Metrics**: Show materialization times and cache hit rates
