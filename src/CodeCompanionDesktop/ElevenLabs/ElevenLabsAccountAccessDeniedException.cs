using System;

namespace CodeCompanionDesktop.ElevenLabs;

/// <summary>
/// The API key authenticated but is not permitted to read account information.
/// Carries the provider's own explanation, which names the missing scope.
/// </summary>
public sealed class ElevenLabsAccountAccessDeniedException : Exception
{
    public ElevenLabsAccountAccessDeniedException(string message)
        : base(message)
    {
    }
}
