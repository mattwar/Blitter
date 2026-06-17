using System.Collections.Concurrent;
using System.Collections.Immutable;

using Blitter.Devices;

namespace Blitter;

public static class Audio
{
    /// <summary>
    /// Ensures the application is running and the SDL audio subsystem is
    /// initialized. Gated by a flag so we only init once per process —
    /// SDL_InitSubSystem increments a refcount internally and we want
    /// QuitSubSystem (during periodic rotation) to actually release.
    /// </summary>
    private static void EnsureInit()
    {
        _ = Application.Current;
        if (!_audioSubsystemInitialized)
        {
            if (!SDL.InitSubSystem(SDL.InitFlags.Audio))
                throw new InvalidOperationException(
                    $"Failed to initialize SDL audio subsystem: {SDL.GetError()}");
            _audioSubsystemInitialized = true;
        }
        EnableSdlAudioLogging();
    }

    private static bool _audioSubsystemInitialized;

    private static bool _sdlLoggingEnabled;
    // Hold the delegate in a static so the GC can't collect it while
    // SDL holds the unmanaged function pointer.
    private static SDL.LogOutputFunction? _sdlLogCallback;

    private static void EnableSdlAudioLogging()
    {
        if (_sdlLoggingEnabled)
            return;
        _sdlLoggingEnabled = true;
        // SDL3's native DLL is almost certainly a Release build, which
        // strips Verbose/Debug messages at compile time — there is no
        // way to retrieve them at runtime no matter what priority we
        // set. Info+ is the realistic floor.
        SDL.SetLogPriorities(SDL.LogPriority.Info);
        _sdlLogCallback = SdlLogCallback;
        SDL.SetLogOutputFunction(_sdlLogCallback, IntPtr.Zero);
    }

    private static void SdlLogCallback(IntPtr userdata, SDL.LogCategory category, SDL.LogPriority priority, string message)
    {
        Console.WriteLine($"[SDL {priority} cat={category}] {message}");
    }

    /// <summary>
    /// Global on/off switch for audio playback. When <c>false</c>,
    /// <see cref="Play"/> and <see cref="PlayAsync"/> become no-ops —
    /// no SDL audio calls are made at all. Useful as a diagnostic to
    /// rule audio in or out when chasing native crashes.
    /// </summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>
    /// Plays the audio data on the default playback device.
    /// </summary>
    public static void Play(Sound data, float volume = 1f)
    {
        // fire and forget
        var _ = PlayAsync(data, volume);
    }
    
    /// <summary>
    /// Plays the audio data on the default playback device.
    /// </summary>
    public static Task PlayAsync(Sound data, float volume = 1f)
    {
        if (!Enabled)
            return Task.CompletedTask;
        // Marshal all SDL audio calls onto the application thread so
        // we never hit the audio backend from arbitrary threadpool /
        // game threads concurrently. Some Windows audio backends
        // (WASAPI in particular) have thread-affinity expectations
        // that SDL doesn't fully shield us from. Pinning every play
        // to one thread sidesteps that class of race / corruption.
        var app = Application.Current;
        if (app.Thread != Thread.CurrentThread)
        {
            // Rent a request object from the pool instead of boxing
            // a (Sound, float) value tuple into a fresh allocation on
            // every cross-thread play. The Post callback returns it
            // to the pool after dispatching.
            var req = RentRequest(data, volume);
            app.Post(static s =>
            {
                var r = (PlayRequest)s!;
                var sound = r.Sound;
                var vol = r.Volume;
                ReturnRequest(r);
                PlayOnAppThread(sound, vol);
            }, req);
            return Task.CompletedTask;
        }

        return PlayOnAppThread(data, volume);
    }

    private sealed class PlayRequest
    {
        public Sound Sound = null!;
        public float Volume;
    }

    // Free-list of PlayRequest instances. Capped so a sudden burst
    // can't grow the pool unboundedly — once full, extras get GC'd
    // like any other short-lived object.
    private static readonly ConcurrentQueue<PlayRequest> _requestPool = new();
    private const int RequestPoolCap = 64;

    private static PlayRequest RentRequest(Sound sound, float volume)
    {
        if (!_requestPool.TryDequeue(out var req))
            req = new PlayRequest();
        req.Sound = sound;
        req.Volume = volume;
        return req;
    }

    private static void ReturnRequest(PlayRequest req)
    {
        req.Sound = null!;
        if (_requestPool.Count < RequestPoolCap)
            _requestPool.Enqueue(req);
    }

    private static Task PlayOnAppThread(Sound data, float volume)
    {
        AudioThread.Assert();
        EnsureInit();

        // Reuse a single open logical device across plays. The device 
        // keeps a pool of long-lived AudioStreams (one per AudioSpec,
        // up to MaxConcurrentPlays slots total) so we don't churn SDL
        // stream creation / destruction on every sound effect.
        var device = GetSharedPlaybackDevice();

        // Periodically tear down + reopen the shared device AND the
        // SDL audio subsystem. SDL3 on Windows / WASAPI accumulates
        // process-wide internal state that eventually access-violates
        // inside PutAudioStreamData (and related stream calls) after
        // sustained playback — neither pool churn, per-stream
        // rotation, native-buffer pinning, a native version bump
        // (3.2.20 → 3.2.24), nor device-only rotation prevented it.
        // Subsystem rotation is the only managed-side workaround
        // we've found that survives indefinite play.
        if (DeviceRotationPlayCount > 0)
        {
            int plays = Interlocked.Increment(ref _sharedDevicePlayCount);
            // Defer rotation until the device is quiescent so we
            // never cut an ongoing sound (e.g. background music).
            // The counter stays above the threshold; subsequent
            // plays re-check on each call.
            if (plays >= DeviceRotationPlayCount && device.IsQuiescent)
            {
                LogicalPlaybackDevice? toDispose = null;
                lock (_sharedPlaybackDeviceLock)
                {
                    if (_sharedDevicePlayCount >= DeviceRotationPlayCount
                        && _sharedPlaybackDevice == device
                        && device.IsQuiescent)
                    {
                        toDispose = device;
                        _sharedPlaybackDevice = null;
                        _playbackDevices = null;
                        _sharedDevicePlayCount = 0;
                    }
                }
                if (toDispose != null)
                {
                    // Synchronous on the app thread: dispose the
                    // device, bounce the subsystem refcount to zero
                    // so SDL discards accumulated audio state, then
                    // reinit and play on the fresh device. Rotation
                    // still waits for quiescence, so no audible tail
                    // is cut, but the play that happened to arrive at
                    // the first idle moment is no longer dropped.
                    toDispose.Dispose();
                    if (_audioSubsystemInitialized)
                    {
                        SDL.QuitSubSystem(SDL.InitFlags.Audio);
                        _audioSubsystemInitialized = false;
                    }
                    EnsureInit();
                    device = GetSharedPlaybackDevice();
                }
            }
        }

        try
        {
            return device.PlayAsync(data, volume);
        }
        catch (InvalidOperationException) when (device.IsDisposed)
        {
            // The shared device invalidated itself mid-call (e.g. the
            // OS swapped audio endpoints). Open a fresh one and retry
            // once so a transient SDL hiccup doesn't kill audio for the
            // rest of the process. Flush the cached physical-device
            // enumeration too — those handles are produced by SDL at
            // enumeration time and can become stale across an endpoint
            // change, so reusing them just causes SDL_OpenAudioDevice
            // to fail with "Invalid audio device instance ID".
            lock (_sharedPlaybackDeviceLock)
            {
                if (_sharedPlaybackDevice == device)
                    _sharedPlaybackDevice = null;
                _playbackDevices = null;
            }
            return GetSharedPlaybackDevice().PlayAsync(data, volume);
        }
    }

    // Cap matches what real game-audio mixers typically expose. Used
    // as the pool capacity on the shared playback device.
    private const int MaxConcurrentPlays = 8;

    /// <summary>
    /// Plays after which the shared playback device is torn down and
    /// reopened. Workaround for a sustained-playback SDL3 / WASAPI
    /// crash inside PutAudioStreamData; set to 0 to disable rotation.
    /// </summary>
    public static int DeviceRotationPlayCount { get; set; } = 64;

    private static int _sharedDevicePlayCount;

    private static LogicalPlaybackDevice? _sharedPlaybackDevice;
    private static readonly object _sharedPlaybackDeviceLock = new();

    private static LogicalPlaybackDevice GetSharedPlaybackDevice()
    {
        var device = _sharedPlaybackDevice;
        if (device != null && !device.IsDisposed)
            return device;

        lock (_sharedPlaybackDeviceLock)
        {
            if (_sharedPlaybackDevice == null || _sharedPlaybackDevice.IsDisposed)
            {
                try
                {
                    _sharedPlaybackDevice = DefaultPlaybackDevice.Open();
                }
                catch (InvalidOperationException)
                {
                    // The cached physical-device id from the last
                    // SDL_GetAudioPlaybackDevices enumeration may have
                    // gone stale (e.g. the OS swapped endpoints while
                    // audio was idle). Flush the enumeration and retry
                    // once against the freshly-queried default device.
                    _playbackDevices = null;
                    _sharedPlaybackDevice = DefaultPlaybackDevice.Open();
                }

                _sharedPlaybackDevice.PoolCapacity = MaxConcurrentPlays;

                // Register so Application.Dispose tears the device
                // (and all its streams) down BEFORE SDL.Quit runs.
                // Otherwise stream-disposal continuations scheduled by
                // PlayAsync's Task.Delay can fire post-Quit and pass
                // freed handles to SDL_DestroyAudioStream, causing
                // STATUS_HEAP_CORRUPTION (0xC0000374).
                Application.Current.AddResource(_sharedPlaybackDevice);
            }
            return _sharedPlaybackDevice;
        }
    }

    /// <summary>
    /// Called by the application event loop when SDL reports the
    /// underlying audio device was removed or its format changed.
    /// Either case invalidates every stream we have bound to it —
    /// continuing to call SDL_PutAudioStreamData /
    /// SDL_SetAudioStreamGain against those streams crashes the CLR
    /// with ExecutionEngineException because SDL has freed the
    /// stream's device pointer internally. Drop the shared device so
    /// the next play opens a fresh one.
    /// </summary>
    internal static void OnPlaybackDeviceLost(uint deviceId)
    {
        AudioThread.Assert();
        LogicalPlaybackDevice? toDispose = null;
        lock (_sharedPlaybackDeviceLock)
        {
            var device = _sharedPlaybackDevice;
            if (device == null || device.IsDisposed)
                return;
            // Match the lost id against the physical device backing
            // our logical device. SDL emits the physical id; our
            // logical id is what SDL_OpenAudioDevice returned, which
            // is different. We can't precisely compare, so on any
            // playback-side device loss we conservatively rebuild —
            // misfires only cost one device reopen.
            toDispose = device;
            _sharedPlaybackDevice = null;
            _playbackDevices = null;
            _sharedDevicePlayCount = 0;
        }
        // Dispose outside the lock; LogicalPlaybackDevice.Dispose
        // itself takes locks and we don't want to nest.
        toDispose?.Dispose();
    }

    private static ImmutableList<AudioPlaybackDevice>? _playbackDevices;
    private static ImmutableList<AudioRecordingDevice>? _recordingDevices;
    private static ImmutableList<string>? _driverNames;

    public static AudioPlaybackDevice DefaultPlaybackDevice =>
        Audio.PlaybackDevices.Count > 0 
            ? Audio.PlaybackDevices[0] 
            : throw new InvalidOperationException("No playback devices available.");

    public static AudioRecordingDevice DefaultRecordingDevice =>
        Audio.RecordingDevices.Count > 0 
            ? Audio.RecordingDevices[0] 
            : throw new InvalidOperationException("No recording devices available.");

    /// <summary>
    /// The set of available audio playback devices.
    /// </summary>
    public static ImmutableList<AudioPlaybackDevice> PlaybackDevices
    {
        get
        {
            EnsureInit();
            var devices = _playbackDevices;
            if (devices == null)
            {
                var ids = SDL.GetAudioPlaybackDevices(out var count);
                if (ids == null)
                    throw new InvalidOperationException("Unable to get audio playback devices.");

                if (count > 0)
                {
                    devices = ids.Select(id => new AudioPlaybackDevice(id)).ToImmutableList();
                }
                else
                {
                    devices = ImmutableList<AudioPlaybackDevice>.Empty;
                }

                Interlocked.CompareExchange(ref _playbackDevices, devices, null);
            }
            return _playbackDevices!;
        }
    }

    /// <summary>
    /// The set of available audio recording devices.
    /// </summary>
    public static ImmutableList<AudioRecordingDevice> RecordingDevices
    {
        get
        {
            EnsureInit();
            var devices = _recordingDevices;
            if (devices == null)
            {
                var ids = SDL.GetAudioRecordingDevices(out var count);
                if (ids != null && count > 0)
                {
                    devices = ids.Select(id => new AudioRecordingDevice(id)).ToImmutableList();
                }
                else
                {
                    devices = ImmutableList<AudioRecordingDevice>.Empty;
                }
                Interlocked.CompareExchange(ref _recordingDevices, devices, null);
            }
            return _recordingDevices!;
        }
    }

    /// <summary>
    /// The name of the current audio driver.
    /// </summary>
    public static string CurrentDriver
    {
        get
        {
            EnsureInit();
            return SDL.GetCurrentAudioDriver() ?? "";
        }
    }

    /// <summary>
    /// The names of all built-in audio drivers.
    /// </summary>
    public static ImmutableList<string> Drivers
    {
        get
        {
            EnsureInit();
            var driverNames = _driverNames;
            if (driverNames == null)
            {
                if (SDL.GetNumAudioDrivers() is { } count
                    && count > 0)
                {
                    driverNames = Enumerable.Range(0, count)
                        .Select(i => SDL.GetAudioDriver(i))
                        .Where(name => name != null)
                        .ToImmutableList()!;
                }
                else
                {
                    driverNames = ImmutableList<string>.Empty;
                }
                Interlocked.CompareExchange(ref _driverNames, driverNames, null);
            }
            return _driverNames!;
        }
    }
}
