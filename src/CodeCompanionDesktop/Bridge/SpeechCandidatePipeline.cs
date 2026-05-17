using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CodeCompanionDesktop.Bridge;

public sealed partial class SpeechCandidatePipeline
{
    public const int MaxSpeechTextLength = 1000;
    private const string TruncationSuffix = "...";

    private readonly object syncRoot = new();
    private readonly HashSet<string> seenMessageIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> seenTextHashes = new(StringComparer.Ordinal);
    private readonly SpeechProfileState speechProfiles;

    public SpeechCandidatePipeline(SpeechProfileState? speechProfiles = null)
    {
        this.speechProfiles = speechProfiles ?? new SpeechProfileState();
    }

    public SpeechCandidatePipelineResult Prepare(SpeechCandidatePipelineInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var speechHint = NormalizeSpeechHint(input.SpeechHint);
        var normalized = NormalizeForSpeech(input.Text);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return SpeechCandidatePipelineResult.Ignored("empty_candidate");
        }

        var speechPolicyText = ReplaceDirectoryPaths(normalized);
        var filtered = RedactSensitiveText(speechPolicyText);
        if (string.IsNullOrWhiteSpace(filtered))
        {
            return SpeechCandidatePipelineResult.Ignored("empty_after_privacy_filter");
        }

        var profileCommand = ParseSpeechProfileCommand(filtered);
        if (profileCommand is not SpeechProfileCommand.None)
        {
            if (!IsSupportedProfileCommandKind(input.Kind))
            {
                return SpeechCandidatePipelineResult.Ignored("unsupported_candidate_kind");
            }

            return PrepareProfileCommand(input.MessageId, profileCommand);
        }

        if (!IsSupportedSpeechCandidateKind(input.Kind))
        {
            return SpeechCandidatePipelineResult.Ignored("unsupported_candidate_kind");
        }

        var reason = GetAcceptedReason(normalized, speechPolicyText, filtered, speechHint);
        if (!string.IsNullOrWhiteSpace(input.Phase)
            && !string.Equals(input.Phase.Trim(), "final", StringComparison.OrdinalIgnoreCase))
        {
            if (!IsExplicitSpeechRequest(speechHint))
            {
                if (!speechProfiles.IsDemoModeActive())
                {
                    return SpeechCandidatePipelineResult.Ignored("non_final_candidate");
                }

                reason = "demo-mode-progress";
            }
        }

        if (filtered.Length > MaxSpeechTextLength)
        {
            filtered = $"{filtered[..(MaxSpeechTextLength - TruncationSuffix.Length)].TrimEnd()}{TruncationSuffix}";
            reason = reason == "privacy_filtered" ? "privacy_filtered_truncated" : "truncated";
        }

        return ReserveAccepted(input.MessageId, filtered, reason);
    }

    private SpeechCandidatePipelineResult PrepareProfileCommand(string messageId, SpeechProfileCommand command)
    {
        var speechText = command == SpeechProfileCommand.EnableDemoMode
            ? "Demo Mode is on. I will speak more often during this session."
            : "Demo Mode is off. Standard speech policy is restored.";
        var reason = command == SpeechProfileCommand.EnableDemoMode
            ? "demo-mode-enabled"
            : "demo-mode-ended";

        var result = ReserveAccepted(messageId, speechText, reason, dedupeText: false);
        if (result.Decision != "accepted")
        {
            return result;
        }

        if (command == SpeechProfileCommand.EnableDemoMode)
        {
            speechProfiles.EnableDemoMode();
        }
        else
        {
            speechProfiles.DisableDemoMode();
        }

        return result;
    }

    private SpeechCandidatePipelineResult ReserveAccepted(
        string messageId,
        string speechText,
        string reason,
        bool dedupeText = true)
    {
        var normalizedHash = HashNormalizedText(speechText);
        lock (syncRoot)
        {
            if (seenMessageIds.Contains(messageId) || (dedupeText && seenTextHashes.Contains(normalizedHash)))
            {
                return SpeechCandidatePipelineResult.Duplicate("duplicate_candidate");
            }

            var reservation = new SpeechCandidateReservation(messageId, normalizedHash);
            seenMessageIds.Add(reservation.MessageId);
            if (dedupeText)
            {
                seenTextHashes.Add(reservation.NormalizedTextHash);
            }

            return SpeechCandidatePipelineResult.Accepted(speechText, reason, reservation);
        }
    }

    public void Release(SpeechCandidateReservation reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        lock (syncRoot)
        {
            seenMessageIds.Remove(reservation.MessageId);
            seenTextHashes.Remove(reservation.NormalizedTextHash);
        }
    }

    private static string NormalizeForSpeech(string text)
    {
        return WhitespaceRegex().Replace(text.Trim(), " ");
    }

    private static string RedactSensitiveText(string text)
    {
        var filtered = AuthorizationHeaderRegex().Replace(text, "$1 [redacted]");
        filtered = BearerTokenRegex().Replace(filtered, "Bearer [redacted]");
        filtered = ApiKeyAssignmentRegex().Replace(filtered, "$1 [redacted]");
        filtered = SecretLikeTokenRegex().Replace(filtered, "[redacted]");
        filtered = EmailRegex().Replace(filtered, "[redacted email]");
        return filtered;
    }

    private static string ReplaceDirectoryPaths(string text)
    {
        var filtered = UncPathRegex().Replace(text, ReplacePathMatch);
        filtered = WindowsPathRegex().Replace(filtered, ReplacePathMatch);
        return UnixPathRegex().Replace(filtered, ReplacePathMatch);
    }

    private static string ReplacePathMatch(Match match)
    {
        var value = match.Value;
        var trailingPunctuation = string.Empty;

        while (value.Length > 0 && IsPathTrailingPunctuation(value[^1]))
        {
            trailingPunctuation = value[^1] + trailingPunctuation;
            value = value[..^1];
        }

        return $"that location{trailingPunctuation}";
    }

    private static bool IsPathTrailingPunctuation(char value)
    {
        return value is '.' or ',' or ';' or '!' or '?' or ')';
    }

    private static string GetAcceptedReason(
        string normalized,
        string speechPolicyText,
        string filtered,
        string? speechHint)
    {
        if (!string.Equals(filtered, speechPolicyText, StringComparison.Ordinal))
        {
            return "privacy_filtered";
        }

        if (!string.Equals(speechPolicyText, normalized, StringComparison.Ordinal))
        {
            return "speech_rewritten";
        }

        return IsExplicitSpeechRequest(speechHint) ? speechHint! : "accepted";
    }

    private static bool IsExplicitSpeechRequest(string? speechHint)
    {
        return speechHint is "voice-check-in" or "manual-speak-last" or "manual-desktop-candidate-test";
    }

    private static bool IsSupportedSpeechCandidateKind(string kind)
    {
        return string.Equals(kind.Trim(), "assistant-message", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSupportedProfileCommandKind(string kind)
    {
        var normalized = kind.Trim();
        return string.Equals(normalized, "assistant-message", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "user-message", StringComparison.OrdinalIgnoreCase);
    }

    private static SpeechProfileCommand ParseSpeechProfileCommand(string text)
    {
        var normalized = text.Trim();
        if (string.Equals(normalized, "Demo Mode", StringComparison.OrdinalIgnoreCase))
        {
            return SpeechProfileCommand.EnableDemoMode;
        }

        if (string.Equals(normalized, "end demo", StringComparison.OrdinalIgnoreCase))
        {
            return SpeechProfileCommand.DisableDemoMode;
        }

        return SpeechProfileCommand.None;
    }

    private static string? NormalizeSpeechHint(string? speechHint)
    {
        return string.IsNullOrWhiteSpace(speechHint)
            ? null
            : speechHint.Trim().ToLowerInvariant();
    }

    private static string HashNormalizedText(string text)
    {
        var normalized = text.Trim().ToUpperInvariant();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"(?i)\b(authorization\s*:\s*)\S+(?:\s+\S+)?")]
    private static partial Regex AuthorizationHeaderRegex();

    [GeneratedRegex(@"(?i)\bBearer\s+[A-Za-z0-9._~+/=-]{12,}")]
    private static partial Regex BearerTokenRegex();

    [GeneratedRegex(@"(?i)\b((?:api[-_ ]?key|token|password|secret)\s*[:=]\s*)\S+")]
    private static partial Regex ApiKeyAssignmentRegex();

    [GeneratedRegex(@"\b(?:sk|xi)-[A-Za-z0-9_-]{16,}\b")]
    private static partial Regex SecretLikeTokenRegex();

    [GeneratedRegex(@"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", RegexOptions.IgnoreCase)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"(?<![\w])(?:[A-Za-z]:[\\/][^\s""<>|]+)")]
    private static partial Regex WindowsPathRegex();

    [GeneratedRegex(@"(?<![\w])\\\\[^\s\\/:*?""<>|]+\\[^\s""<>|]+")]
    private static partial Regex UncPathRegex();

    [GeneratedRegex(@"(?<![\w])/(?:mnt|var|home|tmp|etc|usr|opt|workspace|srv)/[^\s\])}>""']+")]
    private static partial Regex UnixPathRegex();
}

internal enum SpeechProfileCommand
{
    None,
    EnableDemoMode,
    DisableDemoMode
}

public sealed record SpeechCandidatePipelineInput(
    string MessageId,
    string Kind,
    string? Phase,
    string? SpeechHint,
    string Text);

public sealed record SpeechCandidateReservation(
    string MessageId,
    string NormalizedTextHash);

public sealed record SpeechCandidatePipelineResult(
    string Decision,
    string Reason,
    string? SpeechText,
    SpeechCandidateReservation? Reservation)
{
    public static SpeechCandidatePipelineResult Accepted(
        string speechText,
        string reason,
        SpeechCandidateReservation reservation)
    {
        return new SpeechCandidatePipelineResult("accepted", reason, speechText, reservation);
    }

    public static SpeechCandidatePipelineResult Ignored(string reason)
    {
        return new SpeechCandidatePipelineResult("ignored", reason, null, null);
    }

    public static SpeechCandidatePipelineResult Duplicate(string reason)
    {
        return new SpeechCandidatePipelineResult("duplicate", reason, null, null);
    }
}
