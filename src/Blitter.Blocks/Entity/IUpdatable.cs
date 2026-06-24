namespace Blitter.Blocks;

/// <summary>
/// Implemented by behaviors that do per-tick work.
/// </summary>
public interface IUpdatable
{
    /// <summary>
    /// Advance this behavior by one tick.
    /// </summary>
    void Update(in UpdateContext context);
}