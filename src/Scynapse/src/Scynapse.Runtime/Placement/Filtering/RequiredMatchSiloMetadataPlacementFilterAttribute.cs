using System;
using System.Diagnostics.CodeAnalysis;
using Scynapse.Placement;

#nullable enable
namespace Scynapse.Runtime.Placement.Filtering;

/// <summary>
/// Attribute to specify that a silo must have a specific metadata key-value pair matching the local (calling) silo to be considered for placement.
/// </summary>
/// <param name="metadataKeys"></param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
[Experimental("SCYNAPSEEXP004")]
public class RequiredMatchSiloMetadataPlacementFilterAttribute(string[] metadataKeys, int order = 0)
    : PlacementFilterAttribute(new RequiredMatchSiloMetadataPlacementFilterStrategy(metadataKeys, order));