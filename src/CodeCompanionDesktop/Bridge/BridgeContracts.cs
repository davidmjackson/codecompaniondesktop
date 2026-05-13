using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeCompanionDesktop.Bridge;

public static class BridgeJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}

public sealed record ClientHelloRequest(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("client")] BridgeClient Client,
    [property: JsonPropertyName("workspace")] BridgeWorkspace Workspace);

public sealed record BridgeClient(
    [property: JsonPropertyName("clientId")] string ClientId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("host")] string Host,
    [property: JsonPropertyName("environment")] string Environment);

public sealed record BridgeWorkspace(
    [property: JsonPropertyName("projectId")] string ProjectId,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("roots")] IReadOnlyList<string> Roots);

public sealed record ClientHelloResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("authorization")] string Authorization,
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("bridgeVersion")] string BridgeVersion,
    [property: JsonPropertyName("protocolVersion")] int ProtocolVersion,
    [property: JsonPropertyName("sessionToken")] string? SessionToken,
    [property: JsonPropertyName("sessionExpiresAtUtc")] string? SessionExpiresAtUtc);

public sealed record SpeechCandidateRequest(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("client")] BridgeClient Client,
    [property: JsonPropertyName("workspace")] BridgeWorkspace Workspace,
    [property: JsonPropertyName("codex")] CodexMetadata Codex,
    [property: JsonPropertyName("candidate")] SpeechCandidate Candidate);

public sealed record CodexMetadata(
    [property: JsonPropertyName("sessionId")] string SessionId,
    [property: JsonPropertyName("messageId")] string MessageId,
    [property: JsonPropertyName("timestamp")] string Timestamp);

public sealed record SpeechCandidate(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("phase")] string? Phase,
    [property: JsonPropertyName("speechHint")] string? SpeechHint,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("source")] string? Source);

public sealed record SpeechCandidateResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("decision")] string Decision,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("queuePosition")] int QueuePosition);

public sealed record ErrorResponse([property: JsonPropertyName("error")] string Error);
