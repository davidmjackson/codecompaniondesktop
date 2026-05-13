namespace CodeCompanionDesktop.Bridge;

public static class BridgeContractValidator
{
    public const int MaxCandidateTextLength = 8000;

    public static string? ValidateClientHello(ClientHelloRequest request)
    {
        if (request.SchemaVersion != 1)
        {
            return "unsupported_schema_version";
        }

        if (request.Client is null)
        {
            return "invalid_client";
        }

        if (request.Workspace is null)
        {
            return "invalid_workspace";
        }

        if (string.IsNullOrWhiteSpace(request.Client.ClientId)
            || string.IsNullOrWhiteSpace(request.Client.Name)
            || string.IsNullOrWhiteSpace(request.Client.Version)
            || string.IsNullOrWhiteSpace(request.Client.Host)
            || string.IsNullOrWhiteSpace(request.Client.Environment))
        {
            return "invalid_client";
        }

        if (string.IsNullOrWhiteSpace(request.Workspace.ProjectId)
            || string.IsNullOrWhiteSpace(request.Workspace.DisplayName)
            || request.Workspace.Roots is null
            || request.Workspace.Roots.Count == 0)
        {
            return "invalid_workspace";
        }

        return null;
    }

    public static string? ValidateSpeechCandidate(SpeechCandidateRequest request)
    {
        var clientError = ValidateClientHello(new ClientHelloRequest(
            request.SchemaVersion,
            request.Client,
            request.Workspace));
        if (clientError is not null)
        {
            return clientError;
        }

        if (request.Codex is null)
        {
            return "invalid_codex_metadata";
        }

        if (request.Candidate is null)
        {
            return "invalid_candidate";
        }

        if (string.IsNullOrWhiteSpace(request.Codex.SessionId)
            || string.IsNullOrWhiteSpace(request.Codex.MessageId)
            || string.IsNullOrWhiteSpace(request.Codex.Timestamp))
        {
            return "invalid_codex_metadata";
        }

        if (string.IsNullOrWhiteSpace(request.Candidate.Kind)
            || string.IsNullOrWhiteSpace(request.Candidate.Text))
        {
            return "invalid_candidate";
        }

        if (request.Candidate.Text.Length > MaxCandidateTextLength)
        {
            return "candidate_text_too_long";
        }

        return null;
    }
}
