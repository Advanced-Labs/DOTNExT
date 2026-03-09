using System.Text.Json;
using System.Text.RegularExpressions;

namespace FabricS1Prototype;

internal sealed class ConformanceEngine
{
    private const string SliceProfileS1 = "S1";
    private const string SliceProfileS2 = "S2";
    private const string SliceProfileS3 = "S3";
    private const string SliceProfileS4 = "S4";
    private const string SliceProfileS5 = "S5";
    private const string SliceProfileM1S1 = "M1-S1";
    private const string SliceProfileM1S2 = "M1-S2";

    private static readonly Regex TypedIdentifierRegex = new("^[a-z]{2,6}:[A-Za-z0-9][A-Za-z0-9._-]{2,127}$", RegexOptions.Compiled);
    private static readonly Regex RelationTokenCidRegex = new("^sha256:[0-9a-fA-F]{8,128}$", RegexOptions.Compiled);

    private static readonly HashSet<string> KnownSliceProfiles = new(StringComparer.Ordinal)
    {
        SliceProfileS1,
        SliceProfileS2,
        SliceProfileS3,
        SliceProfileS4,
        SliceProfileS5,
        SliceProfileM1S1,
        SliceProfileM1S2
    };

    private static readonly HashSet<string> KnownTokenTransportModes = new(StringComparer.Ordinal)
    {
        "reference",
        "inline"
    };

    private static readonly HashSet<string> KnownTransportPaths = new(StringComparer.Ordinal)
    {
        "mediated",
        "direct"
    };

    private static readonly HashSet<string> PolicyCausalDenyCodes = new(StringComparer.Ordinal)
    {
        "PolicyDenied",
        "DisclosureDenied",
        "GrantMissing",
        "GrantExpired"
    };

    private static readonly HashSet<string> TypedIdentifierPrefixes = new(StringComparer.Ordinal)
    {
        "nid",
        "rid",
        "gid",
        "pid",
        "tid",
        "rte",
        "mid",
        "trc"
    };

    private static readonly HashSet<string> KnownGrantStatuses = new(StringComparer.Ordinal)
    {
        "active",
        "missing",
        "expired",
        "not_required"
    };

    private static readonly HashSet<string> KnownEndpointDirectoryModes = new(StringComparer.Ordinal)
    {
        "plaintext",
        "encrypted"
    };

    private static readonly HashSet<string> KnownObserveScopeModes = new(StringComparer.Ordinal)
    {
        "exact",
        "subtree"
    };

    private static readonly HashSet<string> KnownObserveGapCauses = new(StringComparer.Ordinal)
    {
        "RetentionExpired",
        "PolicyDenied",
        "TransportLoss"
    };

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
        "GrantPresent",
        "PolicyDelta",
        "PolicyDeny",
        "RouteUpgradeProbe",
        "RouteUpgradeAccept",
        "RouteUpgradeReject",
        "ObserveOpen",
        "ObserveAck",
        "ObserveEvent",
        "ObserveGap",
        "ObserveResume",
        "ObserveClose",
        "RouteData"
    };

    private static readonly Dictionary<string, HashSet<string>> RequiredBodyFieldsByType = new(StringComparer.Ordinal)
    {
        ["ResolveRequest"] = new(StringComparer.Ordinal) { "expr_raw", "operation_class" },
        ["ResolveDeny"] = new(StringComparer.Ordinal) { "deny_code" },
        ["HandshakeAccept"] = new(StringComparer.Ordinal) { "route_mode", "disclosure_level" },
        ["HandshakeDeny"] = new(StringComparer.Ordinal) { "deny_code" },
        ["GrantPresent"] = new(StringComparer.Ordinal) { "grant_scope" },
        ["PolicyDelta"] = new(StringComparer.Ordinal) { "parent_hard_lock", "child_weaken_attempt", "override_granted" },
        ["PolicyDeny"] = new(StringComparer.Ordinal) { "deny_code" },
        ["RouteUpgradeReject"] = new(StringComparer.Ordinal) { "decision_code" },
        ["ObserveOpen"] = new(StringComparer.Ordinal) { "scope_mode" },
        ["ObserveGap"] = new(StringComparer.Ordinal) { "cause" },
        ["ObserveResume"] = new(StringComparer.Ordinal) { "replay_available" },
        ["RouteData"] = new(StringComparer.Ordinal) { "route_mode", "transport_path" }
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
            "TrustInsufficient",
            "GrantMissing",
            "GrantExpired"
        },
        ["HandshakeDeny"] = new(StringComparer.Ordinal)
        {
            "PolicyDenied",
            "TrustInsufficient",
            "GrantMissing",
            "GrantExpired",
            "DisclosureDenied",
            "MediatorUnavailable"
        },
        ["PolicyDeny"] = new(StringComparer.Ordinal)
        {
            "PolicyDenied",
            "TrustInsufficient",
            "MediatorUnavailable"
        }
    };

    private static readonly Dictionary<string, HashSet<string>> AllowedStateTransitions = new(StringComparer.Ordinal)
    {
        ["ResolveIntent"] = new(StringComparer.Ordinal) { "DiscoverPath" },
        ["DiscoverPath"] = new(StringComparer.Ordinal) { "PolicyEvaluate", "Deny" },
        ["PolicyEvaluate"] = new(StringComparer.Ordinal) { "DisclosurePlan", "Deny", "Completed" },
        ["DisclosurePlan"] = new(StringComparer.Ordinal) { "MediatedHandshake", "Deny" },
        ["MediatedHandshake"] = new(StringComparer.Ordinal) { "RelayedSession", "Deny" },
        ["RelayedSession"] = new(StringComparer.Ordinal) { "DirectUpgradeProbe", "Completed", "Deny" },
        ["DirectUpgradeProbe"] = new(StringComparer.Ordinal) { "DirectSession", "RelayedSession", "Deny" },
        ["DirectSession"] = new(StringComparer.Ordinal) { "RelayedSession", "Completed", "Deny" },
        ["ObserveIdle"] = new(StringComparer.Ordinal) { "ObservePendingAck" },
        ["ObservePendingAck"] = new(StringComparer.Ordinal) { "ObserveActive", "ObserveDenied" },
        ["ObserveActive"] = new(StringComparer.Ordinal) { "ObserveActive", "ObserveGap", "ObserveClosed" },
        ["ObserveGap"] = new(StringComparer.Ordinal) { "ObserveResuming", "ObserveGap", "ObserveClosed" },
        ["ObserveResuming"] = new(StringComparer.Ordinal) { "ObserveActive", "ObserveGap" }
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
        var errors = new List<ConformanceError>();
        var envelopeMessages = BuildEnvelopeMessages(fixture, errors);
        var sliceProfile = NormalizeSliceProfile(fixture, errors);

        ValidateEnvelopeAndSchema(fixture, envelopeMessages, errors);
        ValidateMessageFieldRules(fixture, sliceProfile, errors);

        var context = ExecuteMessageFlow(fixture, sliceProfile, errors);
        var observedStateTrace = context.ObservedStateTrace.AsReadOnly();
        var observedDenyCode = context.ObservedDenyCode;
        var observedUpgradeDecisionCode = context.ObservedUpgradeDecisionCode;
        bool? observedRetryable = null;

        if (observedDenyCode is not null)
        {
            if (!DenyProfiles.TryGetValue(observedDenyCode, out var profile))
            {
                AddError(errors, "L4", "E4002_UNKNOWN_DENY_CODE", $"Unknown deny code '{observedDenyCode}'.");
            }
            else
            {
                observedRetryable = profile.Retryable;
            }
        }

        ValidateStateTrace(fixture, observedStateTrace, errors);
        ValidateExpectedOutcome(fixture, observedDenyCode, observedUpgradeDecisionCode, observedRetryable, errors);
        var observedBridgeTransits = context.BridgeTransitTraces.AsReadOnly();
        ValidateAssertions(fixture, envelopeMessages, observedStateTrace, observedBridgeTransits, observedDenyCode, observedUpgradeDecisionCode, observedRetryable, errors);

        var errorMessages = errors.Select(FormatError).ToList();
        return new VectorResult
        {
            Id = fixture.Id,
            Passed = errors.Count == 0,
            ErrorDetails = errors,
            Errors = errorMessages,
            ObservedStateTrace = observedStateTrace,
            ObservedBridgeTransits = observedBridgeTransits,
            ObservedDenyCode = observedDenyCode,
            ObservedUpgradeDecisionCode = observedUpgradeDecisionCode,
            ObservedRetryable = observedRetryable,
            EffectivePassed = errors.Count == 0
        };
    }

    private static List<EnvelopeMessage> BuildEnvelopeMessages(FixtureCase fixture, List<ConformanceError> errors)
    {
        var result = new List<EnvelopeMessage>(fixture.Messages.Count);
        for (var i = 0; i < fixture.Messages.Count; i++)
        {
            var message = fixture.Messages[i];
            if (string.IsNullOrWhiteSpace(message.Type))
            {
                AddError(errors, "L1", "E1001_EMPTY_MESSAGE_TYPE", $"Fixture {fixture.Id} message index {i} has empty type.");
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
        List<ConformanceError> errors)
    {
        foreach (var envelope in envelopeMessages)
        {
            if (string.IsNullOrWhiteSpace(envelope.MsgType))
            {
                AddError(errors, "L1", "E1001_EMPTY_MESSAGE_TYPE", $"{fixture.Id} contains empty msg_type.");
            }

            if (string.IsNullOrWhiteSpace(envelope.MsgId))
            {
                AddError(errors, "L1", "E1003_MISSING_MSG_ID", $"{fixture.Id}/{envelope.MsgType} missing msg_id.");
            }

            if (string.IsNullOrWhiteSpace(envelope.TraceId))
            {
                AddError(errors, "L1", "E1004_MISSING_TRACE_ID", $"{fixture.Id}/{envelope.MsgType} missing trace_id.");
            }

            if (envelope.TtlMs <= 0)
            {
                AddError(errors, "L1", "E1005_INVALID_TTL_MS", $"{fixture.Id}/{envelope.MsgType} has invalid ttl_ms.");
            }

            if (!KnownMessageTypes.Contains(envelope.MsgType))
            {
                AddError(errors, "L1", "E1002_UNKNOWN_MESSAGE_TYPE", $"{fixture.Id}/{envelope.MsgType} is unknown.");
            }
        }
    }

    private static void ValidateMessageFieldRules(FixtureCase fixture, string sliceProfile, List<ConformanceError> errors)
    {
        foreach (var message in fixture.Messages)
        {
            if (RequiredBodyFieldsByType.TryGetValue(message.Type, out var requiredFields))
            {
                if (message.Body is null || message.Body.Value.ValueKind != JsonValueKind.Object)
                {
                    AddError(errors, "L2", "E2001_MISSING_REQUIRED_BODY", $"{fixture.Id}/{message.Type} missing required body.");
                    continue;
                }

                foreach (var field in requiredFields)
                {
                    if (!message.Body.Value.TryGetProperty(field, out _))
                    {
                        AddError(errors, "L2", "E2002_MISSING_REQUIRED_FIELD", $"{fixture.Id}/{message.Type} missing required field '{field}'.");
                    }
                }
            }

            if (message.Type is "ResolveDeny" or "HandshakeDeny" or "PolicyDeny")
            {
                var denyCode = GetBodyString(message, "deny_code");
                if (denyCode is null)
                {
                    continue;
                }

                if (AllowedDenyCodesByMessageType.TryGetValue(message.Type, out var allowedCodes) &&
                    !allowedCodes.Contains(denyCode))
                {
                    AddError(errors, "L4", "E4001_DENY_CODE_NOT_ALLOWED", $"{fixture.Id}/{message.Type} deny code '{denyCode}' is not allowed.");
                }
            }

            ValidateExprNormVersionFields(fixture, message, errors);
            ValidateDenyPolicyReferenceFields(fixture, message, errors);

            if (IsDirectUpgradeProfile(sliceProfile) &&
                string.Equals(message.Type, "RouteUpgradeProbe", StringComparison.Ordinal))
            {
                ValidateS2RouteUpgradeProbeFields(fixture, message, errors);
            }

            if (string.Equals(sliceProfile, SliceProfileS3, StringComparison.Ordinal) &&
                string.Equals(message.Type, "ResolveRequest", StringComparison.Ordinal))
            {
                ValidateS3ResolveRequestFields(fixture, message, errors);
            }

            if (string.Equals(sliceProfile, SliceProfileS4, StringComparison.Ordinal))
            {
                ValidateS4ObserveFields(fixture, message, errors);
            }

            if (string.Equals(sliceProfile, SliceProfileS5, StringComparison.Ordinal))
            {
                ValidateS5PolicyFields(fixture, message, errors);
            }

            if (string.Equals(sliceProfile, SliceProfileM1S1, StringComparison.Ordinal))
            {
                ValidateM1S1WireClosureFields(fixture, message, errors);
            }

            if (string.Equals(sliceProfile, SliceProfileM1S2, StringComparison.Ordinal))
            {
                ValidateM1S2RuntimeBridgeFields(fixture, message, errors);
            }
        }
    }

    private static string NormalizeSliceProfile(FixtureCase fixture, List<ConformanceError> errors)
    {
        var rawProfile = string.IsNullOrWhiteSpace(fixture.SliceProfile) ? SliceProfileS1 : fixture.SliceProfile.Trim();
        if (KnownSliceProfiles.Contains(rawProfile))
        {
            return rawProfile;
        }

        AddError(errors, "L1", "E1006_UNKNOWN_SLICE_PROFILE", $"{fixture.Id} uses unsupported slice_profile '{rawProfile}'. Falling back to '{SliceProfileS1}'.");
        return SliceProfileS1;
    }

    private static bool IsDirectUpgradeProfile(string sliceProfile)
    {
        return string.Equals(sliceProfile, SliceProfileS2, StringComparison.Ordinal)
            || string.Equals(sliceProfile, SliceProfileM1S2, StringComparison.Ordinal);
    }

    private static void ValidateS2RouteUpgradeProbeFields(FixtureCase fixture, FixtureMessage message, List<ConformanceError> errors)
    {
        if (message.Body is null || message.Body.Value.ValueKind != JsonValueKind.Object)
        {
            AddError(errors, "L2", "E2001_MISSING_REQUIRED_BODY", $"{fixture.Id}/{message.Type} missing required body.");
            return;
        }

        ValidateRequiredBooleanField(fixture, message, "policy_allowed", errors);
        ValidateRequiredBooleanField(fixture, message, "disclosure_allowed", errors);
        ValidateRequiredBooleanField(fixture, message, "trust_sufficient", errors);

        if (!message.Body.Value.TryGetProperty("grant_status", out var grantStatusElement))
        {
            AddError(errors, "L2", "E2002_MISSING_REQUIRED_FIELD", $"{fixture.Id}/{message.Type} missing required field 'grant_status'.");
            return;
        }

        if (grantStatusElement.ValueKind != JsonValueKind.String)
        {
            AddError(errors, "L2", "E2003_INVALID_FIELD_TYPE", $"{fixture.Id}/{message.Type} field 'grant_status' must be a string.");
            return;
        }

        var grantStatus = grantStatusElement.GetString();
        if (string.IsNullOrWhiteSpace(grantStatus) || !KnownGrantStatuses.Contains(grantStatus))
        {
            AddError(errors, "L2", "E2004_INVALID_FIELD_VALUE", $"{fixture.Id}/{message.Type} field 'grant_status' value '{grantStatus ?? "null"}' is invalid.");
        }
    }

    private static void ValidateRequiredBooleanField(FixtureCase fixture, FixtureMessage message, string fieldName, List<ConformanceError> errors)
    {
        if (!message.Body!.Value.TryGetProperty(fieldName, out var property))
        {
            AddError(errors, "L2", "E2002_MISSING_REQUIRED_FIELD", $"{fixture.Id}/{message.Type} missing required field '{fieldName}'.");
            return;
        }

        if (property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            AddError(errors, "L2", "E2003_INVALID_FIELD_TYPE", $"{fixture.Id}/{message.Type} field '{fieldName}' must be a boolean.");
        }
    }

    private static string? DetermineS2UpgradeRejectCode(FixtureMessage message)
    {
        var policyAllowed = GetBodyBoolean(message, "policy_allowed") ?? true;
        if (!policyAllowed)
        {
            return "PolicyDenied";
        }

        var disclosureAllowed = GetBodyBoolean(message, "disclosure_allowed") ?? true;
        if (!disclosureAllowed)
        {
            return "DisclosureDenied";
        }

        var grantStatus = GetBodyString(message, "grant_status") ?? "not_required";
        if (string.Equals(grantStatus, "missing", StringComparison.Ordinal))
        {
            return "GrantMissing";
        }

        if (string.Equals(grantStatus, "expired", StringComparison.Ordinal))
        {
            return "GrantExpired";
        }

        var trustSufficient = GetBodyBoolean(message, "trust_sufficient") ?? true;
        if (!trustSufficient)
        {
            return "TrustInsufficient";
        }

        return null;
    }

    private static void ValidateS3ResolveRequestFields(FixtureCase fixture, FixtureMessage message, List<ConformanceError> errors)
    {
        var operationClass = GetBodyString(message, "operation_class");
        if (!string.Equals(operationClass, "endpoint", StringComparison.Ordinal))
        {
            return;
        }

        if (message.Body is null || message.Body.Value.ValueKind != JsonValueKind.Object)
        {
            AddError(errors, "L2", "E2001_MISSING_REQUIRED_BODY", $"{fixture.Id}/{message.Type} missing required body.");
            return;
        }

        if (!message.Body.Value.TryGetProperty("endpoint_directory_mode", out var directoryModeElement))
        {
            AddError(errors, "L2", "E2002_MISSING_REQUIRED_FIELD", $"{fixture.Id}/{message.Type} missing required field 'endpoint_directory_mode'.");
        }
        else if (directoryModeElement.ValueKind != JsonValueKind.String)
        {
            AddError(errors, "L2", "E2003_INVALID_FIELD_TYPE", $"{fixture.Id}/{message.Type} field 'endpoint_directory_mode' must be a string.");
        }
        else if (!KnownEndpointDirectoryModes.Contains(directoryModeElement.GetString() ?? string.Empty))
        {
            AddError(errors, "L2", "E2004_INVALID_FIELD_VALUE", $"{fixture.Id}/{message.Type} field 'endpoint_directory_mode' value '{directoryModeElement.GetString() ?? "null"}' is invalid.");
        }

        if (!message.Body.Value.TryGetProperty("endpoint_grant_status", out var grantStatusElement))
        {
            AddError(errors, "L2", "E2002_MISSING_REQUIRED_FIELD", $"{fixture.Id}/{message.Type} missing required field 'endpoint_grant_status'.");
        }
        else if (grantStatusElement.ValueKind != JsonValueKind.String)
        {
            AddError(errors, "L2", "E2003_INVALID_FIELD_TYPE", $"{fixture.Id}/{message.Type} field 'endpoint_grant_status' must be a string.");
        }
        else if (!KnownGrantStatuses.Contains(grantStatusElement.GetString() ?? string.Empty))
        {
            AddError(errors, "L2", "E2004_INVALID_FIELD_VALUE", $"{fixture.Id}/{message.Type} field 'endpoint_grant_status' value '{grantStatusElement.GetString() ?? "null"}' is invalid.");
        }

        if (!message.Body.Value.TryGetProperty("endpoint_disclosure_allowed", out var disclosureElement))
        {
            AddError(errors, "L2", "E2002_MISSING_REQUIRED_FIELD", $"{fixture.Id}/{message.Type} missing required field 'endpoint_disclosure_allowed'.");
        }
        else if (disclosureElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            AddError(errors, "L2", "E2003_INVALID_FIELD_TYPE", $"{fixture.Id}/{message.Type} field 'endpoint_disclosure_allowed' must be a boolean.");
        }
    }

    private static void ValidateS4ObserveFields(FixtureCase fixture, FixtureMessage message, List<ConformanceError> errors)
    {
        if (string.Equals(message.Type, "ObserveOpen", StringComparison.Ordinal))
        {
            var scopeMode = GetBodyString(message, "scope_mode");
            if (scopeMode is not null && !KnownObserveScopeModes.Contains(scopeMode))
            {
                AddError(errors, "L2", "E2004_INVALID_FIELD_VALUE", $"{fixture.Id}/{message.Type} field 'scope_mode' value '{scopeMode}' is invalid.");
            }

            var followMoves = GetBodyBoolean(message, "follow_moves");
            if (followMoves is null && HasBodyProperty(message, "follow_moves"))
            {
                AddError(errors, "L2", "E2003_INVALID_FIELD_TYPE", $"{fixture.Id}/{message.Type} field 'follow_moves' must be a boolean.");
            }
        }

        if (string.Equals(message.Type, "ObserveGap", StringComparison.Ordinal))
        {
            var cause = GetBodyString(message, "cause");
            if (cause is not null && !KnownObserveGapCauses.Contains(cause))
            {
                AddError(errors, "L2", "E2004_INVALID_FIELD_VALUE", $"{fixture.Id}/{message.Type} field 'cause' value '{cause}' is invalid.");
            }
        }

        if (string.Equals(message.Type, "ObserveResume", StringComparison.Ordinal))
        {
            var replayAvailable = GetBodyBoolean(message, "replay_available");
            if (replayAvailable is null)
            {
                AddError(errors, "L2", "E2003_INVALID_FIELD_TYPE", $"{fixture.Id}/{message.Type} field 'replay_available' must be a boolean.");
            }
        }
    }

    private static void ValidateS5PolicyFields(FixtureCase fixture, FixtureMessage message, List<ConformanceError> errors)
    {
        if (!string.Equals(message.Type, "PolicyDelta", StringComparison.Ordinal))
        {
            return;
        }

        if (message.Body is null || message.Body.Value.ValueKind != JsonValueKind.Object)
        {
            AddError(errors, "L2", "E2001_MISSING_REQUIRED_BODY", $"{fixture.Id}/{message.Type} missing required body.");
            return;
        }

        ValidateRequiredBooleanField(fixture, message, "parent_hard_lock", errors);
        ValidateRequiredBooleanField(fixture, message, "child_weaken_attempt", errors);
        ValidateRequiredBooleanField(fixture, message, "override_granted", errors);
    }

    private static void ValidateExprNormVersionFields(FixtureCase fixture, FixtureMessage message, List<ConformanceError> errors)
    {
        if (!string.Equals(message.Type, "ResolveRequest", StringComparison.Ordinal) &&
            !string.Equals(message.Type, "ResolveResponse", StringComparison.Ordinal) &&
            !string.Equals(message.Type, "ResolveReferral", StringComparison.Ordinal))
        {
            return;
        }

        if (message.Body is null || message.Body.Value.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var hasExprNorm = message.Body.Value.TryGetProperty("expr_norm", out var exprNormElement);
        var hasExprNormVersion = message.Body.Value.TryGetProperty("expr_norm_v", out var exprNormVersionElement);

        if (!hasExprNorm)
        {
            if (hasExprNormVersion)
            {
                AddError(errors, "L2", "E2064_EXPR_NORM_V_WITHOUT_EXPR_NORM", $"{fixture.Id}/{message.Type} includes 'expr_norm_v' without 'expr_norm'.");
            }

            return;
        }

        if (exprNormElement.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(exprNormElement.GetString()))
        {
            AddError(errors, "L2", "E2062_EXPR_NORM_VERSION_INVALID_TYPE", $"{fixture.Id}/{message.Type} field 'expr_norm' must be a non-empty string when present.");
            return;
        }

        if (!hasExprNormVersion)
        {
            AddError(errors, "L2", "E2061_EXPR_NORM_VERSION_REQUIRED", $"{fixture.Id}/{message.Type} requires 'expr_norm_v' when 'expr_norm' is present.");
            return;
        }

        if (exprNormVersionElement.ValueKind != JsonValueKind.Number || !exprNormVersionElement.TryGetInt32(out var exprNormVersion))
        {
            AddError(errors, "L2", "E2062_EXPR_NORM_VERSION_INVALID_TYPE", $"{fixture.Id}/{message.Type} field 'expr_norm_v' must be an integer.");
            return;
        }

        if (exprNormVersion != 1)
        {
            AddError(errors, "L2", "E2063_EXPR_NORM_VERSION_UNSUPPORTED", $"{fixture.Id}/{message.Type} field 'expr_norm_v' value '{exprNormVersion}' is unsupported. Expected '1'.");
        }
    }

    private static void ValidateDenyPolicyReferenceFields(FixtureCase fixture, FixtureMessage message, List<ConformanceError> errors)
    {
        if (!string.Equals(message.Type, "ResolveDeny", StringComparison.Ordinal) &&
            !string.Equals(message.Type, "HandshakeDeny", StringComparison.Ordinal) &&
            !string.Equals(message.Type, "PolicyDeny", StringComparison.Ordinal))
        {
            return;
        }

        if (message.Body is null || message.Body.Value.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var denyCode = GetBodyString(message, "deny_code");
        if (denyCode is null || !PolicyCausalDenyCodes.Contains(denyCode))
        {
            return;
        }

        if (!message.Body.Value.TryGetProperty("policy_ref", out var policyRefElement))
        {
            AddError(errors, "L2", "E2065_POLICY_REF_REQUIRED_FOR_DENY", $"{fixture.Id}/{message.Type} deny_code '{denyCode}' requires 'policy_ref'.");
            return;
        }

        if (policyRefElement.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(policyRefElement.GetString()))
        {
            AddError(errors, "L2", "E2066_POLICY_REF_INVALID_TYPE", $"{fixture.Id}/{message.Type} field 'policy_ref' must be a non-empty string.");
        }
    }

    private static void ValidateM1S1WireClosureFields(FixtureCase fixture, FixtureMessage message, List<ConformanceError> errors)
    {
        if (message.Body is null || message.Body.Value.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        ValidateOptionalTypedIdentifierField(fixture, message, "policy_ref", errors);
        ValidateOptionalTypedIdentifierField(fixture, message, "grant_ref", errors);
        ValidateOptionalTypedIdentifierField(fixture, message, "endpoint_disclosure_grant_ref", errors);
        ValidateOptionalTypedIdentifierField(fixture, message, "fallback_route_ref", errors);
        ValidateOptionalTypedIdentifierField(fixture, message, "active_route_ref", errors);
        ValidateOptionalTypedIdentifierField(fixture, message, "relation_token_ref", errors);

        if (!string.Equals(message.Type, "HandshakeAccept", StringComparison.Ordinal))
        {
            return;
        }

        if (!message.Body.Value.TryGetProperty("token_transport", out var tokenTransportElement))
        {
            AddError(errors, "L2", "E2067_TOKEN_TRANSPORT_REQUIRED", $"{fixture.Id}/{message.Type} requires 'token_transport' ('reference' or 'inline').");
            return;
        }

        if (tokenTransportElement.ValueKind != JsonValueKind.String)
        {
            AddError(errors, "L2", "E2068_TOKEN_TRANSPORT_INVALID", $"{fixture.Id}/{message.Type} field 'token_transport' must be a string.");
            return;
        }

        var tokenTransport = tokenTransportElement.GetString() ?? string.Empty;
        if (!KnownTokenTransportModes.Contains(tokenTransport))
        {
            AddError(errors, "L2", "E2068_TOKEN_TRANSPORT_INVALID", $"{fixture.Id}/{message.Type} field 'token_transport' value '{tokenTransport}' is invalid.");
            return;
        }

        if (!message.Body.Value.TryGetProperty("relation_token_ref", out var relationTokenRefElement))
        {
            AddError(errors, "L2", "E2069_TOKEN_REF_REQUIRED", $"{fixture.Id}/{message.Type} requires 'relation_token_ref'.");
            return;
        }

        if (relationTokenRefElement.ValueKind != JsonValueKind.String || !IsTypedIdentifier(relationTokenRefElement.GetString() ?? string.Empty))
        {
            AddError(errors, "L2", "E2069_TOKEN_REF_REQUIRED", $"{fixture.Id}/{message.Type} field 'relation_token_ref' must be a typed identifier.");
        }

        if (!message.Body.Value.TryGetProperty("relation_token_cid", out var relationTokenCidElement))
        {
            AddError(errors, "L2", "E2070_TOKEN_CID_REQUIRED", $"{fixture.Id}/{message.Type} requires 'relation_token_cid'.");
            return;
        }

        if (relationTokenCidElement.ValueKind != JsonValueKind.String)
        {
            AddError(errors, "L2", "E2071_TOKEN_CID_INVALID", $"{fixture.Id}/{message.Type} field 'relation_token_cid' must be a string.");
            return;
        }

        var relationTokenCid = relationTokenCidElement.GetString() ?? string.Empty;
        if (!RelationTokenCidRegex.IsMatch(relationTokenCid))
        {
            AddError(errors, "L2", "E2071_TOKEN_CID_INVALID", $"{fixture.Id}/{message.Type} field 'relation_token_cid' value '{relationTokenCid}' is invalid. Expected 'sha256:<hex>'.");
        }

        var hasTokenBlob = message.Body.Value.TryGetProperty("relation_token_blob", out var tokenBlobElement);
        if (string.Equals(tokenTransport, "inline", StringComparison.Ordinal))
        {
            if (!hasTokenBlob || tokenBlobElement.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(tokenBlobElement.GetString()))
            {
                AddError(errors, "L2", "E2072_TOKEN_BLOB_REQUIRED_INLINE", $"{fixture.Id}/{message.Type} requires non-empty 'relation_token_blob' when token_transport='inline'.");
            }
        }
        else
        {
            if (hasTokenBlob && !(tokenBlobElement.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(tokenBlobElement.GetString())))
            {
                AddError(errors, "L2", "E2073_TOKEN_BLOB_FORBIDDEN_REFERENCE", $"{fixture.Id}/{message.Type} must not include 'relation_token_blob' when token_transport='reference'.");
            }
        }
    }

    private static void ValidateM1S2RuntimeBridgeFields(FixtureCase fixture, FixtureMessage message, List<ConformanceError> errors)
    {
        if (!string.Equals(message.Type, "RouteData", StringComparison.Ordinal))
        {
            return;
        }

        if (message.Body is null || message.Body.Value.ValueKind != JsonValueKind.Object)
        {
            AddError(errors, "L2", "E2001_MISSING_REQUIRED_BODY", $"{fixture.Id}/{message.Type} missing required body.");
            return;
        }

        var routeMode = GetBodyString(message, "route_mode");
        if (routeMode is null)
        {
            AddError(errors, "L2", "E2002_MISSING_REQUIRED_FIELD", $"{fixture.Id}/{message.Type} missing required field 'route_mode'.");
        }
        else if (routeMode is not ("parent_mediated" or "relay_mediated" or "anonymous_relay" or "direct_upgraded"))
        {
            AddError(errors, "L2", "E2004_INVALID_FIELD_VALUE", $"{fixture.Id}/{message.Type} field 'route_mode' value '{routeMode}' is invalid.");
        }

        var transportPath = GetBodyString(message, "transport_path");
        if (transportPath is null)
        {
            AddError(errors, "L2", "E2002_MISSING_REQUIRED_FIELD", $"{fixture.Id}/{message.Type} missing required field 'transport_path'.");
        }
        else if (!KnownTransportPaths.Contains(transportPath))
        {
            AddError(errors, "L2", "E2004_INVALID_FIELD_VALUE", $"{fixture.Id}/{message.Type} field 'transport_path' value '{transportPath}' is invalid.");
        }
    }

    private static void ValidateOptionalTypedIdentifierField(FixtureCase fixture, FixtureMessage message, string fieldName, List<ConformanceError> errors)
    {
        if (!message.Body!.Value.TryGetProperty(fieldName, out var element))
        {
            return;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            AddError(errors, "L2", "E2080_TYPED_IDENTIFIER_INVALID", $"{fixture.Id}/{message.Type} field '{fieldName}' must be a typed identifier string.");
            return;
        }

        var value = element.GetString() ?? string.Empty;
        if (!IsTypedIdentifier(value))
        {
            AddError(errors, "L2", "E2080_TYPED_IDENTIFIER_INVALID", $"{fixture.Id}/{message.Type} field '{fieldName}' value '{value}' is not a valid typed identifier.");
        }
    }

    private static bool IsTypedIdentifier(string value)
    {
        if (!TypedIdentifierRegex.IsMatch(value))
        {
            return false;
        }

        var separator = value.IndexOf(':');
        if (separator <= 0)
        {
            return false;
        }

        var prefix = value[..separator];
        return TypedIdentifierPrefixes.Contains(prefix);
    }

    private static OperationContext ExecuteMessageFlow(FixtureCase fixture, string sliceProfile, List<ConformanceError> errors)
    {
        var context = new OperationContext
        {
            SliceProfile = sliceProfile
        };

        foreach (var message in fixture.Messages)
        {
            ProcessMessage(fixture.Id, message, context, errors);
        }

        if (context.UpgradeProbePending)
        {
            if (string.Equals(sliceProfile, SliceProfileS1, StringComparison.Ordinal))
            {
                AddError(errors, "L3", "E3014_UPGRADE_PROBE_NOT_REJECTED", $"{fixture.Id} requires RouteUpgradeReject after RouteUpgradeProbe in S1 mediated-only mode.");
                context.ObservedDenyCode ??= "UpgradeRejected";
                context.ObservedUpgradeDecisionCode ??= "UpgradeRejected";
                ForceTerminalDeny(context);
            }
            else
            {
                AddError(errors, "L3", "E3028_S2_UPGRADE_PROBE_UNRESOLVED", $"{fixture.Id} requires RouteUpgradeAccept or RouteUpgradeReject after RouteUpgradeProbe in direct-upgrade profile.");
                context.ObservedDenyCode ??= "UpgradeRejected";
                context.ObservedUpgradeDecisionCode ??= "UpgradeRejected";
                ForceTerminalDeny(context);
            }
        }

        if (IsDirectUpgradeProfile(sliceProfile) && context.UpgradeRejectSeen)
        {
            if (!context.UpgradeFallbackRestored)
            {
                AddError(errors, "L3", "E3026_S2_FALLBACK_NOT_RESTORED", $"{fixture.Id} rejected direct upgrade but did not restore RelayedSession.");
            }
        }

        if (string.Equals(sliceProfile, SliceProfileS5, StringComparison.Ordinal) && context.PolicyDenyRequired)
        {
            AddError(errors, "L3", "E3053_S5_POLICY_DENY_MISSING", $"{fixture.Id} requires PolicyDeny after PolicyDelta hard-lock violation.");
            context.ObservedDenyCode ??= "PolicyDenied";
            ForceTerminalDeny(context);
        }

        if (!IsTerminal(context.CurrentState))
        {
            if (string.Equals(context.CurrentState, "RelayedSession", StringComparison.Ordinal) ||
                string.Equals(context.CurrentState, "DirectSession", StringComparison.Ordinal))
            {
                TryTransition(fixture.Id, context, "Completed", errors);
            }
        }

        if (context.ObservedStateTrace.Count == 0)
        {
            AddError(errors, "L3", "E3011_EMPTY_OBSERVED_TRACE", $"{fixture.Id} could not derive observed state trace from message flow.");
        }

        return context;
    }

    private static void ProcessMessage(
        string fixtureId,
        FixtureMessage message,
        OperationContext context,
        List<ConformanceError> errors)
    {
        if (IsTerminal(context.CurrentState))
        {
            AddError(errors, "L3", "E3006_POST_TERMINAL_MESSAGE", $"{fixtureId}/{message.Type} appears after terminal state '{context.CurrentState}'.");
            return;
        }

        switch (message.Type)
        {
            case "ResolveRequest":
            {
                if (context.ResolveStarted || context.HandshakeStarted || context.PolicyOperationStarted)
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3015_OPERATION_ALREADY_STARTED", "TrustInsufficient", $"{fixtureId}/{message.Type} starts a new operation while another operation context is active.");
                    break;
                }

                context.ResolveStarted = true;
                context.RequiresSelectorHints = GetBodyBoolean(message, "requires_selector_hints") ?? false;
                context.HasSelectorHints = HasNonEmptyStringArray(message, "selector_hints");

                if (string.Equals(context.SliceProfile, SliceProfileS3, StringComparison.Ordinal))
                {
                    var operationClass = GetBodyString(message, "operation_class");
                    context.EndpointOperationActive = string.Equals(operationClass, "endpoint", StringComparison.Ordinal);
                    if (context.EndpointOperationActive)
                    {
                        context.EndpointDirectoryMode = GetBodyString(message, "endpoint_directory_mode") ?? "plaintext";
                        context.EndpointGrantStatus = GetBodyString(message, "endpoint_grant_status") ?? "not_required";
                        context.EndpointDisclosureAllowed = GetBodyBoolean(message, "endpoint_disclosure_allowed") ?? true;
                    }
                }

                if (context.CurrentState is null)
                {
                    SetState(context, "ResolveIntent");
                }
                else
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3007_INVALID_STATE_TRANSITION", "TrustInsufficient", $"{fixtureId}/{message.Type} cannot start from state '{context.CurrentState}'.");
                    break;
                }

                TryTransition(fixtureId, context, "DiscoverPath", errors);
                break;
            }
            case "ResolveReferral":
            {
                if (!context.ResolveStarted)
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3001_RESOLVE_MESSAGE_BEFORE_REQUEST", "TrustInsufficient", $"{fixtureId}/{message.Type} appears before ResolveRequest.");
                    break;
                }

                if (!StateIs(context, "DiscoverPath", "PolicyEvaluate"))
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3007_INVALID_STATE_TRANSITION", "TrustInsufficient", $"{fixtureId}/{message.Type} invalid from state '{context.CurrentState}'.");
                    break;
                }

                TryTransition(fixtureId, context, "PolicyEvaluate", errors);
                break;
            }
            case "ResolveResponse":
            {
                if (!context.ResolveStarted)
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3001_RESOLVE_MESSAGE_BEFORE_REQUEST", "TrustInsufficient", $"{fixtureId}/{message.Type} appears before ResolveRequest.");
                    break;
                }

                if (context.RequiresSelectorHints && !context.HasSelectorHints)
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3017_AMBIGUOUS_SELECTOR_HINTS_REQUIRED", "AmbiguousResolution", $"{fixtureId}/{message.Type} is invalid when selector hints are required but missing or empty.");
                    break;
                }

                if (string.Equals(context.SliceProfile, SliceProfileS3, StringComparison.Ordinal) && context.EndpointOperationActive)
                {
                    if (TryEvaluateS3EndpointGateFailure(fixtureId, context, errors))
                    {
                        break;
                    }
                }

                if (string.Equals(context.CurrentState, "DiscoverPath", StringComparison.Ordinal))
                {
                    TryTransition(fixtureId, context, "PolicyEvaluate", errors);
                }
                else if (!string.Equals(context.CurrentState, "PolicyEvaluate", StringComparison.Ordinal))
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3007_INVALID_STATE_TRANSITION", "TrustInsufficient", $"{fixtureId}/{message.Type} invalid from state '{context.CurrentState}'.");
                    break;
                }

                TryTransition(fixtureId, context, "DisclosurePlan", errors);
                TryTransition(fixtureId, context, "MediatedHandshake", errors);
                TryTransition(fixtureId, context, "RelayedSession", errors);
                TryTransition(fixtureId, context, "Completed", errors);
                break;
            }
            case "ResolveDeny":
            {
                if (!context.ResolveStarted)
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3001_RESOLVE_MESSAGE_BEFORE_REQUEST", "TrustInsufficient", $"{fixtureId}/{message.Type} appears before ResolveRequest.");
                    break;
                }

                var denyCode = GetBodyString(message, "deny_code");
                if (denyCode is not null)
                {
                    context.ObservedDenyCode = denyCode;
                }

                ForceTerminalDeny(context);
                break;
            }
            case "HandshakeInit":
            {
                if (context.HandshakeStarted)
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3015_OPERATION_ALREADY_STARTED", "TrustInsufficient", $"{fixtureId}/{message.Type} repeats an already-started handshake operation.");
                    break;
                }

                if (context.ResolveStarted && !string.Equals(context.CurrentState, "DisclosurePlan", StringComparison.Ordinal))
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3007_INVALID_STATE_TRANSITION", "TrustInsufficient", $"{fixtureId}/{message.Type} invalid from state '{context.CurrentState}'.");
                    break;
                }

                context.HandshakeStarted = true;
                context.HandshakeInitSeen = true;
                if (context.CurrentState is null)
                {
                    SetState(context, "MediatedHandshake");
                }
                else
                {
                    TryTransition(fixtureId, context, "MediatedHandshake", errors);
                }

                break;
            }
            case "HandshakeChallenge":
            {
                if (!context.HandshakeInitSeen)
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3002_HANDSHAKE_CHALLENGE_BEFORE_INIT", "TrustInsufficient", $"{fixtureId}/{message.Type} appears before HandshakeInit.");
                    break;
                }

                if (!StateIs(context, "MediatedHandshake"))
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3007_INVALID_STATE_TRANSITION", "TrustInsufficient", $"{fixtureId}/{message.Type} invalid from state '{context.CurrentState}'.");
                    break;
                }

                context.HandshakeChallengeSeen = true;
                break;
            }
            case "HandshakeProof":
            {
                if (!context.HandshakeChallengeSeen)
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3003_HANDSHAKE_PROOF_BEFORE_CHALLENGE", "TrustInsufficient", $"{fixtureId}/{message.Type} appears before HandshakeChallenge.");
                    break;
                }

                if (!StateIs(context, "MediatedHandshake"))
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3007_INVALID_STATE_TRANSITION", "TrustInsufficient", $"{fixtureId}/{message.Type} invalid from state '{context.CurrentState}'.");
                    break;
                }

                context.HandshakeProofSeen = true;
                break;
            }
            case "HandshakeAccept":
            {
                if (!context.HandshakeProofSeen)
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3004_HANDSHAKE_TERMINAL_BEFORE_PROOF", "TrustInsufficient", $"{fixtureId}/{message.Type} appears before HandshakeProof.");
                    break;
                }

                if (!StateIs(context, "MediatedHandshake"))
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3007_INVALID_STATE_TRANSITION", "TrustInsufficient", $"{fixtureId}/{message.Type} invalid from state '{context.CurrentState}'.");
                    break;
                }

                TryTransition(fixtureId, context, "RelayedSession", errors);
                break;
            }
            case "HandshakeDeny":
            {
                if (!context.HandshakeProofSeen)
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3004_HANDSHAKE_TERMINAL_BEFORE_PROOF", "TrustInsufficient", $"{fixtureId}/{message.Type} appears before HandshakeProof.");
                    break;
                }

                if (!StateIs(context, "MediatedHandshake"))
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3007_INVALID_STATE_TRANSITION", "TrustInsufficient", $"{fixtureId}/{message.Type} invalid from state '{context.CurrentState}'.");
                    break;
                }

                var denyCode = GetBodyString(message, "deny_code");
                if (denyCode is not null)
                {
                    context.ObservedDenyCode = denyCode;
                }

                ForceTerminalDeny(context);
                break;
            }
            case "GrantPresent":
            {
                if (!context.ResolveStarted)
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3031_GRANT_MESSAGE_BEFORE_REQUEST", "TrustInsufficient", $"{fixtureId}/{message.Type} appears before ResolveRequest.");
                    break;
                }

                if (!string.Equals(context.SliceProfile, SliceProfileS3, StringComparison.Ordinal))
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3032_GRANT_MESSAGE_OUTSIDE_S3", "TrustInsufficient", $"{fixtureId}/{message.Type} is only supported in S3 endpoint-grant flow.");
                    break;
                }

                if (!context.EndpointOperationActive)
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3037_GRANT_MESSAGE_NON_ENDPOINT_OPERATION", "TrustInsufficient", $"{fixtureId}/{message.Type} is invalid when operation_class is not 'endpoint'.");
                    break;
                }

                context.GrantPresentSeen = true;
                break;
            }
            case "PolicyDelta":
            {
                if (!string.Equals(context.SliceProfile, SliceProfileS5, StringComparison.Ordinal))
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3054_S5_POLICY_MESSAGE_OUTSIDE_PROFILE", "TrustInsufficient", $"{fixtureId}/{message.Type} is only supported in S5 policy-inheritance flow.");
                    break;
                }

                if (context.ResolveStarted || context.HandshakeStarted || context.ObserveSessionStarted || context.PolicyOperationStarted)
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3015_OPERATION_ALREADY_STARTED", "TrustInsufficient", $"{fixtureId}/{message.Type} starts a new operation while another operation context is active.");
                    break;
                }

                context.PolicyOperationStarted = true;
                if (context.CurrentState is null)
                {
                    SetState(context, "PolicyEvaluate");
                }
                else
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3007_INVALID_STATE_TRANSITION", "TrustInsufficient", $"{fixtureId}/{message.Type} invalid from state '{context.CurrentState}'.");
                    break;
                }

                var parentHardLock = GetBodyBoolean(message, "parent_hard_lock") ?? false;
                var childWeakenAttempt = GetBodyBoolean(message, "child_weaken_attempt") ?? false;
                var overrideGranted = GetBodyBoolean(message, "override_granted") ?? false;

                if (parentHardLock && childWeakenAttempt && !overrideGranted)
                {
                    context.PolicyDenyRequired = true;
                    context.ExpectedPolicyDenyCode = "PolicyDenied";
                }
                else
                {
                    context.PolicyDenyRequired = false;
                    context.ExpectedPolicyDenyCode = null;
                    TryTransition(fixtureId, context, "Completed", errors);
                }

                break;
            }
            case "PolicyDeny":
            {
                if (!string.Equals(context.SliceProfile, SliceProfileS5, StringComparison.Ordinal))
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3054_S5_POLICY_MESSAGE_OUTSIDE_PROFILE", "TrustInsufficient", $"{fixtureId}/{message.Type} is only supported in S5 policy-inheritance flow.");
                    break;
                }

                if (!context.PolicyOperationStarted)
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3051_S5_POLICY_DENY_BEFORE_DELTA", "TrustInsufficient", $"{fixtureId}/{message.Type} appears before PolicyDelta.");
                    break;
                }

                if (!StateIs(context, "PolicyEvaluate"))
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3007_INVALID_STATE_TRANSITION", "TrustInsufficient", $"{fixtureId}/{message.Type} invalid from state '{context.CurrentState}'.");
                    break;
                }

                var denyCode = GetBodyString(message, "deny_code");
                var expectedCode = context.ExpectedPolicyDenyCode ?? "PolicyDenied";
                if (!string.Equals(denyCode, expectedCode, StringComparison.Ordinal))
                {
                    AddError(errors, "L3", "E3052_S5_POLICY_DENY_CODE_MISMATCH", $"{fixtureId}/{message.Type} expected deny_code '{expectedCode}' but observed '{denyCode ?? "null"}'.");
                    context.ObservedDenyCode ??= expectedCode;
                    context.PolicyDenyRequired = false;
                    context.ExpectedPolicyDenyCode = null;
                    ForceTerminalDeny(context);
                    break;
                }

                context.ObservedDenyCode = denyCode;
                context.PolicyDenyRequired = false;
                context.ExpectedPolicyDenyCode = null;
                ForceTerminalDeny(context);
                break;
            }
            case "ObserveOpen":
            {
                if (context.ObserveSessionStarted)
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3040_OBSERVE_ALREADY_OPEN", "TrustInsufficient", $"{fixtureId}/{message.Type} repeats an active observe session.");
                    break;
                }

                if (context.ResolveStarted || context.HandshakeStarted)
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3046_OBSERVE_MIXED_WITH_OTHER_OPERATION", "TrustInsufficient", $"{fixtureId}/{message.Type} cannot be mixed with resolve/handshake operation context.");
                    break;
                }

                context.ObserveSessionStarted = true;
                context.ObserveScopeMode = GetBodyString(message, "scope_mode") ?? "subtree";
                context.ObserveFollowMoves = GetBodyBoolean(message, "follow_moves") ?? GetDefaultFollowMoves(context.ObserveScopeMode);

                if (context.CurrentState is null)
                {
                    SetState(context, "ObserveIdle");
                }
                else
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3007_INVALID_STATE_TRANSITION", "TrustInsufficient", $"{fixtureId}/{message.Type} invalid from state '{context.CurrentState}'.");
                    break;
                }

                TryTransition(fixtureId, context, "ObservePendingAck", errors);
                break;
            }
            case "ObserveAck":
            {
                if (!context.ObserveSessionStarted)
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3041_OBSERVE_ACK_BEFORE_OPEN", "TrustInsufficient", $"{fixtureId}/{message.Type} appears before ObserveOpen.");
                    break;
                }

                if (StateIs(context, "ObservePendingAck", "ObserveResuming"))
                {
                    TryTransition(fixtureId, context, "ObserveActive", errors);
                    break;
                }

                RejectWithDeterministicDeny(fixtureId, context, errors, "E3007_INVALID_STATE_TRANSITION", "TrustInsufficient", $"{fixtureId}/{message.Type} invalid from state '{context.CurrentState}'.");
                break;
            }
            case "ObserveEvent":
            {
                if (!context.ObserveSessionStarted)
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3043_OBSERVE_EVENT_BEFORE_ACTIVE", "TrustInsufficient", $"{fixtureId}/{message.Type} appears before ObserveOpen.");
                    break;
                }

                if (!StateIs(context, "ObserveActive"))
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3043_OBSERVE_EVENT_BEFORE_ACTIVE", "TrustInsufficient", $"{fixtureId}/{message.Type} appears before ObserveActive.");
                    break;
                }

                break;
            }
            case "ObserveGap":
            {
                if (!context.ObserveSessionStarted)
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3047_OBSERVE_GAP_BEFORE_OPEN", "TrustInsufficient", $"{fixtureId}/{message.Type} appears before ObserveOpen.");
                    break;
                }

                if (!StateIs(context, "ObserveActive", "ObserveResuming"))
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3007_INVALID_STATE_TRANSITION", "TrustInsufficient", $"{fixtureId}/{message.Type} invalid from state '{context.CurrentState}'.");
                    break;
                }

                TryTransition(fixtureId, context, "ObserveGap", errors);

                var cause = GetBodyString(message, "cause");
                if (string.Equals(cause, "RetentionExpired", StringComparison.Ordinal))
                {
                    context.ObservedDenyCode = "ReplayWindowExpired";
                }

                break;
            }
            case "ObserveResume":
            {
                if (!context.ObserveSessionStarted)
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3044_OBSERVE_RESUME_BEFORE_GAP", "ReplayWindowExpired", $"{fixtureId}/{message.Type} appears before ObserveGap.");
                    break;
                }

                if (!StateIs(context, "ObserveGap"))
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3044_OBSERVE_RESUME_BEFORE_GAP", "ReplayWindowExpired", $"{fixtureId}/{message.Type} appears before ObserveGap.");
                    break;
                }

                var replayAvailable = GetBodyBoolean(message, "replay_available");
                if (replayAvailable is false)
                {
                    context.ObservedDenyCode = "ReplayWindowExpired";
                    break;
                }

                TryTransition(fixtureId, context, "ObserveResuming", errors);
                break;
            }
            case "ObserveClose":
            {
                if (!context.ObserveSessionStarted)
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3048_OBSERVE_CLOSE_BEFORE_OPEN", "TrustInsufficient", $"{fixtureId}/{message.Type} appears before ObserveOpen.");
                    break;
                }

                if (!StateIs(context, "ObserveActive", "ObserveGap"))
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3007_INVALID_STATE_TRANSITION", "TrustInsufficient", $"{fixtureId}/{message.Type} invalid from state '{context.CurrentState}'.");
                    break;
                }

                TryTransition(fixtureId, context, "ObserveClosed", errors);
                break;
            }
            case "RouteUpgradeProbe":
            {
                if (!StateIs(context, "RelayedSession"))
                {
                    var reason = IsDirectUpgradeProfile(context.SliceProfile)
                        ? $"{fixtureId}/{message.Type} is only valid from RelayedSession in direct-upgrade flow."
                        : $"{fixtureId}/{message.Type} is only valid from RelayedSession and is denied in S1 mediated-only mode.";
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3012_DIRECT_UPGRADE_FORBIDDEN", "UpgradeRejected", reason);
                    break;
                }

                if (IsDirectUpgradeProfile(context.SliceProfile))
                {
                    if (!TryTransition(fixtureId, context, "DirectUpgradeProbe", errors))
                    {
                        break;
                    }

                    context.ExpectedUpgradeRejectCode = DetermineS2UpgradeRejectCode(message);
                }
                else
                {
                    context.ExpectedUpgradeRejectCode = "UpgradeRejected";
                }

                context.UpgradeProbePending = true;
                break;
            }
            case "RouteUpgradeReject":
            {
                if (!context.UpgradeProbePending)
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3005_ROUTE_UPGRADE_RESPONSE_BEFORE_PROBE", "TrustInsufficient", $"{fixtureId}/{message.Type} appears before RouteUpgradeProbe.");
                    break;
                }

                context.UpgradeProbePending = false;
                var decisionCode = GetBodyString(message, "decision_code");

                if (IsDirectUpgradeProfile(context.SliceProfile))
                {
                    var expectedRejectCode = context.ExpectedUpgradeRejectCode ?? "UpgradeRejected";
                    if (!string.Equals(decisionCode, expectedRejectCode, StringComparison.Ordinal))
                    {
                        AddError(errors, "L3", "E3024_S2_UPGRADE_REJECT_CODE_MISMATCH", $"{fixtureId}/{message.Type} expected decision_code '{expectedRejectCode}' but observed '{decisionCode ?? "null"}'.");
                        context.ObservedDenyCode ??= expectedRejectCode;
                        context.ObservedUpgradeDecisionCode ??= decisionCode ?? expectedRejectCode;
                        ForceTerminalDeny(context);
                        break;
                    }

                    context.ObservedUpgradeDecisionCode = decisionCode;
                    context.ExpectedUpgradeRejectCode = null;
                    context.UpgradeRejectSeen = true;
                    context.UpgradeFallbackRestored = TryTransition(fixtureId, context, "RelayedSession", errors);
                    break;
                }

                if (!string.Equals(decisionCode, "UpgradeRejected", StringComparison.Ordinal))
                {
                    AddError(errors, "L3", "E3013_UPGRADE_REJECT_CODE_REQUIRED", $"{fixtureId}/{message.Type} must use decision_code 'UpgradeRejected' in S1.");
                    context.ObservedDenyCode ??= "UpgradeRejected";
                    context.ObservedUpgradeDecisionCode ??= "UpgradeRejected";
                    ForceTerminalDeny(context);
                    break;
                }

                context.ObservedUpgradeDecisionCode = decisionCode;
                context.ExpectedUpgradeRejectCode = null;
                break;
            }
            case "RouteUpgradeAccept":
            {
                if (!context.UpgradeProbePending)
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3005_ROUTE_UPGRADE_RESPONSE_BEFORE_PROBE", "TrustInsufficient", $"{fixtureId}/{message.Type} appears before RouteUpgradeProbe.");
                    break;
                }

                context.UpgradeProbePending = false;

                if (IsDirectUpgradeProfile(context.SliceProfile))
                {
                    if (context.ExpectedUpgradeRejectCode is not null)
                    {
                        RejectWithDeterministicDeny(
                            fixtureId,
                            context,
                            errors,
                            "E3023_S2_UPGRADE_ACCEPT_WITH_FAILED_GATES",
                            context.ExpectedUpgradeRejectCode,
                            $"{fixtureId}/{message.Type} cannot accept upgrade when gate outcome is '{context.ExpectedUpgradeRejectCode}'.");
                        context.ObservedUpgradeDecisionCode ??= "UpgradeAccepted";
                        break;
                    }

                    context.ObservedUpgradeDecisionCode = "UpgradeAccepted";
                    context.ExpectedUpgradeRejectCode = null;
                    TryTransition(fixtureId, context, "DirectSession", errors);
                    break;
                }

                RejectWithDeterministicDeny(fixtureId, context, errors, "E3012_DIRECT_UPGRADE_FORBIDDEN", "UpgradeRejected", $"{fixtureId}/{message.Type} is forbidden in S1 mediated-only mode.");
                break;
            }
            case "RouteData":
            {
                if (!string.Equals(context.SliceProfile, SliceProfileM1S2, StringComparison.Ordinal))
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3062_M1S2_ROUTE_DATA_OUTSIDE_PROFILE", "TrustInsufficient", $"{fixtureId}/{message.Type} is only supported in M1-S2 runtime-bridge profile.");
                    break;
                }

                if (!StateIs(context, "RelayedSession", "DirectSession"))
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3063_M1S2_ROUTE_DATA_OUTSIDE_SESSION", "TrustInsufficient", $"{fixtureId}/{message.Type} requires an active relayed or direct session.");
                    break;
                }

                var senderRole = message.Role?.Trim() ?? string.Empty;
                if (!string.Equals(senderRole, "requester", StringComparison.Ordinal) &&
                    !string.Equals(senderRole, "target", StringComparison.Ordinal))
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3067_M1S2_ROUTE_DATA_ROLE_INVALID", "TrustInsufficient", $"{fixtureId}/{message.Type} role '{senderRole}' is invalid for runtime bridge data transfer.");
                    break;
                }

                var transportPath = GetBodyString(message, "transport_path") ?? string.Empty;
                var declaredRouteMode = GetBodyString(message, "route_mode") ?? string.Empty;
                var expectedPath = string.Equals(context.CurrentState, "DirectSession", StringComparison.Ordinal) ? "direct" : "mediated";
                var expectedRouteMode = string.Equals(context.CurrentState, "DirectSession", StringComparison.Ordinal) ? "direct_upgraded" : "parent_mediated";

                if (!string.Equals(transportPath, expectedPath, StringComparison.Ordinal))
                {
                    if (string.Equals(expectedPath, "mediated", StringComparison.Ordinal))
                    {
                        RejectWithDeterministicDeny(fixtureId, context, errors, "E3064_M1S2_DIRECT_PATH_WHILE_MEDIATED", "UpgradeRejected", $"{fixtureId}/{message.Type} attempted direct transport while session is mediated.");
                    }
                    else
                    {
                        RejectWithDeterministicDeny(fixtureId, context, errors, "E3065_M1S2_MEDIATED_PATH_AFTER_DIRECT", "TrustInsufficient", $"{fixtureId}/{message.Type} attempted mediated transport while session is direct.");
                    }

                    break;
                }

                if (!string.Equals(declaredRouteMode, expectedRouteMode, StringComparison.Ordinal))
                {
                    RejectWithDeterministicDeny(fixtureId, context, errors, "E3066_M1S2_ROUTE_MODE_MISMATCH", "TrustInsufficient", $"{fixtureId}/{message.Type} route_mode '{declaredRouteMode}' does not match active session mode '{expectedRouteMode}'.");
                    break;
                }

                context.BridgeTransitTraces.Add(BuildBridgeTransitTrace(senderRole, expectedPath));
                break;
            }
        }
    }

    private static bool TryEvaluateS3EndpointGateFailure(string fixtureId, OperationContext context, List<ConformanceError> errors)
    {
        if (!context.EndpointDisclosureAllowed)
        {
            RejectWithDeterministicDeny(fixtureId, context, errors, "E3033_S3_ENDPOINT_DISCLOSURE_DENIED", "DisclosureDenied", $"{fixtureId}/ResolveResponse endpoint disclosure is blocked by policy.");
            return true;
        }

        if (!string.Equals(context.EndpointDirectoryMode, "encrypted", StringComparison.Ordinal))
        {
            return false;
        }

        if (string.Equals(context.EndpointGrantStatus, "missing", StringComparison.Ordinal))
        {
            RejectWithDeterministicDeny(fixtureId, context, errors, "E3034_S3_ENDPOINT_GRANT_MISSING", "GrantMissing", $"{fixtureId}/ResolveResponse requires active endpoint grant.");
            return true;
        }

        if (string.Equals(context.EndpointGrantStatus, "expired", StringComparison.Ordinal))
        {
            RejectWithDeterministicDeny(fixtureId, context, errors, "E3035_S3_ENDPOINT_GRANT_EXPIRED", "GrantExpired", $"{fixtureId}/ResolveResponse uses an expired endpoint grant.");
            return true;
        }

        if (string.Equals(context.EndpointGrantStatus, "active", StringComparison.Ordinal) && !context.GrantPresentSeen)
        {
            RejectWithDeterministicDeny(fixtureId, context, errors, "E3036_S3_ENDPOINT_GRANT_PROOF_MISSING", "GrantMissing", $"{fixtureId}/ResolveResponse requires GrantPresent proof path before endpoint disclosure.");
            return true;
        }

        return false;
    }
    private static void ValidateStateTrace(FixtureCase fixture, IReadOnlyList<string> observedStateTrace, List<ConformanceError> errors)
    {
        if (fixture.ExpectedStateTrace.Count == 0)
        {
            AddError(errors, "L3", "E3010_EMPTY_EXPECTED_TRACE", $"{fixture.Id} has empty expected_state_trace.");
            return;
        }

        for (var i = 1; i < observedStateTrace.Count; i++)
        {
            var current = observedStateTrace[i - 1];
            var next = observedStateTrace[i];
            if (!AllowedStateTransitions.TryGetValue(current, out var allowedNext) || !allowedNext.Contains(next))
            {
                AddError(errors, "L3", "E3007_INVALID_STATE_TRANSITION", $"{fixture.Id} invalid transition '{current}' -> '{next}'.");
            }
        }

        var expectedTrace = string.Join(" -> ", fixture.ExpectedStateTrace);
        var observedTrace = string.Join(" -> ", observedStateTrace);
        if (!TraceEquals(fixture.ExpectedStateTrace, observedStateTrace))
        {
            AddError(errors, "L3", "E3008_TRACE_MISMATCH", $"{fixture.Id} expected trace '{expectedTrace}' but observed '{observedTrace}'.");
        }

        if (observedStateTrace.Count == 0)
        {
            AddError(errors, "L3", "E3011_EMPTY_OBSERVED_TRACE", $"{fixture.Id} observed state trace is empty.");
            return;
        }

        var finalState = observedStateTrace[^1];
        if (fixture.ExpectedOutcome.Success &&
            (string.Equals(finalState, "Deny", StringComparison.Ordinal) ||
             string.Equals(finalState, "ObserveDenied", StringComparison.Ordinal)))
        {
            AddError(errors, "L3", "E3009_FINAL_STATE_MISMATCH", $"{fixture.Id} expected success but final state is '{finalState}'.");
        }

        if (!fixture.ExpectedOutcome.Success &&
            !(string.Equals(finalState, "Deny", StringComparison.Ordinal) ||
              string.Equals(finalState, "ObserveDenied", StringComparison.Ordinal) ||
              string.Equals(finalState, "ObserveGap", StringComparison.Ordinal)))
        {
            AddError(errors, "L3", "E3009_FINAL_STATE_MISMATCH", $"{fixture.Id} expected deny but final state is '{finalState}'.");
        }
    }

    private static void ValidateExpectedOutcome(
        FixtureCase fixture,
        string? observedDenyCode,
        string? observedUpgradeDecisionCode,
        bool? observedRetryable,
        List<ConformanceError> errors)
    {
        var expectedCode = fixture.ExpectedOutcome.DenyCode;
        if (!string.Equals(expectedCode, observedDenyCode, StringComparison.Ordinal))
        {
            if (!(expectedCode is null && observedDenyCode is null))
            {
                AddError(errors, "L4", "E4003_EXPECTED_DENY_MISMATCH", $"{fixture.Id} expected deny code '{expectedCode ?? "null"}' but observed '{observedDenyCode ?? "null"}'.");
            }
        }

        if (fixture.ExpectedOutcome.Retryable is { } expectedRetryable)
        {
            if (observedRetryable is null)
            {
                AddError(errors, "L4", "E4005_MISSING_DENY_PROFILE_FOR_RETRYABLE", $"{fixture.Id} expected retryable='{expectedRetryable}', but no deny profile was observed.");
            }
            else if (observedRetryable.Value != expectedRetryable)
            {
                AddError(errors, "L4", "E4004_EXPECTED_RETRYABLE_MISMATCH", $"{fixture.Id} expected retryable='{expectedRetryable}' but observed '{observedRetryable.Value}'.");
            }
        }

        var expectedUpgradeCode = fixture.ExpectedOutcome.UpgradeAttemptCode;
        if (!string.Equals(expectedUpgradeCode, observedUpgradeDecisionCode, StringComparison.Ordinal))
        {
            if (!(expectedUpgradeCode is null && observedUpgradeDecisionCode is null))
            {
                AddError(errors, "L3", "E3030_UPGRADE_ATTEMPT_CODE_MISMATCH", $"{fixture.Id} expected upgrade attempt code '{expectedUpgradeCode ?? "null"}' but observed '{observedUpgradeDecisionCode ?? "null"}'.");
            }
        }
    }

    private static void ValidateAssertions(
        FixtureCase fixture,
        IReadOnlyList<EnvelopeMessage> envelopeMessages,
        IReadOnlyList<string> observedStateTrace,
        IReadOnlyList<string> observedBridgeTransits,
        string? observedDenyCode,
        string? observedUpgradeDecisionCode,
        bool? observedRetryable,
        List<ConformanceError> errors)
    {
        foreach (var assertion in fixture.Assertions)
        {
            switch (assertion.Check)
            {
                case "final_state_equals":
                {
                    var expected = assertion.Value?.GetStringOrNull();
                    var actual = observedStateTrace.Count == 0 ? null : observedStateTrace[^1];
                    if (!string.Equals(expected, actual, StringComparison.Ordinal))
                    {
                        AddAssertionError(errors, assertion.Id, $"expected final state '{expected}', observed '{actual ?? "null"}'.");
                    }

                    break;
                }
                case "contains_state":
                {
                    var expected = assertion.Value?.GetStringOrNull();
                    if (expected is null || !observedStateTrace.Contains(expected, StringComparer.Ordinal))
                    {
                        AddAssertionError(errors, assertion.Id, $"expected state trace to contain '{expected ?? "null"}'.");
                    }

                    break;
                }
                case "deny_code_absent":
                {
                    if (observedDenyCode is not null)
                    {
                        AddAssertionError(errors, assertion.Id, $"expected no deny code but observed '{observedDenyCode}'.");
                    }

                    break;
                }
                case "deny_code_equals":
                {
                    var expected = assertion.Value?.GetStringOrNull();
                    if (!string.Equals(expected, observedDenyCode, StringComparison.Ordinal))
                    {
                        AddAssertionError(errors, assertion.Id, $"expected deny code '{expected}', observed '{observedDenyCode ?? "null"}'.");
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
                            AddAssertionError(errors, assertion.Id, $"required envelope field '{field}' missing.");
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
                        AddAssertionError(errors, assertion.Id, "expected remediation hint for deny code.");
                    }

                    break;
                }
                case "remediation_contains":
                {
                    var expectedToken = assertion.Value?.GetStringOrNull();
                    if (observedDenyCode is null || !DenyProfiles.TryGetValue(observedDenyCode, out var profile))
                    {
                        AddAssertionError(errors, assertion.Id, "no deny profile to validate remediation token.");
                        break;
                    }

                    if (expectedToken is null || profile.Remediation.IndexOf(expectedToken, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        AddAssertionError(errors, assertion.Id, $"remediation does not contain '{expectedToken ?? "null"}'.");
                    }

                    break;
                }
                case "selector_hints_present":
                {
                    var request = fixture.Messages.FirstOrDefault(m => string.Equals(m.Type, "ResolveRequest", StringComparison.Ordinal));
                    if (request is null || !HasNonEmptyStringArray(request, "selector_hints"))
                    {
                        AddAssertionError(errors, assertion.Id, "expected non-empty selector_hints in ResolveRequest.");
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
                        AddAssertionError(errors, assertion.Id, $"expected HandshakeAccept route_mode '{expected}', observed '{actual ?? "null"}'.");
                    }

                    break;
                }
                case "direct_upgrade_rejected":
                {
                    var reject = fixture.Messages.FirstOrDefault(m => string.Equals(m.Type, "RouteUpgradeReject", StringComparison.Ordinal));
                    var decision = reject is null ? null : GetBodyString(reject, "decision_code");
                    if (!string.Equals(decision, "UpgradeRejected", StringComparison.Ordinal))
                    {
                        AddAssertionError(errors, assertion.Id, $"expected RouteUpgradeReject decision_code 'UpgradeRejected', observed '{decision ?? "null"}'.");
                    }

                    break;
                }
                case "retryable_equals":
                {
                    var expected = assertion.Value?.GetBooleanOrNull();
                    if (expected is null || observedRetryable is null || expected.Value != observedRetryable.Value)
                    {
                        AddAssertionError(errors, assertion.Id, $"expected retryable '{expected?.ToString() ?? "null"}', observed '{observedRetryable?.ToString() ?? "null"}'.");
                    }

                    break;
                }
                case "upgrade_decision_code_equals":
                {
                    var expected = assertion.Value?.GetStringOrNull();
                    if (!string.Equals(expected, observedUpgradeDecisionCode, StringComparison.Ordinal))
                    {
                        AddAssertionError(errors, assertion.Id, $"expected upgrade decision code '{expected ?? "null"}', observed '{observedUpgradeDecisionCode ?? "null"}'.");
                    }

                    break;
                }
                case "bridge_transit_contains":
                {
                    var expected = assertion.Value?.GetStringOrNull();
                    if (expected is null || !observedBridgeTransits.Contains(expected, StringComparer.Ordinal))
                    {
                        AddAssertionError(errors, assertion.Id, $"expected bridge transit trace to contain '{expected ?? "null"}'.");
                    }

                    break;
                }
                case "bridge_transit_count_equals":
                {
                    if (assertion.Value is null || assertion.Value.Value.ValueKind != JsonValueKind.Number || !assertion.Value.Value.TryGetInt32(out var expectedCount))
                    {
                        AddAssertionError(errors, assertion.Id, "bridge_transit_count_equals requires an integer value.");
                        break;
                    }

                    if (observedBridgeTransits.Count != expectedCount)
                    {
                        AddAssertionError(errors, assertion.Id, $"expected bridge transit count '{expectedCount}', observed '{observedBridgeTransits.Count}'.");
                    }

                    break;
                }
                default:
                    AddAssertionError(errors, assertion.Id, $"unsupported assertion check '{assertion.Check}'.");
                    break;
            }
        }
    }

    private static bool TryTransition(string fixtureId, OperationContext context, string nextState, List<ConformanceError> errors)
    {
        if (context.CurrentState is null)
        {
            SetState(context, nextState);
            return true;
        }

        if (string.Equals(context.CurrentState, nextState, StringComparison.Ordinal))
        {
            return true;
        }

        if (AllowedStateTransitions.TryGetValue(context.CurrentState, out var allowedNext) && allowedNext.Contains(nextState))
        {
            SetState(context, nextState);
            return true;
        }

        AddError(errors, "L3", "E3007_INVALID_STATE_TRANSITION", $"{fixtureId} invalid transition '{context.CurrentState}' -> '{nextState}'.");
        return false;
    }

    private static bool StateIs(OperationContext context, params string[] expectedStates)
    {
        if (context.CurrentState is null)
        {
            return false;
        }

        return expectedStates.Any(state => string.Equals(context.CurrentState, state, StringComparison.Ordinal));
    }

    private static void SetState(OperationContext context, string state)
    {
        context.CurrentState = state;
        AppendState(context.ObservedStateTrace, state);
    }

    private static void ForceTerminalDeny(OperationContext context)
    {
        if (string.Equals(context.CurrentState, "Completed", StringComparison.Ordinal))
        {
            return;
        }

        if (!string.Equals(context.CurrentState, "Deny", StringComparison.Ordinal))
        {
            SetState(context, "Deny");
        }
    }

    private static bool IsTerminal(string? state)
    {
        return string.Equals(state, "Completed", StringComparison.Ordinal)
            || string.Equals(state, "Deny", StringComparison.Ordinal)
            || string.Equals(state, "ObserveClosed", StringComparison.Ordinal)
            || string.Equals(state, "ObserveDenied", StringComparison.Ordinal);
    }

    private static void RejectWithDeterministicDeny(
        string fixtureId,
        OperationContext context,
        List<ConformanceError> errors,
        string errorId,
        string deterministicDenyCode,
        string message)
    {
        AddError(errors, "L3", errorId, $"{message} Deterministic deny: '{deterministicDenyCode}'.");
        context.ObservedDenyCode ??= deterministicDenyCode;
        ForceTerminalDeny(context);
    }

    private static void AddAssertionError(List<ConformanceError> errors, string assertionId, string message)
    {
        AddError(errors, "A", $"A_{assertionId}", message);
    }

    private static void AddError(List<ConformanceError> errors, string layer, string id, string message)
    {
        errors.Add(new ConformanceError
        {
            Layer = layer,
            Id = id,
            Message = message
        });
    }

    private static string FormatError(ConformanceError error)
    {
        return $"[{error.Layer}:{error.Id}] {error.Message}";
    }

    private static bool TraceEquals(IReadOnlyList<string> expected, IReadOnlyList<string> observed)
    {
        if (expected.Count != observed.Count)
        {
            return false;
        }

        for (var i = 0; i < expected.Count; i++)
        {
            if (!string.Equals(expected[i], observed[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static void AppendState(List<string> states, string state)
    {
        if (states.Count == 0 || !string.Equals(states[^1], state, StringComparison.Ordinal))
        {
            states.Add(state);
        }
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

    private static bool? GetBodyBoolean(FixtureMessage message, string propertyName)
    {
        if (message.Body is null || message.Body.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!message.Body.Value.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static bool HasNonEmptyStringArray(FixtureMessage message, string propertyName)
    {
        if (message.Body is null || message.Body.Value.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!message.Body.Value.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        return property
            .EnumerateArray()
            .Any(item => item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()));
    }

    private static bool HasBodyProperty(FixtureMessage message, string propertyName)
    {
        if (message.Body is null || message.Body.Value.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return message.Body.Value.TryGetProperty(propertyName, out _);
    }

    private static bool GetDefaultFollowMoves(string scopeMode)
    {
        return !string.Equals(scopeMode, "exact", StringComparison.Ordinal);
    }

    private static string BuildBridgeTransitTrace(string senderRole, string transportPath)
    {
        if (string.Equals(transportPath, "mediated", StringComparison.Ordinal))
        {
            return string.Equals(senderRole, "requester", StringComparison.Ordinal)
                ? "requester->mediator->target"
                : "target->mediator->requester";
        }

        return string.Equals(senderRole, "requester", StringComparison.Ordinal)
            ? "requester->target"
            : "target->requester";
    }
}

internal sealed class OperationContext
{
    public string SliceProfile { get; set; } = "S1";
    public string? CurrentState { get; set; }
    public bool ResolveStarted { get; set; }
    public bool HandshakeStarted { get; set; }
    public bool HandshakeInitSeen { get; set; }
    public bool HandshakeChallengeSeen { get; set; }
    public bool HandshakeProofSeen { get; set; }
    public bool UpgradeProbePending { get; set; }
    public bool UpgradeRejectSeen { get; set; }
    public bool UpgradeFallbackRestored { get; set; }
    public bool EndpointOperationActive { get; set; }
    public bool EndpointDisclosureAllowed { get; set; } = true;
    public bool GrantPresentSeen { get; set; }
    public bool PolicyOperationStarted { get; set; }
    public bool PolicyDenyRequired { get; set; }
    public bool ObserveSessionStarted { get; set; }
    public bool ObserveFollowMoves { get; set; } = true;
    public bool RequiresSelectorHints { get; set; }
    public bool HasSelectorHints { get; set; }
    public string EndpointDirectoryMode { get; set; } = "plaintext";
    public string EndpointGrantStatus { get; set; } = "not_required";
    public string ObserveScopeMode { get; set; } = "subtree";
    public string? ExpectedPolicyDenyCode { get; set; }
    public string? ExpectedUpgradeRejectCode { get; set; }
    public string? ObservedDenyCode { get; set; }
    public string? ObservedUpgradeDecisionCode { get; set; }
    public List<string> ObservedStateTrace { get; } = new();
    public List<string> BridgeTransitTraces { get; } = new();
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
