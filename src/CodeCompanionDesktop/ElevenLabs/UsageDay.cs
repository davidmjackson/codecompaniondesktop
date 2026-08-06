using System;

namespace CodeCompanionDesktop.ElevenLabs;

/// <summary>
/// One day's billed characters, as reported by /v1/usage/character-stats.
/// The date is UTC, matching the bucket boundaries the API returns.
/// </summary>
public sealed record UsageDay(DateOnly Date, long Characters);
