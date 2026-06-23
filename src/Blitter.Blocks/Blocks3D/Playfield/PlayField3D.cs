namespace Blitter.Blocks3D;

/// <summary>
/// The 3D "world" layer, containing a set of sprites and barriers
/// that interact with each other.
/// </summary>
public class PlayField3D : Layer3D, ISpriteHost3D
{
    private readonly List<Sprite3D> _sprites = new();
    private readonly List<Barrier3D> _barriers = new();

    // While Update is iterating we can't mutate _sprites/_barriers
    // directly. Adds/removes during update push into these pending
    // lists and the changes are applied at the end of the frame.
    private readonly List<Sprite3D> _pendingAddSprites = new();
    private readonly HashSet<Sprite3D> _pendingRemoveSprites = new(ReferenceEqualityComparer.Instance);
    private readonly List<Barrier3D> _pendingAddBarriers = new();
    private readonly List<Barrier3D> _pendingRemoveBarriers = new();
    private bool _updating;

    public PlayField3D()
    {
    }

    public PlayField3D(IEnumerable<Sprite3D> sprites)
    {
        AddSprites(sprites);
    }

    public PlayField3D(IEnumerable<Sprite3D> sprites, IEnumerable<Barrier3D> barriers)
    {
        AddSprites(sprites);
        AddBarriers(barriers);
    }

    /// <summary>
    /// The sprites currently in this playfield.
    /// </summary>
    public IReadOnlyList<Sprite3D> Sprites => _sprites;

    /// <summary>
    /// Static, non-sprite obstacles in this playfield.
    /// </summary>
    public IReadOnlyList<Barrier3D> Barriers => _barriers;

    /// <summary>
    /// Total time since construction of the playfield.
    /// </summary>
    public TimeSpan Elapsed { get; private set; }

    /// <summary>Adds a sprite to the playfield.</summary>
    public void AddSprite(Sprite3D sprite)
    {
        if (ReferenceEquals(sprite.Host, this))
        {
            // Already a member — cancel any pending removal so the
            // sprite stays around past the current frame.
            _pendingRemoveSprites.Remove(sprite);
            return;
        }

        // Evict the sprite from any previous host before adopting it. Use the
        // immediate, non-retiring path for PlayField3D hosts so reparenting
        // doesn't fire OnSpriteRetired; fall back to the host API otherwise.
        if (sprite.Host is PlayField3D previous)
            previous.RemoveImmediate(sprite);
        else
            sprite.Host?.RemoveSprite(sprite);

        // Parenting drives attachment (Entity.OnAttach wires behaviors).
        sprite.Parent = this;
        sprite._spawnedAt = Elapsed;

        if (_updating)
        {
            _pendingAddSprites.Add(sprite);
        }
        else
        {
            _sprites.Add(sprite);
        }
    }

    /// <summary>Adds multiple sprites to the playfield.</summary>
    public void AddSprites(IEnumerable<Sprite3D> sprites)
    {
        foreach (var s in sprites)
            AddSprite(s);
    }

    /// <summary>
    /// Retires a sprite from the playfield. Safe to call during
    /// <see cref="Update"/>: the sprite stops updating and colliding
    /// immediately and the actual removal is deferred to end of frame.
    /// </summary>
    public void RemoveSprite(Sprite3D sprite)
    {
        if (sprite.Host != this)
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

    // A sprite is live while it is a member and not pending removal this
    // frame. Membership/removal is host-owned state; sprites carry no flag.
    private bool IsLive(Sprite3D sprite) => !_pendingRemoveSprites.Contains(sprite);

    /// <summary>
    /// Reports whether <paramref name="child"/> is a sprite or barrier this
    /// playfield contains, is removing this frame, or does not hold.
    /// </summary>
    public override Containment GetContainment(IEntity child)
    {
        if (child is Sprite3D sprite)
        {
            if (!ReferenceEquals(sprite.Parent, this))
                return Containment.NotContained;
            return _pendingRemoveSprites.Contains(sprite) ? Containment.Removing : Containment.Contained;
        }

        if (child is Barrier3D barrier)
        {
            if (_pendingRemoveBarriers.Contains(barrier))
                return Containment.Removing;
            if (_barriers.Contains(barrier) || _pendingAddBarriers.Contains(barrier))
                return Containment.Contained;
        }

        return Containment.NotContained;
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
        if (sprite.Parent == this)
            sprite.Parent = null;
        if (retired)
            OnSpriteRetired(sprite);
    }

    /// <summary>
    /// Called once for each sprite that leaves this playfield via
    /// <see cref="RemoveSprite"/>. Not called when a sprite is
    /// reparented into another playfield. Override to return the sprite
    /// to a pool, recycle resources, etc.
    /// </summary>
    protected virtual void OnSpriteRetired(Sprite3D sprite)
    {
    }

    /// <inheritdoc/>
    public override void Update(in UpdateContext context)
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

    private void RunOneStep(in UpdateContext spriteContext)
    {
        // Animated barriers tick before sprites so this frame's
        // sprite-vs-barrier pass sees the new geometry.
        for (int i = 0; i < _barriers.Count; i++)
            _barriers[i].Update(spriteContext);

        for (int i = 0; i < _sprites.Count; i++)
        {
            var sprite = _sprites[i];
            if (!IsLive(sprite))
                continue;
            sprite.Update(spriteContext);
        }

        // sprite-vs-sprite collision
        for (int i = 0; i < _sprites.Count; i++)
        {
            var a = _sprites[i];
            if (!IsLive(a))
                continue;
            var aShape = a.HitShape;
            if (aShape.BoundingSphere.Radius <= 0f)
                continue;

            for (int j = i + 1; j < _sprites.Count; j++)
            {
                if (!IsLive(a))
                    break;

                var b = _sprites[j];
                if (!IsLive(b))
                    continue;
                var bShape = b.HitShape;
                if (bShape.BoundingSphere.Radius <= 0f)
                    continue;
                if (!aShape.TestHit(bShape))
                    continue;

                HitDispatch3D.SpriteHit(a, b, in spriteContext);
                if (IsLive(a) && IsLive(b))
                    HitDispatch3D.SpriteHit(b, a, in spriteContext);
            }
        }

        // sprite-vs-barrier collision
        for (int i = 0; i < _sprites.Count; i++)
        {
            var sprite = _sprites[i];
            if (!IsLive(sprite))
                continue;
            var spriteShape = sprite.HitShape;
            if (spriteShape.BoundingSphere.IsEmpty)
                continue;

            for (int j = 0; j < _barriers.Count; j++)
            {
                if (!IsLive(sprite))
                    break;
                var barrier = _barriers[j];
                if (!spriteShape.TestHit(barrier.HitShape))
                    continue;
                HitDispatch3D.BarrierHit(sprite, barrier, in spriteContext);
                if (IsLive(sprite))
                    barrier.OnHitSprite(sprite, spriteContext);
            }
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
            if (IsLive(s))
                s.Draw(renderer);
        }
    }
}
