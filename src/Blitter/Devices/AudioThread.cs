namespace Blitter.Devices;

/// <summary>
/// Tripwire for code paths that must execute on the application thread.
/// SDL audio APIs are not safe to call from arbitrary threads under
/// load (the audio backend's mixer thread races against our writes);
/// every SDL audio call site asserts this so any future off-thread
/// caller blows up immediately instead of silently corrupting state.
/// </summary>
internal static class AudioThread
{
    /// <summary>
    /// Throws if the current thread isn't the application thread.
    /// </summary>
    public static void Assert()
    {
        var appThread = Application.Current.Thread;
        if (appThread != Thread.CurrentThread)
            throw new InvalidOperationException(
                $"SDL audio call from wrong thread: caller=#{Environment.CurrentManagedThreadId} " +
                $"app=#{appThread.ManagedThreadId}. All SDL audio APIs must be invoked on " +
                $"the application thread.");
    }
}
