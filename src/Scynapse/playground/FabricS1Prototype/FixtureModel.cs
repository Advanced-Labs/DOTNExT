using System.Text.Json;
using System.Text.Json.Serialization;

namespace FabricS1Prototype;

internal sealed class FixtureCase
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("expect_conformance")]
    public string ExpectConformance { get; set; } = "pass";

    [JsonPropertyName("expected_error_contains")]
    public List<string> ExpectedErrorContains { get; set; } = new();

    [JsonPropertyName("preconditions")]
    public List<string> Preconditions { get; set; } = new();

    [JsonPropertyName("messages")]
    public List<FixtureMessage> Messages { get; set; } = new();

    [JsonPropertyName("expected_state_trace")]
    public List<string> ExpectedStateTrace { get; set; } = new();

    [JsonPropertyName("expected_outcome")]
    public ExpectedOutcome ExpectedOutcome { get; set; } = new();

    [JsonPropertyName("assertions")]
    public List<FixtureAssertion> Assertions { get; set; } = new();
}

internal sealed class FixtureMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public JsonElement? Body { get; set; }

    [JsonPropertyName("negative")]
    public bool Negative { get; set; }
}

internal sealed class ExpectedOutcome
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("deny_code")]
    public string? DenyCode { get; set; }

    [JsonPropertyName("retryable")]
    public bool? Retryable { get; set; }

    [JsonPropertyName("upgrade_attempt_code")]
    public string? UpgradeAttemptCode { get; set; }
}

internal sealed class FixtureAssertion
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("check")]
    public string Check { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public JsonElement? Value { get; set; }
}

internal sealed class EnvelopeMessage
{
    public required string MsgType { get; init; }
    public required string MsgId { get; init; }
    public required string TraceId { get; init; }
    public required int TtlMs { get; init; }
    public required FixtureMessage Source { get; init; }
}

internal sealed class DenyProfile
{
    public required string Code { get; init; }
    public required bool Retryable { get; init; }
    public required string Remediation { get; init; }
}

internal sealed class VectorResult
{
    public required string Id { get; init; }
    public required bool Passed { get; init; }
    public required List<string> Errors { get; init; }
    public required IReadOnlyList<string> ObservedStateTrace { get; init; }
    public string? ObservedDenyCode { get; init; }
    public bool? ObservedRetryable { get; init; }
    public bool EffectivePassed { get; set; }
}
