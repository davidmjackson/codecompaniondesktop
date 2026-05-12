using System;
using System.Security.Cryptography;
using CodeCompanionDesktop.Credentials;

namespace CodeCompanionDesktop.Bridge;

public sealed class BridgeTokenStore
{
    public const string BridgeTokenTarget = "CodeCompanionDesktop/BridgeToken";

    private readonly WindowsCredentialStore credentialStore;

    public BridgeTokenStore(WindowsCredentialStore credentialStore)
    {
        this.credentialStore = credentialStore;
    }

    public string EnsureToken()
    {
        var token = credentialStore.ReadSecret(BridgeTokenTarget);
        if (!string.IsNullOrWhiteSpace(token))
        {
            return token;
        }

        token = GenerateToken();
        credentialStore.SaveSecret(BridgeTokenTarget, "CodeCompanionDesktop Bridge", token);
        return token;
    }

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);

        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
