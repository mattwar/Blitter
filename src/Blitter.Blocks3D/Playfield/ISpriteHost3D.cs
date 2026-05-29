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

    /// <summary>Removes a sprite from this host. The normal way to retire a sprite is to set <see cref="Sprite3D.IsAlive"/> to <c>false</c>; use this when a caller needs to evict a sprite without killing it.</summary>
    void RemoveSprite(Sprite3D sprite);

    /// <summary>Adds a barrier to this host.</summary>
    void AddBarrier(Barrier3D barrier);

    /// <summary>Removes a barrier from this host.</summary>
    void RemoveBarrier(Barrier3D barrier);
}
