namespace Blitter.Blocks3D;

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

    /// <summary>Whether <paramref name="sprite"/> is a live member of this host — still held and not retired during the current frame.</summary>
    bool IsAlive(Sprite3D sprite);

    /// <summary>Adds a barrier to this host.</summary>
    void AddBarrier(Barrier3D barrier);

    /// <summary>Removes a barrier from this host.</summary>
    void RemoveBarrier(Barrier3D barrier);
}
