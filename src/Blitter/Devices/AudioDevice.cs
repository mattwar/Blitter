namespace Blitter.Devices;

/// <summary>
/// The base class for audio devices, either playback or recording.
/// </summary>
public abstract class AudioDevice
{
    private protected uint _deviceId;
    // SDL's device spec is fixed for the lifetime of the open device,
    // so cache it after the first successful query. Querying SDL on
    // every access is both wasteful and unreliable — a transient
    // failure of SDL_GetAudioDeviceFormat would otherwise surface as
    // `Spec == default`, i.e. Format == 0, which downstream calls like
    // SDL_CreateAudioStream reject as an invalid format.
    private AudioSpec? _cachedSpec;
    private int _cachedSampleFrames;

    internal AudioDevice(uint deviceId)
    {
        _deviceId = deviceId;
    }

    /// <summary>
    /// The name of the audio device.
    /// </summary>
    public string Name =>
        SDL.GetAudioDeviceName(_deviceId) ?? "";

    /// <summary>
    /// The specifications of the audio device.
    /// </summary>
    public AudioSpec Spec
    {
        get
        {
            if (_cachedSpec is { } cached)
                return cached;
            EnsureSpecCached();
            return _cachedSpec ?? default;
        }
    }

    /// <summary>
    /// The number of sample frames in the audio device's buffer.
    /// </summary>
    public int SampleFrames
    {
        get
        {
            if (_cachedSpec is null)
                EnsureSpecCached();
            return _cachedSampleFrames;
        }
    }

    private void EnsureSpecCached()
    {
        if (_deviceId == 0)
            return;
        if (SDL.GetAudioDeviceFormat(_deviceId, out var spec, out var frames))
        {
            _cachedSpec = AudioSpec.From(spec);
            _cachedSampleFrames = frames;
        }
    }

    /// <summary>
    /// The volume of the audio device, from 0.0 (silent) to 1.0 (full volume).
    /// </summary>
    public virtual float Volume
    {
        get => -1f;
        set { }
    }
}
