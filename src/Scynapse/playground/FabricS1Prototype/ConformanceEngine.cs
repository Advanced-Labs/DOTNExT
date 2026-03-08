using System.Text.Json;

namespace FabricS1Prototype;

internal sealed class ConformanceEngine
{
    private static readonly HashSet<string> KnownMessageTypes = new(StringComparer.Ordinal)
    {
        "ResolveRequest",
        "ResolveResponse",
        "ResolveReferral",
        "ResolveDeny",
        "HandshakeInit",
        "HandshakeChallenge",
        "HandshakeProof",
        "HandshakeAccept",
        "HandshakeDeny",
        "RouteUpgradeProbe",
        "RouteUpgradeReject"
    };

    private static readonly Dictionary<string, HashSet<string>> RequiredBodyFieldsByType = new(StringComparer.Ordinal)
    {
        ["ResolveRequest"] = new(StringComparer.Ordinal) { "expr_raw", "operation_class" },
        ["ResolveDeny"] = new(StringComparer.Ordinal) { "deny_code" },
        ["HandshakeAccept"] = new(StringComparer.Ordinal) { "route_mode", "disclosure_level" },
        ["RouteUpgradeReject"] = new(StringComparer.Ordinal) { "decision_code" }
    };

    private static readonly Dictionary<string, HashSet<string>> AllowedDenyCodesByMessageType = new(StringComparer.Ordinal)
    {
        ["ResolveDeny"] = new(StringComparer.Ordinal)
        {
            "PathNotFound",
            "PolicyDenied",
            "DisclosureDenied",
            "AmbiguousResolution",
            "MediatorUnavailable",
            "TrustInsufficient"
        },
        ["HandshakeDeny"] = new(StringComparer.Ordinal)
        {
            "PolicyDenied",
            "TrustInsufficient",
            "GrantMissing",
            "GrantExpired",
            "DisclosureDenied",
            "MediatorUnavailable"
        }
    };

    private static readonly Dictionary<string, HashSet<string>> AllowedStateTransitions = new(StringComparer.Ordinal)
    {
        ["ResolveIntent"] = new(StringComparer.Ordinal) { "DiscoverPath" },
        ["DiscoverPath"] = new(StringComparer.Ordinal) { "PolicyEvaluate", "Deny" },
        ["PolicyEvaluate"] = new(StringComparer.Ordinal) { "DisclosurePlan", "Deny" },
        ["DisclosurePlan"] = new(StringComparer.Ordinal) { "MediatedHandshake", "Deny" },
        ["MediatedHandshake"] = new(StringComparer.Ordinal) { "RelayedSession", "Deny" },
        ["RelayedSession"] = new(StringComparer.Ordinal) { "DirectUpgradeProbe", "Completed" },
        ["DirectUpgradeProbe"] = new(StringComparer.Ordinal) { "DirectSession", "RelayedSession", "Deny" },
        ["DirectSession"] = new(StringComparer.Ordinal) { "RelayedSession", "Completed" }
    };

    private static readonly Dictionary<string, DenyProfile> DenyProfiles = new(StringComparer.Ordinal)
    {
        ["PathNotFound"] = new() { Code = "PathNotFound", Retryable = false, Remediation = "Use nearest authoritative scope or referral hint." },
        ["PolicyDenied"] = new() { Code = "PolicyDenied", Retryable = false, Remediation = "Inspect policy reference and required action class." },
        ["DisclosureDenied"] = new() { Code = "DisclosureDenied", Retryable = false, Remediation = "Request required disclosure level and missing gate." },
        ["TrustInsufficient"] = new() { Code = "TrustInsufficient", Retryable = false, Remediation = "Provide missing trust proof class." },
        ["UpgradeRejected"] = new() { Code = "UpgradeRejected", Retryable = true, Remediation = "Stay on fallback route and retry later." },
        ["MediatorUnavailable"] = new() { Code = "MediatorUnavailable", Retryable = true, Remediation = "Backoff and retry with alternate mediator or referral." },
        ["GrantMissing"] = new() { Code = "GrantMissing", Retryable = true, Remediation = "Obtain required grant action and scope." },
        ["GrantExpired"] = new() { Code = "GrantExpired", Retryable = true, Remediation = "Renew grant at configured renewal scope." },
        ["ReplayWindowExpired"] = new() { Code = "ReplayWindowExpired", Retryable = true, Remediation = "Resubscribe from current head cursor." },
        ["AmbiguousResolution"] = new() { Code = "AmbiguousResolution", Retryable = true, Remediation = "Provide selector hints to disambiguate candidates." }
    };

    public VectorResult Evaluate(FixtureCase fixture)
    {
        var errors = new List<string>();
        var envelopeMessages = BuildEnvelopeMessages(fixture, errors);

        ValidateEnvelopeAndSchema(fixture, envelopeMessages, errors);
        ValidateMessageFieldRules(fixture, errors);
        ValidateStateTrace(fixture, errors);

        var observedDenyCode = GetObservedDenyCode(fixture);
        bool? observedRetryable = null;

        if (observedDenyCode is not null)
        {
            if (!DenyProfiles.TryGetValue(observedDenyCode, out var profile))
            {
                errors.Add($"[L4] Unknown deny code '{observedDenyCode}'.");
            }
            else
            {
                observedRetryable = profile.Retryable;
            }
        }

        ValidateExpectedOutcome(fixture, observedDenyCode, observedRetryable, errors);
        ValidateAssertions(fixture, envelopeMessages, observedDenyCode, observedRetryable, errors);

        return new VectorResult
        {
            Id = fixture.Id,
            Passed = errors.Count == 0,
            Errors = errors,
            ObservedStateTrace = fixture.ExpectedStateTrace.AsReadOnly(),
            ObservedDenyCode = observedDenyCode,
            ObservedRetryable = observedRetryable
        };
    }

    private static List<EnvelopeMessage> BuildEnvelopeMessages(FixtureCase fixture, List<string> errors)
    {
        var result = new List<EnvelopeMessage>(fixture.Messages.Count);
        for (var i = 0; i < fixture.Messages.Count; i++)
        {
            var message = fixture.Messages[i];
            if (string.IsNullOrWhiteSpace(message.Type))
            {
                errors.Add($"[L1] Fixture {fixture.Id} message index {i} has empty type.");
                continue;
            }

            var envelope = new EnvelopeMessage
            {
                MsgType = message.Type,
                MsgId = $"{fixture.Id}-m{i + 1}",
                TraceId = $"{fixture.Id}-trace",
                TtlMs = 30_000,
                Source = message
            };
            result.Add(envelope);
        }

        return result;
    }

    private static void ValidateEnvelopeAndSchema(
        FixtureCase fixture,
        IReadOnlyList<EnvelopeMessage> envelopeMessages,
        List<string> errors)
    {
        foreach (var envelope in envelopeMessages)
        {
            if (string.IsNullOrWhiteSpace(envelope.MsgType))
            {
                errors.Add($"[L1] {fixture.Id} contains empty msg_type.");
            }

            if (string.IsNullOrWhiteSpace(envelope.MsgId))
            {
                errors.Add($"[L1] {fixture.Id}/{envelope.MsgType} missing msg_id.");
            }

            if (string.IsNullOrWhiteSpace(envelope.TraceId))
            {
                errors.Add($"[L1] {fixture.Id}/{envelope.MsgType} missing trace_id.");
            }

            if (envelope.TtlMs <= 0)
            {
                errors.Add($"[L1] {fixture.Id}/{envelope.MsgType} has invalid ttl_ms.");
            }

            if (!KnownMessageTypes.Contains(envelope.MsgType))
            {
                errors.Add($"[L1] {fixture.Id}/{envelope.MsgType} is unknown.");
            }
        }
    }

    private static void ValidateMessageFieldRules(FixtureCase fixture, List<string> errors)
    {
        foreach (var message in fixture.Messages)
        {
            if (!RequiredBodyFieldsByType.TryGetValue(message.Type, out var requiredFields))
            {
                continue;
            }

            if (message.Body is null || message.Body.Value.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"[L2] {fixture.Id}/{message.Type} missing required body.");
                continue;
            }

            foreach (var field in requiredFields)
            {
                if (!message.Body.Value.TryGetProperty(field, out _))
                {
                    errors.Add($"[L2] {fixture.Id}/{message.Type} missing required field '{field}'.");
                }
            }

            if (message.Type is "ResolveDeny" or "HandshakeDeny")
            {
                var denyCode = GetBodyString(message, "deny_code");
                if (denyCode is null)
                {
                    continue;
                }

                if (AllowedDenyCodesByMessageType.TryGetValue(message.Type, out var allowedCodes) &&
                    !allowedCodes.Contains(denyCode))
                {
                    errors.Add($"[L4] {fixture.Id}/{message.Type} deny code '{denyCode}' is not allowed.");
                }
            }
        }
    }

    private static void ValidateStateTrace(FixtureCase fixture, List<string> errors)
    {
        if (fixture.ExpectedStateTrace.Count == 0)
        {
            errors.Add($"[L3] {fixture.Id} has empty expected_state_trace.");
            return;
        }

        for (var i = 1; i < fixture.ExpectedStateTrace.Count; i++)
        {
            var current = fixture.ExpectedStateTrace[i - 1];
            var next = fixture.ExpectedStateTrace[i];
            if (!AllowedStateTransitions.TryGetValue(current, out var allowedNext) || !allowedNext.Contains(next))
            {
                errors.Add($"[L3] {fixture.Id} invalid transition '{current}' -> '{next}'.");
            }
        }

        var finalState = fixture.ExpectedStateTrace[^1];
        if (fixture.ExpectedOutcome.Success && !string.Equals(finalState, "Completed", StringComparison.Ordinal))
        {
            errors.Add($"[L3] {fixture.Id} expected success but final state is '{finalState}'.");
        }

        if (!fixture.ExpectedOutcome.Success && !string.Equals(finalState, "Deny", StringComparison.Ordinal))
        {
            errors.Add($"[L3] {fixture.Id} expected deny but final state is '{finalState}'.");
        }
    }

    private static void ValidateExpectedOutcome(
        FixtureCase fixture,
        string? observedDenyCode,
        bool? observedRetryable,
        List<string> errors)
    {
        var expectedCode = fixture.ExpectedOutcome.DenyCode;
        if (!string.Equals(expectedCode, observedDenyCode, StringComparison.Ordinal))
        {
            if (!(expectedCode is null && observedDenyCode is null))
            {
                errors.Add($"[L4] {fixture.Id} expected deny code '{expectedCode ?? "null"}' but observed '{observedDenyCode ?? "null"}'.");
            }
        }

        if (fixture.ExpectedOutcome.Retryable is { } expectedRetryable)
        {
            if (observedRetryable is null)
            {
                errors.Add($"[L4] {fixture.Id} expected retryable='{expectedRetryable}', but no deny profile was observed.");
            }
            else if (observedRetryable.Value != expectedRetryable)
            {
                errors.Add($"[L4] {fixture.Id} expected retryable='{expectedRetryable}' but observed '{observedRetryable.Value}'.");
            }
        }
    }

    private static void ValidateAssertions(
        FixtureCase fixture,
        IReadOnlyList<EnvelopeMessage> envelopeMessages,
        string? observedDenyCode,
        bool? observedRetryable,
        List<string> errors)
    {
        foreach (var assertion in fixture.Assertions)
        {
            switch (assertion.Check)
            {
                case "final_state_equals":
                {
                    var expected = assertion.Value?.GetStringOrNull();
                    var actual = fixture.ExpectedStateTrace.Count == 0 ? null : fixture.ExpectedStateTrace[^1];
                    if (!string.Equals(expected, actual, StringComparison.Ordinal))
                    {
                        errors.Add($"[A:{assertion.Id}] expected final state '{expected}', observed '{actual ?? "null"}'.");
                    }

                    break;
                }
                case "contains_state":
                {
                    var expected = assertion.Value?.GetStringOrNull();
                    if (expected is null || !fixture.ExpectedStateTrace.Contains(expected, StringComparer.Ordinal))
                    {
                        errors.Add($"[A:{assertion.Id}] expected state trace to contain '{expected ?? "null"}'.");
                    }

                    break;
                }
                case "deny_code_absent":
                {
                    if (observedDenyCode is not null)
                    {
                        errors.Add($"[A:{assertion.Id}] expected no deny code but observed '{observedDenyCode}'.");
                    }

                    break;
                }
                case "deny_code_equals":
                {
                    var expected = assertion.Value?.GetStringOrNull();
                    if (!string.Equals(expected, observedDenyCode, StringComparison.Ordinal))
                    {
                        errors.Add($"[A:{assertion.Id}] expected deny code '{expected}', observed '{observedDenyCode ?? "null"}'.");
                    }

                    break;
                }
                case "required_fields_present":
                {
                    var fields = assertion.Value?.GetStringArrayOrEmpty() ?? Array.Empty<string>();
                    foreach (var field in fields)
                    {
                        var allHaveField = field switch
                        {
                            "msg_type" => envelopeMessages.All(m => !string.IsNullOrWhiteSpace(m.MsgType)),
                            "msg_id" => envelopeMessages.All(m => !string.IsNullOrWhiteSpace(m.MsgId)),
                            "trace_id" => envelopeMessages.All(m => !string.IsNullOrWhiteSpace(m.TraceId)),
                            "ttl_ms" => envelopeMessages.All(m => m.TtlMs > 0),
                            _ => false
                        };

                        if (!allHaveField)
                        {
                            errors.Add($"[A:{assertion.Id}] required envelope field '{field}' missing.");
                        }
                    }

                    break;
                }
                case "remediation_present":
                {
                    var hasRemediation = observedDenyCode is not null
                        && DenyProfiles.TryGetValue(observedDenyCode, out var profile)
                        && !string.IsNullOrWhiteSpace(profile.Remediation);
                    if (!hasRemediation)
                    {
                        errors.Add($"[A:{assertion.Id}] expected remediation hint for deny code.");
                    }

                    break;
                }
                case "remediation_contains":
                {
                    var expectedToken = assertion.Value?.GetStringOrNull();
                    if (observedDenyCode is null || !DenyProfiles.TryGetValue(observedDenyCode, out var profile))
                    {
                        errors.Add($"[A:{assertion.Id}] no deny profile to validate remediation token.");
                        break;
                    }

                    if (expectedToken is null ||
                        profile.Remediation.IndexOf(expectedToken, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        errors.Add($"[A:{assertion.Id}] remediation does not contain '{expectedToken ?? "null"}'.");
                    }

                    break;
                }
                case "selector_hints_present":
                {
                    var request = fixture.Messages.FirstOrDefault(m => string.Equals(m.Type, "ResolveRequest", StringComparison.Ordinal));
                    if (request is null ||
                        request.Body is null ||
                        request.Body.Value.ValueKind != JsonValueKind.Object ||
                        !request.Body.Value.TryGetProperty("selector_hints", out var hints) ||
                        hints.ValueKind != JsonValueKind.Array ||
                        hints.GetArrayLength() == 0)
                    {
                        errors.Add($"[A:{assertion.Id}] expected non-empty selector_hints in ResolveRequest.");
                    }

                    break;
                }
                case "handshake_accept_route_mode":
                {
                    var expected = assertion.Value?.GetStringOrNull();
                    var accept = fixture.Messages.FirstOrDefault(m => string.Equals(m.Type, "HandshakeAccept", StringComparison.Ordinal));
                    var actual = accept is null ? null : GetBodyString(accept, "route_mode");
                    if (!string.Equals(expected, actual, StringComparison.Ordinal))
                    {
                        errors.Add($"[A:{assertion.Id}] expected HandshakeAccept route_mode '{expected}', observed '{actual ?? "null"}'.");
                    }

                    break;
                }
                case "direct_upgrade_rejected":
                {
                    var reject = fixture.Messages.FirstOrDefault(m => string.Equals(m.Type, "RouteUpgradeReject", StringComparison.Ordinal));
                    var decision = reject is null ? null : GetBodyString(reject, "decision_code");
                    if (!string.Equals(decision, "UpgradeRejected", StringComparison.Ordinal))
                    {
                        errors.Add($"[A:{assertion.Id}] expected RouteUpgradeReject decision_code 'UpgradeRejected', observed '{decision ?? "null"}'.");
                    }

                    break;
                }
                case "retryable_equals":
                {
                    var expected = assertion.Value?.GetBooleanOrNull();
                    if (expected is null || observedRetryable is null || expected.Value != observedRetryable.Value)
                    {
                        errors.Add($"[A:{assertion.Id}] expected retryable '{expected?.ToString() ?? "null"}', observed '{observedRetryable?.ToString() ?? "null"}'.");
                    }

                    break;
                }
                default:
                    errors.Add($"[A:{assertion.Id}] unsupported assertion check '{assertion.Check}'.");
                    break;
            }
        }
    }

    private static string? GetObservedDenyCode(FixtureCase fixture)
    {
        foreach (var message in fixture.Messages)
        {
            if (message.Type is "ResolveDeny" or "HandshakeDeny")
            {
                var code = GetBodyString(message, "deny_code");
                if (code is not null)
                {
                    return code;
                }
            }
        }

        return null;
    }

    private static string? GetBodyString(FixtureMessage message, string propertyName)
    {
        if (message.Body is null || message.Body.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!message.Body.Value.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return property.GetString();
    }
}

internal static class JsonElementExtensions
{
    public static string? GetStringOrNull(this JsonElement element)
    {
        return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
    }

    public static bool? GetBooleanOrNull(this JsonElement element)
    {
        return element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False
            ? element.GetBoolean()
            : null;
    }

    public static string[] GetStringArrayOrEmpty(this JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var values = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { } text)
            {
                values.Add(text);
            }
        }

        return values.ToArray();
    }
}

