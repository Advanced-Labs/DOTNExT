# ADR-001: Hybrid Storage Model for Virtual Objects

> **Status:** Decided
> **Date:** 2026-01-30
> **Decision Makers:** Louis (Orchestrator), CAI (Implementation)

---

## Context

VAYRON's Memory System needs to persist virtual objects to Voron storage. The original T06 spec proposed a pure blob serialization approach where each object is stored as a single byte array.

However, this approach has significant limitations:
- **Not searchable**: Binary blobs cannot be indexed by Corax
- **No semantic search**: Cannot vectorize field values for AI-powered search
- **No field-level queries**: Cannot query "all Customers where Balance > 1000"

VAYRON's vision requires durable memory that is **searchable and indexable**.

---

## Decision

Adopt a **Hybrid Storage Model** with field-level storage:

### Storage Key Structure

```
{VUID}/meta           → Object metadata (type, version, flags)
{VUID}/f/{FieldToken} → Primitive or string field value (SEARCHABLE)
{VUID}/r/{FieldToken} → VUID reference to [Memorize] child (TRAVERSABLE)
{VUID}/e/{FieldToken} → Embedded blob for non-virtual child (NOT SEARCHABLE)
```

### Field Storage Rules

| Field Type | Key Prefix | Value | Searchable | Notes |
|------------|------------|-------|------------|-------|
| Primitives | `f/` | Direct value | Yes | int, bool, float, etc. |
| String | `f/` | UTF-8 bytes | Yes | Text + semantic search |
| DateTime/Guid/VUID | `f/` | Binary | Yes | Fixed-size types |
| `[Virtual, Memorize]` ref | `r/` | VUID (16 bytes) | Traversable | Independent entity |
| Non-virtual class ref | `e/` | Serialized blob | No | Owned by parent |
| Collections | Mixed | Depends on elements | Mixed | See below |

### Child Object Handling

```
Is child type marked [Virtual, Memorize]?
│
├── YES: Store VUID reference only
│        - Child is persisted independently
│        - Child has its own lifecycle
│        - Loaded lazily on access
│
└── NO:  Serialize as embedded blob
         - Child is "owned" by parent
         - No independent identity
         - Loaded when parent loads
         - Ephemeral (dies with parent)
```

### Collections

- `List<int>`, `int[]` → Serialized as blob in `f/`
- `List<Customer>` where Customer is `[Memorize]` → List of VUIDs in `r/`
- `List<Address>` where Address is not `[Memorize]` → Serialized blob in `e/`

---

## Consequences

### Positive

1. **Corax Integration**: Primitive and string fields can be indexed
2. **Text Search**: String fields support full-text search
3. **Semantic Search**: String fields can be vectorized for AI search
4. **Object Graph**: VUID references create a navigable object graph
5. **Partial Updates**: Change one field = update one key (efficient)
6. **Query Support**: Range queries, equality filters on indexed fields

### Negative

1. **More Keys**: N fields = N+1 Voron keys per object
2. **Serialization Cost**: Non-virtual children still need serialization
3. **Complexity**: More logic than pure blob approach

### Neutral

1. **Performance**: Acceptable for Phase 2; optimization possible later
2. **Migration**: Field-level storage is forward-compatible with schema evolution

---

## Implementation Notes

### Save Operation

```csharp
void Save(object obj, VUID vuid, Transaction tx)
{
    var tree = tx.CreateTree("vobjects");

    // 1. Write metadata
    tree.Add($"{vuid}/meta", SerializeMetadata(obj));

    // 2. Write each field
    foreach (var field in GetFields(obj))
    {
        var token = field.MetadataToken;
        var value = field.GetValue(obj);

        if (IsPrimitiveOrString(field.FieldType))
        {
            // Searchable field
            tree.Add($"{vuid}/f/{token}", Serialize(value));
        }
        else if (HasMemorizeAttribute(field.FieldType))
        {
            // Reference to another [Memorize] object
            var childVuid = GetOrCreateVUID(value);
            tree.Add($"{vuid}/r/{token}", childVuid.ToBytes());
            EnsureChildIsSaved(value);
        }
        else
        {
            // Embedded non-virtual child
            tree.Add($"{vuid}/e/{token}", SerializeBlob(value));
        }
    }
}
```

### Load Operation

```csharp
T Load<T>(VUID vuid, Transaction tx)
{
    var tree = tx.ReadTree("vobjects");
    var obj = CreateInstance<T>();

    // 1. Read metadata (for validation)
    var meta = tree.Read($"{vuid}/meta");

    // 2. Read each field
    foreach (var field in GetFields(typeof(T)))
    {
        var token = field.MetadataToken;

        // Try field-level storage first
        var fValue = tree.Read($"{vuid}/f/{token}");
        if (fValue != null)
        {
            field.SetValue(obj, Deserialize(fValue, field.FieldType));
            continue;
        }

        // Try reference storage
        var rValue = tree.Read($"{vuid}/r/{token}");
        if (rValue != null)
        {
            var childVuid = VUID.FromBytes(rValue);
            // Lazy load or immediate load depending on strategy
            field.SetValue(obj, CreateLazyProxy(childVuid, field.FieldType));
            continue;
        }

        // Try embedded storage
        var eValue = tree.Read($"{vuid}/e/{token}");
        if (eValue != null)
        {
            field.SetValue(obj, DeserializeBlob(eValue, field.FieldType));
            continue;
        }

        // Field not found - leave as default (schema evolution)
    }

    return obj;
}
```

---

## Future Considerations

1. **Body Cache**: Optional `{VUID}/body` for bulk serialization (optimization)
2. **Background Reserialization**: Regenerate body cache asynchronously
3. **Corax Indexes**: Define indexes on specific fields
4. **Vector Embeddings**: Store embeddings for semantic search
5. **Compression**: Compress large string/blob values

---

## References

- T06: Body Encoder (updated to reflect this decision)
- T05: Storage_Voron Driver (implements this model)
- Corax documentation (RavenDB indexing)
