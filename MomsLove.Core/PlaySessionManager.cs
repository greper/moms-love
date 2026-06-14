namespace MomsLove.Core;

public sealed class PlaySessionManager
{
    private const int GraceSegmentMinutes = 5;

    public DailyUsage Usage { get; private set; }
    public ControlPolicy Policy { get; private set; }

    public PlaySessionManager(DailyUsage usage, ControlPolicy policy)
    {
        Usage = usage;
        Policy = policy;
    }

    public void UpdatePolicy(ControlPolicy policy)
    {
        Policy = policy;
        if (Usage.State == PlayState.Idle && Usage.SessionsStarted >= policy.DailyPlayCount)
        {
            Usage.State = PlayState.LockedForToday;
        }
        else if (Usage.State == PlayState.LockedForToday && Usage.SessionsStarted < policy.DailyPlayCount)
        {
            Usage.State = PlayState.Idle;
        }
    }

    public PlaySnapshot Tick(DateTimeOffset now)
    {
        EnsureToday(now);

        if (Usage.State == PlayState.Running && Usage.SessionStartedAt is DateTimeOffset started)
        {
            var endsAt = started.AddMinutes(Usage.ActiveSessionMinutes);
            if (now >= endsAt)
            {
                Usage.State = PlayState.GracePeriod;
                Usage.GraceStartedAt = endsAt;
            }
        }

        if (Usage.State == PlayState.GracePeriod && Usage.GraceStartedAt is DateTimeOffset graceStarted)
        {
            var graceEndsAt = GetCurrentGraceSegmentEndsAt(now, graceStarted);
            if (now >= graceEndsAt)
            {
                EnterAfterSession(now);
            }
        }

        if (Usage.State == PlayState.Cooldown && Usage.CooldownUntil is DateTimeOffset cooldownUntil && now >= cooldownUntil)
        {
            Usage.CooldownUntil = null;
            Usage.State = Usage.SessionsStarted >= Policy.DailyPlayCount
                ? PlayState.LockedForToday
                : PlayState.Idle;
        }

        if (Usage.State == PlayState.Idle && Usage.SessionsStarted >= Policy.DailyPlayCount)
        {
            Usage.State = PlayState.LockedForToday;
        }

        return Snapshot(now);
    }

    public bool TryStart(DateTimeOffset now)
    {
        Tick(now);
        if (Usage.State != PlayState.Idle || Usage.SessionsStarted >= Policy.DailyPlayCount)
        {
            if (Usage.SessionsStarted >= Policy.DailyPlayCount)
            {
                Usage.State = PlayState.LockedForToday;
            }

            return false;
        }

        Usage.SessionsStarted++;
        Usage.State = PlayState.Running;
        Usage.SessionStartedAt = now;
        Usage.ActiveSessionMinutes = Policy.SessionMinutes;
        Usage.ActiveGraceMinutes = Policy.GraceMinutes;
        Usage.ActiveCooldownMinutes = Policy.CooldownMinutes;
        Usage.GraceStartedAt = null;
        Usage.CooldownUntil = null;
        return true;
    }

    public void FinishNow(DateTimeOffset now)
    {
        if (Usage.State is PlayState.Running or PlayState.GracePeriod)
        {
            EnterAfterSession(now);
        }
    }

    private void EnterAfterSession(DateTimeOffset now)
    {
        Usage.SessionStartedAt = null;
        Usage.GraceStartedAt = null;

        if (Usage.SessionsStarted >= Policy.DailyPlayCount)
        {
            Usage.CooldownUntil = null;
            Usage.State = PlayState.LockedForToday;
            return;
        }

        var cooldownMinutes = Usage.ActiveCooldownMinutes > 0
            ? Usage.ActiveCooldownMinutes
            : Policy.CooldownMinutes;
        Usage.CooldownUntil = now.AddMinutes(cooldownMinutes);
        Usage.State = PlayState.Cooldown;
    }

    private void EnsureToday(DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.LocalDateTime);
        if (Usage.Date == today)
        {
            return;
        }

        Usage = new DailyUsage { Date = today };
    }

    private PlaySnapshot Snapshot(DateTimeOffset now)
    {
        var remaining = Math.Max(0, Policy.DailyPlayCount - Usage.SessionsStarted);
        var remainingTime = TimeSpan.Zero;
        DateTimeOffset? stateEndsAt = null;

        if (Usage.State == PlayState.Running && Usage.SessionStartedAt is DateTimeOffset started)
        {
            stateEndsAt = started.AddMinutes(Usage.ActiveSessionMinutes);
            remainingTime = Positive(stateEndsAt.Value - now);
        }
        else if (Usage.State == PlayState.GracePeriod && Usage.GraceStartedAt is DateTimeOffset graceStarted)
        {
            stateEndsAt = GetCurrentGraceSegmentEndsAt(now, graceStarted);
            remainingTime = Positive(stateEndsAt.Value - now);
        }
        else if (Usage.State == PlayState.Cooldown && Usage.CooldownUntil is DateTimeOffset cooldownUntil)
        {
            stateEndsAt = cooldownUntil;
            remainingTime = Positive(cooldownUntil - now);
        }

        return new PlaySnapshot(Usage.State, Usage.SessionsStarted, remaining, remainingTime, Usage.Date, stateEndsAt);
    }

    private DateTimeOffset GetCurrentGraceSegmentEndsAt(DateTimeOffset now, DateTimeOffset graceStarted)
    {
        var totalGraceMinutes = Math.Max(0, Usage.ActiveGraceMinutes);
        if (totalGraceMinutes == 0)
        {
            return graceStarted;
        }

        var elapsedMinutes = Math.Max(0, (now - graceStarted).TotalMinutes);
        var completedSegments = (int)Math.Floor(elapsedMinutes / GraceSegmentMinutes);
        var segmentEndMinutes = Math.Min(totalGraceMinutes, (completedSegments + 1) * GraceSegmentMinutes);
        return graceStarted.AddMinutes(segmentEndMinutes);
    }

    private static TimeSpan Positive(TimeSpan value) => value < TimeSpan.Zero ? TimeSpan.Zero : value;
}
