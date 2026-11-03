namespace TaskFlow.Tests;

/// <summary>
/// A clock frozen at a known instant. Every test that touches deadlines, overdue
/// detection or completion timestamps uses this instead of the real clock, so results
/// do not depend on when the suite happens to run.
/// </summary>
public sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
{
    /// <summary>The reference "now" shared by the tests.</summary>
    public static readonly DateTime DefaultNow = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

    public DateTime UtcNow { get; set; } = utcNow;

    public FixedTimeProvider() : this(DefaultNow)
    {
    }

    public override DateTimeOffset GetUtcNow() => new(UtcNow, TimeSpan.Zero);
}
