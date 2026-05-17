using Blitter.Bits;

namespace Blitter.Blocks;

/// <summary>
/// A static, non-sprite obstacle in a <see cref="PlayField2D"/>.
/// Participates in the collision pass: when a sprite's
/// <see cref="Sprite2D.HitCircle"/> overlaps the barrier's shape, the
/// playfield dispatches <see cref="Sprite2D.OnHitBarrier"/>. Barriers
/// don't update, don't get reaped, and don't have behaviors.
/// </summary>
public abstract class Barrier2D
{
    /// <summary>
    /// Optional free-form classifier (e.g. "wall", "deathzone",
    /// "goal-line") so handlers can switch on intent without
    /// subclassing.
    /// </summary>
    public string? Tag { get; init; }

    /// <summary>
    /// Optional caller-owned reference. Typed sibling to
    /// <see cref="Tag"/> for when a string isn't enough.
    /// </summary>
    public object? UserData { get; init; }

    /// <summary>
    /// True when <paramref name="circle"/> overlaps this barrier's
    /// shape. Called once per prop per tick during the playfield's
    /// collision pass.
    /// </summary>
    public abstract bool Intersects(BoundingCircle circle);
}
