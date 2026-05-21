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
    /// Tear down the device after SDL has already invalidated its
    /// underlying handle (e.g. SDL_BindAudioStream failed because
    /// the OS swapped audio endpoints). Every existing stream id is
    /// already stale, so we orphan them — zeroing their ids without
    /// any SDL calls — and skip SDL_CloseAudioDevice on the dead id.
    /// </summary>
    private void DisposeOrphaned()
    {
        if (!IsDisposed)
        {
            Interlocked.Exchange(ref _deviceId, 0);
            foreach (var stream in _streams)
            {
                stream.Orphan();
            }
            _streams = ImmutableList<AudioStream>.Empty;
        }
    }

    /// <summary>
    /// The user-facing master volume of the audio device, from 0.0
    /// (silent) to 1.0 (full volume). The actual SDL device gain
    /// also has an automatic attenuation applied that scales with
    /// the number of concurrent streams (equal-loudness 1/sqrt(N)
    /// mix law), so several overlapping plays don't sum to a
    /// clipping signal.
    /// </summary>
    public override float Volume
    {
        get => _userVolume;
        set
        {
            _userVolume = Math.Clamp(value, 0f, 1f);
            ApplyEffectiveGain();
        }
    }

    private float _userVolume = 1f;
    private float _lastAppliedGain = float.NaN;
    private readonly object _gainLock = new();

    private void ApplyEffectiveGain()
    {
        if (_deviceId == 0)
            return;
        var count = Math.Max(1, _streams.Count);
        var gain = _userVolume / MathF.Sqrt(count);
        lock (_gainLock)
        {
            // SDL_SetAudioDeviceGain is documented thread-safe but
            // hammering it from many worker threads on every stream
            // start/stop is a needless concurrency hazard. Skip no-op
            // writes and serialize the rest behind a tiny lock.
            if (_deviceId == 0 || gain == _lastAppliedGain)
                return;
            SDL.SetAudioDeviceGain(_deviceId, gain);
            _lastAppliedGain = gain;
        }
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
        var destSpec = this.Spec;
        if ((uint)destSpec.Format == 0)
            throw new InvalidOperationException(
                "Audio device has no valid format; SDL_GetAudioDeviceFormat returned an empty spec.");
        var streamId = SDL.CreateAudioStream(sourceSpec.ToSdl(), destSpec.ToSdl());
        if (streamId == 0)
            throw new InvalidOperationException($"SDL_OpenAudioDeviceStream Error: {SDL.GetError()}");

        if (!SDL.BindAudioStream(_deviceId, streamId))
        {
            // The bind failed — almost always because SDL has
            // invalidated the underlying device id (e.g. the OS audio
            // endpoint changed). Every stream id already bound to
            // this device is now stale, so we MUST NOT call
            // SDL_DestroyAudioStream on them — that crashes the CLR
            // with ExecutionEngineException. Orphan them all (zero
            // their ids in-place) and skip SDL_CloseAudioDevice on
            // the dead id. The orphan stream we just created via
            // SDL_CreateAudioStream is _not_ yet bound, so it is
            // safe to destroy.
            var error = SDL.GetError();
            SDL.DestroyAudioStream(streamId);
            DisposeOrphaned();
            throw new InvalidOperationException($"SDL_BindAudioStream Error: {error}");
        }

        return new AudioStream(this, streamId, onDataRequested);
    }

    internal void AddStream(AudioStream stream)
    {
        ImmutableInterlocked.Update(ref _streams, (list) => list.Add(stream));
        ApplyEffectiveGain();
    }

    internal void RemoveStream(AudioStream stream)
    {
        ImmutableInterlocked.Update(ref _streams, (list) => list.Remove(stream));
        ApplyEffectiveGain();
    }

    #endregion
}
