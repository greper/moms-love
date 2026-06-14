using MomsLove.Core;

namespace MomsLove.Tests;

public class PlaySessionManagerTests
{
    [Fact]
    public void Start_ConsumesOneSessionImmediately()
    {
        var start = new DateTimeOffset(2026, 6, 14, 8, 0, 0, TimeSpan.Zero);
        var manager = CreateManager();

        var started = manager.TryStart(start);
        var snapshot = manager.Tick(start);

        Assert.True(started);
        Assert.Equal(PlayState.Running, snapshot.State);
        Assert.Equal(1, snapshot.SessionsStarted);
        Assert.Equal(1, snapshot.RemainingSessions);
    }

    [Fact]
    public void Flow_RunningToGraceToCooldownToIdle()
    {
        var start = new DateTimeOffset(2026, 6, 14, 8, 0, 0, TimeSpan.Zero);
        var manager = CreateManager(sessionMinutes: 30, graceMinutes: 10, cooldownMinutes: 30);

        manager.TryStart(start);

        Assert.Equal(PlayState.Running, manager.Tick(start.AddMinutes(29)).State);
        Assert.Equal(PlayState.GracePeriod, manager.Tick(start.AddMinutes(30)).State);
        Assert.Equal(PlayState.Cooldown, manager.Tick(start.AddMinutes(41)).State);
        Assert.Equal(PlayState.Idle, manager.Tick(start.AddMinutes(72)).State);
    }

    [Fact]
    public void SecondFinishedSession_LocksForToday()
    {
        var start = new DateTimeOffset(2026, 6, 14, 8, 0, 0, TimeSpan.Zero);
        var manager = CreateManager(sessionMinutes: 1, graceMinutes: 1, cooldownMinutes: 1);

        manager.TryStart(start);
        manager.FinishNow(start.AddSeconds(10));
        manager.Tick(start.AddMinutes(2));
        manager.TryStart(start.AddMinutes(3));
        manager.FinishNow(start.AddMinutes(4));

        Assert.Equal(PlayState.LockedForToday, manager.Tick(start.AddMinutes(4)).State);
        Assert.False(manager.TryStart(start.AddMinutes(5)));
    }

    [Fact]
    public void NewDate_ResetsUsage()
    {
        var start = new DateTimeOffset(2026, 6, 14, 23, 58, 0, TimeSpan.Zero);
        var manager = CreateManager();
        manager.TryStart(start);

        var snapshot = manager.Tick(start.AddDays(1));

        Assert.Equal(PlayState.Idle, snapshot.State);
        Assert.Equal(0, snapshot.SessionsStarted);
        Assert.Equal(2, snapshot.RemainingSessions);
    }

    [Fact]
    public void FinishNow_StartsCooldownWhenMoreSessionsRemain()
    {
        var start = new DateTimeOffset(2026, 6, 14, 8, 0, 0, TimeSpan.Zero);
        var manager = CreateManager(cooldownMinutes: 30);
        manager.TryStart(start);

        manager.FinishNow(start.AddMinutes(5));
        var snapshot = manager.Tick(start.AddMinutes(10));

        Assert.Equal(PlayState.Cooldown, snapshot.State);
        Assert.Equal(TimeSpan.FromMinutes(25), snapshot.RemainingInState);
    }

    [Fact]
    public void StartDuringCooldown_DoesNotLockTheDay()
    {
        var start = new DateTimeOffset(2026, 6, 14, 8, 0, 0, TimeSpan.Zero);
        var manager = CreateManager(cooldownMinutes: 30);
        manager.TryStart(start);
        manager.FinishNow(start.AddMinutes(5));

        var started = manager.TryStart(start.AddMinutes(6));

        Assert.False(started);
        Assert.Equal(PlayState.Cooldown, manager.Tick(start.AddMinutes(6)).State);
    }

    [Fact]
    public void RunningSession_UsesPolicySnapshotForCooldown()
    {
        var start = new DateTimeOffset(2026, 6, 14, 8, 0, 0, TimeSpan.Zero);
        var manager = CreateManager(cooldownMinutes: 30);
        manager.TryStart(start);
        manager.UpdatePolicy(new ControlPolicy
        {
            DailyPlayCount = 2,
            SessionMinutes = 5,
            GraceMinutes = 1,
            CooldownMinutes = 5
        });

        manager.FinishNow(start.AddMinutes(1));
        var snapshot = manager.Tick(start.AddMinutes(2));

        Assert.Equal(TimeSpan.FromMinutes(29), snapshot.RemainingInState);
    }

    [Fact]
    public void GracePeriod_UsesFiveMinuteSegmentsUntilMaxGraceIsUsed()
    {
        var start = new DateTimeOffset(2026, 6, 14, 8, 0, 0, TimeSpan.Zero);
        var manager = CreateManager(sessionMinutes: 1, graceMinutes: 13, cooldownMinutes: 30);
        manager.TryStart(start);

        var firstSegment = manager.Tick(start.AddMinutes(1));
        var secondSegment = manager.Tick(start.AddMinutes(6));
        var finalSegment = manager.Tick(start.AddMinutes(11));
        var cooldown = manager.Tick(start.AddMinutes(14));

        Assert.Equal(PlayState.GracePeriod, firstSegment.State);
        Assert.Equal(TimeSpan.FromMinutes(5), firstSegment.RemainingInState);
        Assert.Equal(PlayState.GracePeriod, secondSegment.State);
        Assert.Equal(TimeSpan.FromMinutes(5), secondSegment.RemainingInState);
        Assert.Equal(PlayState.GracePeriod, finalSegment.State);
        Assert.Equal(TimeSpan.FromMinutes(3), finalSegment.RemainingInState);
        Assert.Equal(PlayState.Cooldown, cooldown.State);
    }

    [Fact]
    public void GracePeriod_UsesRemainingGraceWhenLessThanFiveMinutesRemain()
    {
        var start = new DateTimeOffset(2026, 6, 14, 8, 0, 0, TimeSpan.Zero);
        var manager = CreateManager(sessionMinutes: 1, graceMinutes: 3, cooldownMinutes: 30);
        manager.TryStart(start);

        var grace = manager.Tick(start.AddMinutes(1));
        var cooldown = manager.Tick(start.AddMinutes(4));

        Assert.Equal(PlayState.GracePeriod, grace.State);
        Assert.Equal(TimeSpan.FromMinutes(3), grace.RemainingInState);
        Assert.Equal(PlayState.Cooldown, cooldown.State);
    }

    private static PlaySessionManager CreateManager(
        int dailyCount = 2,
        int sessionMinutes = 30,
        int graceMinutes = 10,
        int cooldownMinutes = 30)
    {
        return new PlaySessionManager(
            new DailyUsage { Date = new DateOnly(2026, 6, 14) },
            new ControlPolicy
            {
                DailyPlayCount = dailyCount,
                SessionMinutes = sessionMinutes,
                GraceMinutes = graceMinutes,
                CooldownMinutes = cooldownMinutes
            });
    }
}
