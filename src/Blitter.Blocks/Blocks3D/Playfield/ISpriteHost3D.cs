namespace Blitter.Blocks3D;
using Bits;

/// <summary>
/// What a <see cref="Sprite3D"/> sees as its container: a small surface
/// for spawn/despawn calls and time queries. Implemented by
/// <see cref="PlayField3D"/> today and by chunk-style containers later,
/// so behaviors can spawn projectiles or remove themselves without
/// knowing which concrete host owns the sprite.
/// </summary>
public interface ISpriteHost3D
{
    /// <summary>Host clock used as the reference for <see cref="Sprite3D.Age"/>.</summary>
    TimeSpan Elapsed { get; }

    /// <summary>Adds a sprite to this host.</summary>
    void AddSprite(Sprite3D sprite);

    /// <summary>Retires a sprite from this host. Safe to call while the host is updating; the sprite stops colliding immediately and is reaped at end of frame.</summary>
    void RemoveSprite(Sprite3D sprite);

    /// <summary>Reports whether <paramref name="child"/> is contained by this host, being removed this frame, or not held. A sprite is live when this returns <see cref="Containment.Contained"/>.</summary>
    Containment GetContainment(IEntity child);

    /// <summary>Adds a barrier to this host.</summary>
    void AddBarrier(Barrier3D barrier);

    /// <summary>Removes a barrier from this host.</summary>
    void RemoveBarrier(Barrier3D barrier);
}
