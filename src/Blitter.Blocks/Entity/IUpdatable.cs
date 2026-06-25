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
/// Allows entity operations to skip disabled entities.
/// </summary>
public interface IUpdateEnabled
{
    bool Enabled { get; }
}

/// <summary>
/// Implemented by containers whose update implementation owns child traversal.
/// </summary>
public interface IUpdateTraversalOwner
{
}