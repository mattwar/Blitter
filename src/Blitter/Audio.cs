using System.Collections.Immutable;

using Blitter.Devices;

namespace Blitter;

public static class Audio
{
    /// <summary>
    /// Ensures the application is running and the SDL audio subsystem is
    /// initialized. Safe to call from any audio entry point; SDL ref-counts
    /// subsystem init so repeated calls are cheap.
    /// </summary>
    private static void EnsureInit()
    {
        _ = Application.Current;
        if (!SDL.InitSubSystem(SDL.InitFlags.Audio))
            throw new InvalidOperationException(
                $"Failed to initialize SDL audio subsystem: {SDL.GetError()}");
    }

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
        EnsureInit();

        // Reuse a single open logical device across plays. Opening /
        // closing a fresh SDL audio device for every fire-and-forget
        // `Play` call breaks down under load (hundreds of overlapping
        // sounds per second silently fail or get torn down before SDL
        // ever renders them). Each play still gets its own AudioStream
        // which self-disposes when its queue drains, so old plays are
        // reaped without churning the device handle.
        var device = GetSharedPlaybackDevice();

        // Soft cap: drop new plays once too many streams are stacked up.
        // Prevents a runaway sample (collision storm, stuck loop) from
        // exhausting SDL audio resources.
        if (device.Streams.Count >= MaxConcurrentPlays)
            return Task.CompletedTask;

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
            var fresh = GetSharedPlaybackDevice();
            if (fresh.Streams.Count >= MaxConcurrentPlays)
                return Task.CompletedTask;
            return fresh.PlayAsync(data, volume);
        }
    }

    // Cap matches what real game-audio mixers typically expose.
    private const int MaxConcurrentPlays = 32;

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
            }
            return _sharedPlaybackDevice;
        }
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
