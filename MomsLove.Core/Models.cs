namespace MomsLove.Core;

public enum ControlRuleType
{
    Executable,
    Shortcut,
    Directory
}

public enum PlayState
{
    Idle,
    Running,
    GracePeriod,
    Cooldown,
    LockedForToday
}

public sealed class ControlRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DisplayName { get; set; } = "";
    public string Path { get; set; } = "";
    public ControlRuleType Type { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string IconKey { get; set; } = "Game";
    public string IconPath { get; set; } = "";
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.Now;
}

public sealed class ControlPolicy
{
    public int DailyPlayCount { get; set; } = 2;
    public int SessionMinutes { get; set; } = 30;
    public int CooldownMinutes { get; set; } = 30;
    public int GraceMinutes { get; set; } = 10;
}

public sealed class PasswordSettings
{
    public byte[] Salt { get; set; } = [];
    public byte[] Hash { get; set; } = [];
    public int Iterations { get; set; } = 120_000;

    public bool IsConfigured => Salt.Length > 0 && Hash.Length > 0;
}

public sealed class AppConfig
{
    public ControlPolicy Policy { get; set; } = new();
    public List<ControlRule> Rules { get; set; } = [];
    public bool RunAtStartup { get; set; }
    public PasswordSettings Password { get; set; } = new();
}

public sealed class DailyUsage
{
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public PlayState State { get; set; } = PlayState.Idle;
    public int SessionsStarted { get; set; }
    public DateTimeOffset? SessionStartedAt { get; set; }
    public int ActiveSessionMinutes { get; set; }
    public int ActiveGraceMinutes { get; set; }
    public int ActiveCooldownMinutes { get; set; }
    public DateTimeOffset? GraceStartedAt { get; set; }
    public DateTimeOffset? CooldownUntil { get; set; }
}

public sealed record PlaySnapshot(
    PlayState State,
    int SessionsStarted,
    int RemainingSessions,
    TimeSpan RemainingInState,
    DateOnly Date,
    DateTimeOffset? StateEndsAt = null);
