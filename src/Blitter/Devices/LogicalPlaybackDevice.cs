using System.Collections.Immutable;

namespace Blitter.Devices;

/// <summary>
/// An opened audio device that can play audio streams.
/// </summary>
public class LogicalPlaybackDevice : AudioPlaybackDevice
{
    private ImmutableList<AudioStream> _streams = ImmutableList<AudioStream>.Empty;

    internal LogicalPlaybackDevice(uint deviceId)
        : base(deviceId)
    {
    }

    public bool IsDisposed => _deviceId == 0;

    public void Dispose()
    {
        if (!IsDisposed)
        {
            var id = Interlocked.Exchange(ref _deviceId, 0);
            if (id != 0)
            {
                foreach (var stream in _streams)
                {
                    stream.Dispose();
                }

                SDL.CloseAudioDevice(id);
            }
        }
    }

    /// <summary>
    /// The volume of the audio device, from 0.0 (silent) to 1.0 (full volume).
    /// </summary>
    public override float Volume
    {
        get => SDL.GetAudioDeviceGain(_deviceId);
        set => SDL.SetAudioDeviceGain(_deviceId, value);
    }

    /// <summary>
    /// Play the specified audio data on the device.
    /// </summary>
    public override Task PlayAsync(Sound data, float volume = 1f)
    {
        var stream = CreateStream(data.Spec);
        stream.Volume = Math.Clamp(volume, 0f, 1f);
        stream.Queue(data);
        stream.Paused = false;

        // SDL's "get more data" callback stops firing once the queue drains,
        // so we can't reliably observe playback completion through it. Wait
        // for the known length of the sample instead. A small slack covers
        // SDL's own device-side buffer that keeps playing after the stream's
        // queue hits zero.
        var duration = GetPlaybackDuration(data);
        var slack = TimeSpan.FromMilliseconds(50);
        return Task.Delay(duration + slack).ContinueWith(_ =>
        {
            if (!stream.IsDisposed)
                stream.Dispose();
        }, TaskScheduler.Default);
    }

    private static TimeSpan GetPlaybackDuration(Sound data)
    {
        var spec = data.Spec;
        // Low byte of the SDL format encodes the bit depth.
        int bitsPerSample = (int)((uint)spec.Format & 0xFF);
        int bytesPerFrame = Math.Max(1, (bitsPerSample / 8) * Math.Max(1, spec.Channels));
        int frames = data.Data.Length / bytesPerFrame;
        if (spec.Frequency <= 0)
            return TimeSpan.Zero;
        return TimeSpan.FromSeconds((double)frames / spec.Frequency);
    }

    #region Audio Streams

    /// <summary>
    /// The set of current audio streams.
    /// </summary>
    public ImmutableList<AudioStream> Streams => _streams;

    public AudioStream CreateStream(AudioSpec sourceSpec, AudioDataRequested? onDataRequested = null)
    {
        var streamId = SDL.CreateAudioStream(sourceSpec.ToSdl(), this.Spec.ToSdl());
        if (streamId == 0)
            throw new InvalidOperationException($"SDL_OpenAudioDeviceStream Error: {SDL.GetError()}");

        if (!SDL.BindAudioStream(_deviceId, streamId))
            throw new InvalidOperationException($"SDL_BindAudioStream Error: {SDL.GetError()}");

        return new AudioStream(this, streamId, onDataRequested);
    }

    internal void AddStream(AudioStream stream)
    {
        ImmutableInterlocked.Update(ref _streams, (list) => list.Add(stream));
    }

    internal void RemoveStream(AudioStream stream)
    {
        ImmutableInterlocked.Update(ref _streams, (list) => list.Remove(stream));
    }

    #endregion
}
