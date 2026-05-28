namespace Blitter.Blocks3D;

/// <summary>
/// The 3D "world" layer: owns a set of <see cref="Sprite3D"/>s and
/// <see cref="Barrier3D"/>s, drives their per-frame updates, and runs
/// the collision pass that dispatches <see cref="Sprite3D.OnHitSprite"/>
/// and <see cref="Sprite3D.OnHitBarrier"/>.
/// </summary>
public class PlayField3D : Layer3D
{
    private readonly List<Sprite3D> _sprites = new();
    private readonly List<Barrier3D> _barriers = new();

    // While Update is iterating we can't mutate _sprites/_barriers
    // directly. Adds/removes during update push into these pending
    // lists and the changes are applied at the end of the frame.
    private readonly List<Sprite3D> _pendingAddSprites = new();
    private readonly List<Sprite3D> _pendingRemoveSprites = new();
    private readonly List<Barrier3D> _pendingAddBarriers = new();
    private readonly List<Barrier3D> _pendingRemoveBarriers = new();
    private bool _updating;

    public PlayField3D()
    {
    }

    public PlayField3D(IEnumerable<Sprite3D> sprites)
    {
        AdoptSprites(sprites);
    }

    public PlayField3D(IEnumerable<Sprite3D> sprites, IEnumerable<Barrier3D> barriers)
    {
        AdoptSprites(sprites);
        foreach (var b in barriers)
            _barriers.Add(b);
    }

    private void AdoptSprites(IEnumerable<Sprite3D> sprites)
    {
        foreach (var s in sprites)
        {
            s._playField?.RemoveImmediate(s);
            s._playField = this;
            s._spawnedAt = Elapsed;
            _sprites.Add(s);
        }
    }

    /// <summary>The sprites currently in this playfield.</summary>
    public IReadOnlyList<Sprite3D> Sprites => _sprites;

    /// <summary>
    /// Static, non-sprite obstacles in this playfield. Tested against
    /// every sprite's <see cref="Sprite3D.HitSphere"/> each frame.
    /// </summary>
    public IReadOnlyList<Barrier3D> Barriers => _barriers;

    /// <summary>
    /// Total time accumulated from <see cref="UpdateContext3D"/> deltas
    /// passed through this playfield's <see cref="Update"/>. Used as
    /// the clock for <see cref="Sprite3D.Age"/>.
    /// </summary>
    public TimeSpan Elapsed { get; private set; }

    /// <summary>Adds a sprite to the playfield.</summary>
    public void AddSprite(Sprite3D sprite)
    {
        var existing = sprite._playField;
        if (existing == this)
        {
            _pendingRemoveSprites.Remove(sprite);
            return;
        }
        existing?.RemoveImmediate(sprite);
        sprite._playField = this;
        sprite._spawnedAt = Elapsed;
        if (_updating)
            _pendingAddSprites.Add(sprite);
        else
            _sprites.Add(sprite);
    }

    /// <summary>Adds multiple sprites to the playfield.</summary>
    public void AddSprites(IEnumerable<Sprite3D> sprites)
    {
        foreach (var s in sprites)
            AddSprite(s);
    }

    /// <summary>
    /// Removes a sprite from the playfield. Safe to call during
    /// <see cref="Update"/>; the actual removal is deferred to end of
    /// frame. The normal way to retire a sprite is to set
    /// <see cref="Sprite3D.IsAlive"/> to <c>false</c>. This method is
    /// for callers that need to evict a sprite without killing it.
    /// </summary>
    public void RemoveSprite(Sprite3D sprite)
    {
        if (sprite._playField != this)
            return;
        if (_updating)
        {
            _pendingAddSprites.Remove(sprite);
            _pendingRemoveSprites.Add(sprite);
        }
        else if (_sprites.Remove(sprite))
        {
            Detach(sprite, retired: true);
        }
    }

    /// <summary>Adds a barrier to the playfield.</summary>
    public void AddBarrier(Barrier3D barrier)
    {
        if (_updating)
            _pendingAddBarriers.Add(barrier);
        else
            _barriers.Add(barrier);
    }

    /// <summary>Adds multiple barriers to the playfield.</summary>
    public void AddBarriers(IEnumerable<Barrier3D> barriers)
    {
        var sink = _updating ? _pendingAddBarriers : _barriers;
        foreach (var b in barriers)
            sink.Add(b);
    }

    /// <summary>Removes a barrier from the playfield.</summary>
    public void RemoveBarrier(Barrier3D barrier)
    {
        if (_updating)
            _pendingRemoveBarriers.Add(barrier);
        else
            _barriers.Remove(barrier);
    }

    // Reparenting helper: yanks `sprite` out of this playfield right now,
    // bypassing the pending pipeline so AddSprite on a different playfield
    // can immediately re-adopt it.
    internal void RemoveImmediate(Sprite3D sprite)
    {
        _pendingAddSprites.Remove(sprite);
        _pendingRemoveSprites.Remove(sprite);
        if (_sprites.Remove(sprite))
            Detach(sprite, retired: false);
    }

    private void Detach(Sprite3D sprite, bool retired)
    {
        if (sprite._playField == this)
            sprite._playField = null;
        if (retired)
            OnSpriteRetired(sprite);
    }

    /// <summary>
    /// Called once for each sprite that leaves this playfield, either
    /// because its <see cref="Sprite3D.IsAlive"/> went to <c>false</c>
    /// or because <see cref="RemoveSprite"/> evicted it. Not called when
    /// a sprite is reparented into another playfield. Override to
    /// return the sprite to a pool, recycle resources, etc.
    /// </summary>
    protected virtual void OnSpriteRetired(Sprite3D sprite)
    {
    }

    /// <inheritdoc/>
    public override void Update(in UpdateContext3D context)
    {
        Elapsed += context.ElapsedSinceLastUpdate;

        _updating = true;
        try
        {
            RunOneStep(context);
        }
        finally
        {
            _updating = false;
        }

        ApplyPendingChanges();
    }

    private void RunOneStep(in UpdateContext3D spriteContext)
    {
        // Animated barriers tick before sprites so this frame's
        // sprite-vs-barrier pass sees the new geometry.
        for (int i = 0; i < _barriers.Count; i++)
            _barriers[i].Update(spriteContext);

        for (int i = 0; i < _sprites.Count; i++)
        {
            var sprite = _sprites[i];
            if (!sprite.IsAlive)
                continue;
            sprite.Update(spriteContext);
        }

        // sprite-vs-sprite collision
        for (int i = 0; i < _sprites.Count; i++)
        {
            var a = _sprites[i];
            if (!a.IsAlive || !a.CanBeHit)
                continue;
            var aShape = a.HitShape;
            if (aShape.BoundingSphere.Radius <= 0f)
                continue;

            for (int j = i + 1; j < _sprites.Count; j++)
            {
                if (!a.IsAlive)
                    break;

                var b = _sprites[j];
                if (!b.IsAlive || !b.CanBeHit)
                    continue;
                var bShape = b.HitShape;
                if (bShape.BoundingSphere.Radius <= 0f)
                    continue;
                if (!aShape.TestHit(bShape))
                    continue;

                a.OnHitSprite(b, spriteContext);
                if (a.IsAlive && b.IsAlive)
                    b.OnHitSprite(a, spriteContext);
            }
        }

        // sprite-vs-barrier collision
        for (int i = 0; i < _sprites.Count; i++)
        {
            var sprite = _sprites[i];
            if (!sprite.IsAlive || !sprite.CanBeHit)
                continue;
            var spriteShape = sprite.HitShape;
            if (spriteShape.BoundingSphere.IsEmpty)
                continue;

            for (int j = 0; j < _barriers.Count; j++)
            {
                if (!sprite.IsAlive)
                    break;
                var barrier = _barriers[j];
                if (!spriteShape.TestHit(barrier.HitShape))
                    continue;
                sprite.OnHitBarrier(barrier, spriteContext);
                if (sprite.IsAlive)
                    barrier.OnHitSprite(sprite, spriteContext);
            }
        }

        // Reap any sprite that died during this step.
        for (int i = 0; i < _sprites.Count; i++)
        {
            if (!_sprites[i].IsAlive)
                _pendingRemoveSprites.Add(_sprites[i]);
        }
    }

    private void ApplyPendingChanges()
    {
        if (_pendingRemoveSprites.Count > 0)
        {
            foreach (var s in _pendingRemoveSprites)
            {
                if (_sprites.Remove(s))
                    Detach(s, retired: true);
            }
            _pendingRemoveSprites.Clear();
        }
        if (_pendingAddSprites.Count > 0)
        {
            _sprites.AddRange(_pendingAddSprites);
            _pendingAddSprites.Clear();
        }
        if (_pendingRemoveBarriers.Count > 0)
        {
            foreach (var b in _pendingRemoveBarriers)
                _barriers.Remove(b);
            _pendingRemoveBarriers.Clear();
        }
        if (_pendingAddBarriers.Count > 0)
        {
            _barriers.AddRange(_pendingAddBarriers);
            _pendingAddBarriers.Clear();
        }
    }

    /// <inheritdoc/>
    public override void Draw(Renderer3D renderer)
    {
        for (int i = 0; i < _barriers.Count; i++)
            _barriers[i].Draw(renderer);

        for (int i = 0; i < _sprites.Count; i++)
        {
            var s = _sprites[i];
            if (s.IsAlive)
                s.Draw(renderer);
        }
    }
}
