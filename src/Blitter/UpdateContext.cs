namespace Blitter;

/// <summary>
/// Contract for a per-frame inputs struct passed to a stateful object's <c>Update</c> method.
/// </summary>
public interface IUpdateContext
{
    /// <summary>Wall-clock time since the host's clock started.</summary>
    TimeSpan ElapsedSinceStart { get; }

    /// <summary>
    /// Wall-clock time since the previous update, clamped by the host's
    /// frame-delta cap so a long pause doesn't teleport time-integrated
    /// state.
    /// </summary>
    TimeSpan ElapsedSinceLastUpdate { get; }

    /// <summary>
    /// <see cref="ElapsedSinceStart"/> as <c>float</c> seconds. Convenient
    /// for shader uniforms and animation phase math.
    /// </summary>
    float ElapsedSecondsSinceStart =>
        (float)ElapsedSinceStart.TotalSeconds;

    /// <summary>
    /// <see cref="ElapsedSinceLastUpdate"/> as <c>float</c> seconds.
    /// Convenient as a per-frame <c>dt</c> for time-integrated state.
    /// </summary>
    float ElapsedSecondsSinceLastUpdate =>
        (float)ElapsedSinceLastUpdate.TotalSeconds;
}


/// <summary>
/// 3D-domain update context. 
/// </summary>
public interface IUpdateContext3D : IUpdateContext
{
}

/// <summary>
/// Bare update context: timings only. 
/// </summary>
public readonly struct UpdateContext : IUpdateContext
{
    public TimeSpan ElapsedSinceStart { get; init; }
    public TimeSpan ElapsedSinceLastUpdate { get; init; }
}


/// <summary>
/// 3D update context: timings only so far.
/// </summary>
public readonly struct UpdateContext3D : IUpdateContext3D
{
    public TimeSpan ElapsedSinceStart { get; init; }
    public TimeSpan ElapsedSinceLastUpdate { get; init; }
}
