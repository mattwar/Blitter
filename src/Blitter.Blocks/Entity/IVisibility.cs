namespace Blitter.Blocks;

/// <summary>
/// Allows draw operations to skip invisible entities and their subtrees.
/// </summary>
public interface IVisibility
{
    bool Visible { get; }
}