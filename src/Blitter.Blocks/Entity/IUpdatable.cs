namespace Blitter.Blocks;

/// <summary>
/// Implemented by behaviors that do per-tick work.
/// </summary>
public interface IUpdatable
{
    /// <summary>
    /// Advance this behavior by one tick.
    /// </summary>
    void Update(in EntityUpdateContext context);
}

/// <summary>
/// Allows entity operations to skip disabled entities and their subtrees.
/// </summary>
public interface IUpdatability
{
    bool Enabled { get; }
}

