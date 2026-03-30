using System.Threading;

namespace Content.IntegrationTests;

[SetUpFixture]
public sealed class PoolManagerTestEventHandler
{
    // This value is completely arbitrary.
    // Increased for CI environments which are slower than local development machines.
    private static TimeSpan MaximumTotalTestingTimeLimit => TimeSpan.FromMinutes(45);
    private static TimeSpan HardStopTimeLimit => MaximumTotalTestingTimeLimit.Add(TimeSpan.FromMinutes(5));

    // Use Timer instead of Task.Delay to avoid ThreadPool starvation issues
    private static Timer? _softTimeoutTimer;
    private static Timer? _hardTimeoutTimer;

    [OneTimeSetUp]
    public void Setup()
    {
        TestContext.Out.WriteLine($"[{DateTime.Now:O}] PoolManagerTestEventHandler.Setup() started");
        PoolManager.Startup();
        TestContext.Out.WriteLine($"[{DateTime.Now:O}] PoolManager.Startup() completed");

        // Use Timer with dedicated threads to avoid ThreadPool starvation
        // These will fire even if the ThreadPool is completely blocked
        TestContext.Out.WriteLine($"[{DateTime.Now:O}] Setting up timeout timers (soft={MaximumTotalTestingTimeLimit.TotalMinutes}min, hard={HardStopTimeLimit.TotalMinutes}min)");
        _softTimeoutTimer = new Timer(SoftTimeoutCallback, null, MaximumTotalTestingTimeLimit, Timeout.InfiniteTimeSpan);
        _hardTimeoutTimer = new Timer(HardTimeoutCallback, null, HardStopTimeLimit, Timeout.InfiniteTimeSpan);
        TestContext.Out.WriteLine($"[{DateTime.Now:O}] PoolManagerTestEventHandler.Setup() completed");
    }

    private static void SoftTimeoutCallback(object? state)
    {
        // This can and probably will cause server/client pairs to shut down MID test, and will lead to really confusing test failures.
        TestContext.Error.WriteLine($"\n\n{nameof(PoolManagerTestEventHandler)}: ERROR: Tests are taking too long (>{MaximumTotalTestingTimeLimit.TotalMinutes} min). Shutting down all tests. This may lead to weird failures/exceptions.\n\n");
        TestContext.Error.WriteLine($"Death Report:\n{PoolManager.DeathReport()}");
        PoolManager.Shutdown();
    }

    private static void HardTimeoutCallback(object? state)
    {
        var deathReport = PoolManager.DeathReport();
        Environment.FailFast($"Tests took way too long (>{HardStopTimeLimit.TotalMinutes} min);\n Death Report:\n{deathReport}");
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        _softTimeoutTimer?.Dispose();
        _hardTimeoutTimer?.Dispose();
        PoolManager.Shutdown();
    }
}
