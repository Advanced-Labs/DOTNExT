// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// TDSINTRINSICS.H
//
// TypeDriver System (TDS) - Field Access Intrinsics
// Phase 1: Explicit intrinsic calls (no JIT modification)

#ifndef _TDS_INTRINSICS_H_
#define _TDS_INTRINSICS_H_

#include "common.h"

// Forward declarations
struct Object;
class FieldDesc;

//=============================================================================
// TDS Field Access Intrinsics (Phase 1: explicit calls)
//
// These intrinsics check the TDS routing bit and dispatch through the
// appropriate driver. For default objects, they proxy to standard CLR
// behavior with minimal overhead.
//
// Phase 1 does NOT modify JIT helpers - these must be called explicitly.
// Transparent interception comes in Phase 2.5 (IMP-002).
//=============================================================================

// Read a field value through TDS routing
// Parameters:
//   obj        - The object containing the field
//   field      - The FieldDesc describing the field
//   buffer     - Output buffer to receive the field value
//   bufferSize - Size of the output buffer in bytes
// Returns: bytes read, or -1 on error
intptr_t TDS_ReadField(Object* obj, FieldDesc* field, void* buffer, size_t bufferSize);

// Write a field value through TDS routing
// Parameters:
//   obj       - The object containing the field
//   field     - The FieldDesc describing the field
//   value     - Pointer to the value to write
//   valueSize - Size of the value in bytes
void TDS_WriteField(Object* obj, FieldDesc* field, const void* value, size_t valueSize);

// Write a reference field with GC barrier through TDS routing
// Parameters:
//   obj    - The object containing the field
//   field  - The FieldDesc (must be a reference field)
//   newRef - The new reference value
void TDS_WriteRefField(Object* obj, FieldDesc* field, Object* newRef);

// Get effective field address through TDS routing
// Note: May trigger materialization for lazy/remote objects
// Parameters:
//   obj   - The object containing the field
//   field - The FieldDesc describing the field
// Returns: Direct memory address, or nullptr if field must be accessed through driver
void* TDS_GetFieldAddress(Object* obj, FieldDesc* field);

#endif // _TDS_INTRINSICS_H_
