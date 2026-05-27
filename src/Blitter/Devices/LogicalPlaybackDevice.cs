using System.Collections.Immutable;
using System.Diagnostics;

namespace Blitter.Devices;

/// <summary>
/// An opened audio device that can play audio streams.
/// </summary>
public class LogicalPlaybackDevice : AudioPlaybackDevice, IDisposable
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
            // Acquire the pool lock so we can't tear down a stream
            // that another thread is mid-call against inside
            // PlayAsync. Without this, a parallel
            // SetAudioStreamGain / PutAudioStreamData on a stream we
            // just destroyed crashes the CLR with
            // ExecutionEngineException.
            lock (_poolLock)
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
    /// The user-facing master volume of the audio device, from 0.0 (silent) to 1.0 (full volume). 
    /// The actual SDL device gain also has an automatic attenuation applied that scales with
    /// the number of concurrent streams (equal-loudness 1/sqrt(N) mix law), 
    /// so several overlapping plays don't sum to a clipping signal.
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
        AudioThread.Assert();
        if (_deviceId == 0)
            return;
        // Count slots that are still within their known playback
        // window. SDL's queued-byte counts are unreliable for this
        // (the same shortcoming that forced PlayAsync's
        // known-duration scheduling in the first place), so we trust
        // the slot's own busy-until timestamp set when its play was
        // queued.
        int active = 0;
        long now = Stopwatch.GetTimestamp();
        lock (_poolLock)
        {
            foreach (var bucket in _pool.Values)
            {
                for (int i = 0; i < bucket.Count; i++)
                {
                    if (bucket[i].BusyUntilTicks > now)
                        active++;
                }
            }
        }
        var count = Math.Max(1, active);
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
    /// Maximum number of pooled streams kept alive for fire-and-forget <see cref="PlayAsync"/>. 
    /// Each distinct <see cref="AudioSpec"/> shares this cap. 
    /// Pool slots are created lazily and never destroyed until the device itself disposes.
    /// </summary>
    public int PoolCapacity { get; set; } = 8;

    /// <summary>
    /// True when no pooled stream is currently within its known playback window. 
    /// Used by <see cref="Audio"/> to defer disruptive rotation until ongoing sounds 
    /// (long tracks like background music) have finished, so a rotation never cuts
    /// audible audio.
    /// </summary>
    public bool IsQuiescent
    {
        get
        {
            long now = Stopwatch.GetTimestamp();
            lock (_poolLock)
            {
                foreach (var bucket in _pool.Values)
                {
                    for (int i = 0; i < bucket.Count; i++)
                    {
                        if (bucket[i].BusyUntilTicks > now)
                            return false;
                    }
                }
            }
            return true;
        }
    }

    private sealed class PoolSlot
    {
        public required AudioStream Stream { get; init; }
        // Stopwatch timestamp at/after which this slot is considered
        // idle and free to reuse. Time-based rather than
        // SDL_GetAudioStreamQueued-based because SDL3's queued-byte
        // reporting can lag (or never reach zero) after a stream
        // drains — the same issue that makes its completion callback
        // unreliable.
        public long BusyUntilTicks;
        // Last gain value we wrote to this stream via SDL. Reused so
        // we can skip redundant SDL_SetAudioStreamGain calls when the
        // caller plays the same sound at the same volume repeatedly.
        // The audio thread continuously reads the stream, so every
        // gain write is a thread-race with SDL's mixer — minimizing
        // them is both faster and safer.
        public float LastVolume = float.NaN;
    }

    private readonly Dictionary<AudioSpec, List<PoolSlot>> _pool = new();
    // Per-bucket round-robin cursor. Idle-slot search starts here so
    // we spread plays evenly across the pool instead of hammering
    // the first slot. Concentrated reuse on a single slot ages it
    // disproportionately and exposes whatever per-stream rot SDL3
    // accumulates.
    private readonly Dictionary<AudioSpec, int> _poolCursor = new();
    private readonly object _poolLock = new();
    private int _pooledCount;

    /// <summary>
    /// Play the specified audio data on the device.
    /// </summary>
    public override Task PlayAsync(Sound data, float volume = 1f)
    {
        AudioThread.Assert();
        var duration = data.Duration;
        var slack = TimeSpan.FromMilliseconds(50);
        var busyTicks = (long)((duration + slack).TotalSeconds * Stopwatch.Frequency);

        AudioStream stream;
        lock (_poolLock)
        {
            if (_deviceId == 0)
                throw new InvalidOperationException("Device is disposed.");

            if (!_pool.TryGetValue(data.Spec, out var bucket))
            {
                bucket = new List<PoolSlot>();
                _pool[data.Spec] = bucket;
            }

            long now = Stopwatch.GetTimestamp();

            // Find a slot whose busy-until window has elapsed,
            // starting from the round-robin cursor so reuse spreads
            // across the bucket.
            PoolSlot? slot = null;
            int n = bucket.Count;
            if (n > 0)
            {
                _poolCursor.TryGetValue(data.Spec, out int cursor);
                for (int k = 0; k < n; k++)
                {
                    int i = (cursor + k) % n;
                    var s = bucket[i];
                    if (!s.Stream.IsDisposed && s.BusyUntilTicks <= now)
                    {
                        slot = s;
                        _poolCursor[data.Spec] = (i + 1) % n;
                        break;
                    }
                }
            }

            if (slot == null)
            {
                if (_pooledCount < PoolCapacity)
                {
                    // Pool isn't full yet for this device — add a new
                    // slot. New streams stay bound for the device's
                    // lifetime; subsequent plays reuse them.
                    var newStream = CreateStream(data.Spec);
                    slot = new PoolSlot { Stream = newStream };
                    bucket.Add(slot);
                    _pooledCount++;
                    // SDL opens devices unpaused by default, but call
                    // resume defensively at first-slot creation. Pause
                    // is per-device, not per-stream, so calling it on
                    // every play is wasted SDL work.
                    newStream.Paused = false;
                }
                else
                {
                    // Pool full and every slot is still mid-play.
                    // Rather than going silent, steal the slot whose
                    // remaining window is shortest — its tail gets
                    // cut off but the new play is heard. Keeps audio
                    // responsive during sustained bursts.
                    long minBusy = long.MaxValue;
                    for (int i = 0; i < bucket.Count; i++)
                    {
                        var s = bucket[i];
                        if (s.Stream.IsDisposed) continue;
                        if (s.BusyUntilTicks < minBusy)
                        {
                            minBusy = s.BusyUntilTicks;
                            slot = s;
                        }
                    }
                    if (slot == null)
                        return Task.CompletedTask;
                    // Trim any leftover queue bytes before re-queuing
                    // so the new sound starts cleanly.
                    slot.Stream.Clear();
                }
            }

            slot.BusyUntilTicks = now + busyTicks;
            stream = slot.Stream;

            // Configure under the pool lock so concurrent plays can't
            // both claim the same idle slot and trample each other's
            // queue contents mid-write. Skip the gain write when it
            // hasn't changed since the slot's last play — every
            // SDL_SetAudioStreamGain races with SDL's audio thread
            // reading the stream, and most plays use the same volume.
            var clampedVolume = Math.Clamp(volume, 0f, 1f);
            if (clampedVolume != slot.LastVolume)
            {
                stream.Volume = clampedVolume;
                slot.LastVolume = clampedVolume;
            }
            stream.Queue(data);
        }

        ApplyEffectiveGain();

        // The returned task completes when this play's busy window
        // ends. We deliberately do NOT recompute the 1/sqrt(N) mix
        // gain on completion — the next play recomputes it from its
        // own start-of-play call (slots whose BusyUntilTicks have
        // elapsed are excluded), so the only audible difference is
        // that the first play after a long silence is mixed against
        // the trailing attenuation from the prior burst. That's
        // both inaudible in practice and avoids half the
        // SDL_SetAudioDeviceGain traffic.
        return Task.Delay(duration + slack);
    }

    #region Audio Streams

    /// <summary>
    /// The set of current audio streams.
    /// </summary>
    public ImmutableList<AudioStream> Streams => _streams;

    public AudioStream CreateStream(AudioSpec sourceSpec, AudioDataRequested? onDataRequested = null)
    {
        AudioThread.Assert();
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
