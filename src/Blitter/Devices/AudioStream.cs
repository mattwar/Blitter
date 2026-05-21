namespace Blitter.Devices;

public delegate void AudioDataRequested(AudioStream stream, int additionalAmount, int totalAmount);

public class AudioStream : IDisposable
{
    private LogicalPlaybackDevice _device;
    private nint _streamId;
    private readonly AudioDataRequested? _onDataRequested;

    // Hold the SDL callback delegate in a field so the GC can't collect
    // it while SDL still holds the raw function pointer. Without this,
    // a quiet period (no plays for tens of seconds) is enough for the
    // delegate to be reaped and the next SDL invocation crashes with a
    // NullReferenceException that has no managed stack.
    // Null when no caller-supplied pull callback was provided — in
    // pre-queued playback (Audio.PlayAsync) we don't need SDL to
    // marshal back into managed code at all.
    private readonly SDL.AudioStreamCallback? _getDataCallback;

    internal AudioStream(LogicalPlaybackDevice device, nint streamId, AudioDataRequested? onDataRequested = null)
    {
        _device = device;
        _streamId = streamId;
        _onDataRequested = onDataRequested;

        if (onDataRequested != null)
        {
            _getDataCallback = GetDataCallback;
            SDL.SetAudioStreamGetCallback(_streamId, _getDataCallback, nint.Zero);
        }

        // Register with the owning device so Audio's MaxConcurrentPlays
        // cap is enforced and so device-level teardown can dispose us
        // before SDL_CloseAudioDevice invalidates our stream id from
        // under us. Without this, streams were untracked and a stale
        // id later passed to SDL_DestroyAudioStream crashed the CLR
        // with ExecutionEngineException.
        device.AddStream(this);
    }

    private void GetDataCallback(nint userdata, nint stream, int additionalAmount, int totalAmount)
    {
        _onDataRequested?.Invoke(this, additionalAmount, totalAmount);
    }

    public bool IsDisposed => _streamId == 0;

    /// <summary>
    /// Zero our stream id without calling any SDL functions. Used by
    /// the owning device when it has detected SDL has invalidated the
    /// underlying device handle (e.g. SDL_BindAudioStream failed):
    /// every stream id bound to that device is already stale, so
    /// calling SDL_DestroyAudioStream on them crashes the process.
    /// </summary>
    internal void Orphan()
    {
        Interlocked.Exchange(ref _streamId, 0);
        _device = null!;
    }

    public void Dispose()
    {
        if (!IsDisposed)
        {
            var id = Interlocked.Exchange(ref _streamId, 0);
            if (id != 0)
            {
                // Clear the SDL callback before destroying the stream so
                // a callback in flight on the audio thread can't be
                // marshalled into a delegate that's about to become
                // unreachable. SDL_DestroyAudioStream alone is supposed
                // to be safe, but explicitly unhooking first is cheap
                // insurance against native crashes.
                // NOTE: do NOT call SDL_PauseAudioStreamDevice here —
                // despite the name it pauses the entire bound device,
                // not just this stream, which silences every other
                // stream sharing the device.
                if (_getDataCallback != null)
                    SDL.SetAudioStreamGetCallback(id, null!, nint.Zero);
                _device.RemoveStream(this);
                _device = null!;
                SDL.DestroyAudioStream(id);
            }
        }
    }

    public float Volume
    {
        get
        {
            return IsDisposed 
                ? 0f
                : SDL.GetAudioStreamGain(_streamId);
        }
        set
        {
            if (value < 0.0f || value > 1.0f)
                throw new ArgumentOutOfRangeException(nameof(value), "Volume must be between 0.0 and 1.0");
            if (!IsDisposed)
            {
                SDL.SetAudioStreamGain(_streamId, value);
            }
        }
    }

    public int QueuedBytes
    {
        get
        {
            return IsDisposed
                ? 0
                : SDL.GetAudioStreamQueued(_streamId);
        }
    }

    /// <summary>
    /// True if the stream is paused.
    /// </summary>
    public bool Paused
    {
        get
        {
            return IsDisposed
                ? true
                : SDL.AudioStreamDevicePaused(_streamId);
        }
        set
        {
            if (IsDisposed)
                return;
            if (value)
                SDL.PauseAudioStreamDevice(_streamId);
            else
                SDL.ResumeAudioStreamDevice(_streamId);
        }
    }

    /// <summary>
    /// Clears any queued audio data in the stream.
    /// </summary>
    /// <exception cref="ObjectDisposedException"></exception>
    public void Clear()
    {
        if (IsDisposed)
            return;
        SDL.ClearAudioStream(_streamId);
    }

    /// <summary>
    /// Flushes any queued audio data to the device (aka, play it now).
    /// </summary>
    public void Flush()
    {
        if (IsDisposed)
            return;
        SDL.FlushAudioStream(_streamId);
    }

    /// <summary>
    /// Queues audio data to be played on the stream.
    /// </summary>
    public void Queue(Sound data)
    {
        unsafe
        {
            var span = data.Data.Span;
            fixed (byte* pData = span)
            {
                SDL.PutAudioStreamData(_streamId, (nint)pData, span.Length);
            }
        }
    }
}
