using System;
using System.Threading;

namespace MiniMetrics.Services;

// Guards against a second copy of the app running at once. The first process to start creates and owns
// a named system mutex; a later process finds it already exists and bows out. The owner holds the mutex
// for its lifetime and releases it on dispose (or when the process exits and the OS reclaims it).
public sealed class SingleInstance : IDisposable
{
    // App-specific name in the session-local namespace, so the guard is per logged-in user and does not
    // collide with other software's mutexes.
    private const string DefaultName = @"Local\MiniMetrics.SingleInstance";

    private readonly Mutex? _mutex;

    private SingleInstance(Mutex? ownedMutex) => _mutex = ownedMutex;

    // True when this process created the mutex and is therefore the only running instance.
    public bool IsOnlyInstance => _mutex is not null;

    public static SingleInstance Acquire() => Acquire(DefaultName);

    // Overload taking an explicit name so tests can isolate from the live app's mutex.
    public static SingleInstance Acquire(string name)
    {
        var mutex = new Mutex(true, name, out bool createdNew);
        if (createdNew) return new(mutex);

        // The mutex already existed: another instance owns it. Release our handle to it and report that
        // we are not the only instance.
        mutex.Dispose();
        return new(null);
    }

    public void Dispose()
    {
        if (_mutex is null) return;

        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
