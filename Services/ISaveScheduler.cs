using System;

namespace MiniMetrics.Services;

// Collapses a burst of save requests into a single delayed write. SettingsController owns the policy
// of what to persist; the scheduler owns only the timing mechanism, so it can be faked in tests.
public interface ISaveScheduler
{
    // Schedules the action to run after a quiet period, restarting the delay on each call so a burst
    // of changes coalesces into one run.
    void Schedule(Action action);

    // Runs any pending action immediately and cancels the timer.
    void Flush();
}
