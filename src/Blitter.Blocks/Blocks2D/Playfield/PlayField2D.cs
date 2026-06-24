using System.Diagnostics.CodeAnalysis;

namespace Blitter.Blocks2D;

/// <summary>
/// The 2D "world" layer: owns sprite-role and barrier-role entities,
/// drives their per-tick updates, and
/// runs the collision pass that dispatches hits to each sprite's
/// <see cref="IHittable2D"/> behaviors.
/// </summary>
public class PlayField2D : Layer2D, IContainerEntity
{
    // Live membership lists. Read-only views are exposed via
    // Sprites/Barriers; outside callers must not mutate.
    private readonly List<IEntity> _sprites = new();
    private readonly List<IEntity> _barriers = new();

    // While Update is iterating we can't mutate _sprites/_barriers
    // directly. AddSprite/AddBarrier/RemoveBarrier called during the
    // update push into these pending lists and the changes are
    // applied at the end of the frame.
    private readonly List<IEntity> _pendingAddSprites = new();
    private readonly HashSet<IEntity> _pendingRemoveSprites = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<IEntity> _pendingRetireSprites = new(ReferenceEqualityComparer.Instance);
    private readonly List<IEntity> _pendingAddBarriers = new();
    private readonly HashSet<IEntity> _pendingRemoveBarriers = new(ReferenceEqualityComparer.Instance);
    private bool _updating;

    // Spawn timestamp (this playfield's Elapsed clock) per member, used to
    // answer GetAge. The playfield owns this; sprites carry no age field.
    private readonly Dictionary<IEntity, TimeSpan> _spawnedAt = new(ReferenceEqualityComparer.Instance);

    // World-space bounds, refreshed each frame from WorldBounds or the
    // viewport. Resolved by sprite behaviors via TryFindTrait.
    private readonly Bounds2D _bounds = new();

    // Detects and dispatches collisions; entity-agnostic, shapes sourced
    // from each entity's IColliderShape2D behavior.
    private readonly Collider2D _collider;

    public PlayField2D()
    {
        AddTrait(_bounds);
        _collider = new Collider2D(IsLive);
    }

    public PlayField2D(IEnumerable<IEntity> sprites)
        : this()
    {
        AdoptSprites(sprites);
    }

    public PlayField2D(IEnumerable<IEntity> sprites, IEnumerable<IEntity> barriers)
        : this()
    {
        AdoptSprites(sprites);
        AdoptBarriers(barriers);
    }

    private static void SetParent(IEntity child, IContainerEntity? parent)
    {
        if (child is Entity entity)
        {
            entity.Parent = parent;
            return;
        }

        throw new InvalidOperationException($"PlayField2D can only contain {nameof(Entity)} instances.");
    }

    private void AdoptSprites(IEnumerable<IEntity> sprites)
    {
        foreach (var s in sprites)
        {
            (s.Parent as PlayField2D)?.RemoveImmediate(s);
            RemoveBarrierMembership(s, clearParent: false);
            SetParent(s, this);
            _spawnedAt[s] = Elapsed;
            _sprites.Add(s);
        }
    }

    private void AdoptBarriers(IEnumerable<IEntity> barriers)
    {
        foreach (var b in barriers)
        {
            (b.Parent as PlayField2D)?.RemoveImmediate(b);
            RemoveSpriteMembership(b, retired: false, clearParent: false);
            SetParent(b, this);
            _barriers.Add(b);
        }
    }

    /// <summary>
    /// The sprites currently in this playfield. The <c>init</c> accessor adopts
    /// an initial set at construction (object-initializer or constructor), taking
    /// ownership of each sprite just like <see cref="AddSprite"/>.
    /// </summary>
    public IReadOnlyList<IEntity> Sprites
    {
        get => _sprites;
        init => AdoptSprites(value);
    }

    /// <summary>
    /// Tries to resolve the single sprite assignable to <typeparamref name="T"/>
    /// in this playfield. Returns <c>false</c> if none. Throws if more than one
    /// matches.
    /// </summary>
    public bool TryGetSprite<T>([NotNullWhen(true)] out T? sprite) where T : class, IEntity
    {
        T? match = null;
        foreach (var candidate in _sprites)
        {
            if (candidate is not T typed)
                continue;
            if (match is not null)
                throw new InvalidOperationException($"More than one sprite is a {typeof(T).Name}.");
            match = typed;
        }
        sprite = match;
        return match is not null;
    }

    /// <summary>
    /// Resolves the single sprite assignable to <typeparamref name="T"/> in
    /// this playfield. Throws if none exists or more than one matches.
    /// </summary>
    public T GetSprite<T>() where T : class, IEntity =>
        TryGetSprite<T>(out var sprite) ? sprite : throw new InvalidOperationException($"No sprite of type {typeof(T).Name}.");

    /// <summary>
    /// Static, non-sprite obstacle-role entities in this playfield.
    /// Tested against every sprite-role entity's hit shape each tick.
    /// The <c>init</c> accessor adds an initial set at construction
    /// (object-initializer or constructor).
    /// </summary>
    public IReadOnlyList<IEntity> Barriers
    {
        get => _barriers;
        init => AdoptBarriers(value);
    }

    /// <summary>
    /// Total time accumulated from <see cref="UpdateContext"/> deltas passed through this playfield's <see cref="Update"/>.
    /// Used as the clock for <see cref="Sprite2D.Age"/>.
    /// </summary>
    public TimeSpan Elapsed { get; private set; }

    /// <summary>
    /// Optional world rectangle larger (or smaller) than the visible viewport. 
    /// When set, sprites and behaviors resolve this rectangle as their
    /// <see cref="Bounds2D"/> trait instead of the renderer's viewport — so
    /// edge-bounce, spawn placement, etc. operate in world space. 
    /// When <c>null</c> (the default), the playfield publishes the renderer's
    /// viewport as the active bounds.
    /// </summary>
    public Rect? WorldBounds { get; set; }

    /// <summary>
    /// When true and <see cref="WorldBounds"/> is set, the playfield draws the world boundary.
    /// </summary>
    public bool ShowWorldBounds { get; set; }

    /// <summary>
    /// Color used by <see cref="ShowWorldBounds"/> for the boundary outline.
    /// </summary>
    public Color WorldBoundsColor { get; set; } = new Color(0, 200, 255, 255);

    /// <summary>
    /// Adds a sprite to the playfield.
    /// </summary>
    public void AddSprite(IEntity sprite)
    {
        var existing = sprite.Parent as PlayField2D;
        if (existing is not null && existing != this)
        {
            existing.RemoveImmediate(sprite);
        }
        else if (existing == this)
        {
            if (IsBarrierRoleMember(sprite) || _pendingRemoveBarriers.Contains(sprite))
                RemoveBarrier(sprite);
            if (IsSpriteRoleMember(sprite))
            {
                // Already a sprite-role member — cancel any pending
                // removal so it stays around past the current frame.
                _pendingRemoveSprites.Remove(sprite);
                return;
            }
        }
        if (sprite.Parent != this)
            SetParent(sprite, this);
        _spawnedAt[sprite] = Elapsed;
        if (_updating)
        {
            _pendingAddSprites.Add(sprite);           
        }
        else
        {
            _sprites.Add(sprite);
        }
    }

    /// <summary>
    /// Adds multiple sprites to the playfield.
    /// </summary>
    public void AddSprites(IEnumerable<IEntity> sprites)
    {
        foreach (var s in sprites)
            AddSprite(s);
    }

    /// <summary>
    /// Retires a sprite from the playfield. Safe to call during
    /// <see cref="Update"/>: the sprite stops updating and colliding
    /// immediately and the actual removal is deferred to end of frame.
    /// </summary>
    public void RemoveSprite(IEntity sprite)
    {
        RequestRemoveSprite(sprite, retired: true);
    }

    private void RequestRemoveSprite(IEntity sprite, bool retired)
    {
        if (sprite.Parent != this && !IsSpriteRoleMember(sprite))
            return;
        if (_updating)
        {
            var removedPendingAdd = _pendingAddSprites.Remove(sprite);
            if (_sprites.Contains(sprite))
            {
                _pendingRemoveSprites.Add(sprite);
                if (retired)
                    _pendingRetireSprites.Add(sprite);
                else
                    _pendingRetireSprites.Remove(sprite);
            }
            else if (removedPendingAdd)
            {
                _spawnedAt.Remove(sprite);
                ClearParentIfUncontained(sprite);
            }
        }
        else if (_sprites.Remove(sprite))
        {
            Detach(sprite, retired: true);
        }
    }

    private bool RemoveSpriteMembership(IEntity sprite, bool retired, bool clearParent)
    {
        var removed = _pendingAddSprites.Remove(sprite);
        _pendingRemoveSprites.Remove(sprite);
        _pendingRetireSprites.Remove(sprite);
        if (_sprites.Remove(sprite))
            removed = true;

        if (!removed)
            return false;

        _spawnedAt.Remove(sprite);
        if (clearParent)
            ClearParentIfUncontained(sprite);
        if (retired)
            OnSpriteRetired(sprite);
        return true;
    }

    // A sprite is live while it is a member and not pending removal this
    // frame. Membership/removal is host-owned state; sprites carry no flag.
    private bool IsLive(IEntity entity) =>
        !_pendingRemoveSprites.Contains(entity) && !_pendingRemoveBarriers.Contains(entity);

    private bool IsSpriteRoleMember(IEntity entity) =>
        _sprites.Contains(entity) || _pendingAddSprites.Contains(entity);

    private bool IsBarrierRoleMember(IEntity entity) =>
        _barriers.Contains(entity) || _pendingAddBarriers.Contains(entity);

    private void ClearParentIfUncontained(IEntity child)
    {
        if (child.Parent == this && !IsSpriteRoleMember(child) && !IsBarrierRoleMember(child))
            SetParent(child, null);
    }

    /// <inheritdoc/>
    public TimeSpan GetAge(IEntity child) =>
        _spawnedAt.TryGetValue(child, out var t) ? Elapsed - t : TimeSpan.Zero;

    /// <summary>
    /// Removes <paramref name="child"/> from this playfield, dispatching to the
    /// correct pool by runtime type so callers need not know the sprite/barrier
    /// split. No-op for anything this playfield does not hold.
    /// </summary>
    public void RemoveEntity(IEntity child)
    {
        RemoveSprite(child);
        RemoveBarrier(child);
    }

    /// <summary>
    /// Reports whether <paramref name="child"/> is a sprite or barrier this
    /// playfield contains, is removing this frame, or does not hold.
    /// </summary>
    public override Containment GetContainment(IEntity child)
    {
        var isSpriteRoleMember = IsSpriteRoleMember(child);
        var isBarrierRoleMember = IsBarrierRoleMember(child);
        if (isSpriteRoleMember || isBarrierRoleMember)
        {
            if (!ReferenceEquals(child.Parent, this))
                return Containment.NotContained;
            return Containment.Contained;
        }

        if (_pendingRemoveSprites.Contains(child))
            return Containment.Removing;

        if (_pendingRemoveBarriers.Contains(child))
            return Containment.Removing;

        return Containment.NotContained;
    }

    /// <summary>
    /// Adds a barrier to the playfield.
    /// </summary>
    public void AddBarrier(IEntity barrier)
    {
        var existing = barrier.Parent as PlayField2D;
        if (existing is not null && existing != this)
        {
            existing.RemoveImmediate(barrier);
        }
        else if (existing == this)
        {
            if (IsSpriteRoleMember(barrier) || _pendingRemoveSprites.Contains(barrier))
                RequestRemoveSprite(barrier, retired: false);
            if (IsBarrierRoleMember(barrier))
            {
                _pendingRemoveBarriers.Remove(barrier);
                return;
            }
        }

        if (barrier.Parent != this)
            SetParent(barrier, this);
        if (_updating)
        {
            _pendingAddBarriers.Add(barrier);           
        }
        else
        {
            _barriers.Add(barrier);
        }
    }

    /// <summary>
    /// Adds multiple barriers to the playfield.
    /// </summary>
    public void AddBarriers(IEnumerable<IEntity> barriers)
    {
        foreach (var b in barriers)
            AddBarrier(b);
    }

    /// <summary>
    /// Removes a barrier from the playfield.
    /// </summary>
    public void RemoveBarrier(IEntity barrier)
    {
        if (barrier.Parent != this && !IsBarrierRoleMember(barrier))
            return;
        if (_updating)
        {
            var removedPendingAdd = _pendingAddBarriers.Remove(barrier);
            if (_barriers.Contains(barrier))
                _pendingRemoveBarriers.Add(barrier);
            else if (removedPendingAdd)
                ClearParentIfUncontained(barrier);
        }
        else if (_barriers.Remove(barrier))
        {
            ClearParentIfUncontained(barrier);
        }
    }

    private bool RemoveBarrierMembership(IEntity barrier, bool clearParent)
    {
        var removed = _pendingAddBarriers.Remove(barrier);
        _pendingRemoveBarriers.Remove(barrier);
        if (_barriers.Remove(barrier))
            removed = true;

        if (!removed)
            return false;

        if (clearParent)
            ClearParentIfUncontained(barrier);
        return true;
    }

    // Removes `child` from this playfield immediately, regardless of update state. 
    // Used by reparenting paths where we can't wait until end-of-frame.
    // User-facing removal goes through RemoveSprite/RemoveBarrier and is reaped via the pending pipeline.
    internal void RemoveImmediate(IEntity child)
    {
        var removed = RemoveSpriteMembership(child, retired: false, clearParent: false);
        removed = RemoveBarrierMembership(child, clearParent: false) || removed;
        if (removed && child.Parent == this)
            SetParent(child, null);
    }

    // Clears the sprite's playfield link and, when `retired` is true,
    // notifies the playfield via OnSpriteRetired. Reparenting paths
    // pass retired=false so pool consumers don't try to recycle a
    // sprite that's about to live in another playfield.
    private void Detach(IEntity sprite, bool retired)
    {
        _spawnedAt.Remove(sprite);
        ClearParentIfUncontained(sprite);
        if (retired)
            OnSpriteRetired(sprite);
    }

    /// <summary>
    /// Called once for each sprite that leaves this playfield via
    /// <see cref="RemoveSprite"/>. Not called when a sprite is reparented
    /// into another playfield.
    /// Override to return the sprite to a pool, recycle resources, etc.
    /// </summary>
    protected virtual void OnSpriteRetired(IEntity sprite)
    {
    }

    /// <summary>
    /// Maximum number of physics substeps per frame. When the fastest
    /// sprite would move more than half its hit radius in one frame,
    /// the playfield runs the per-frame update loop multiple times
    /// with proportionally smaller deltas so a fast circle can't
    /// jump over a zero-width line barrier without some substep
    /// catching the overlap. 1 disables substepping.
    /// </summary>
    public int MaxSubsteps { get; set; } = 8;

    /// <inheritdoc/>
    public override void Update(in UpdateContext context)
    {
        Elapsed += context.ElapsedSinceLastUpdate;

        // Publish the active world bounds as a trait so sprite behaviors can
        // resolve them by walking up to this playfield. Prefer the explicit
        // WorldBounds; otherwise fall back to the renderer's viewport while
        // the scene is running (leaving the last value when neither applies).
        _bounds.Rect = WorldBounds
            ?? (this.Parent as Scene2D)?.RendererOrNull?.LogicalBounds
            ?? _bounds.Rect;

        // Global substepping: every sprite and barrier gets the same
        // number of substeps with the same per-substep dt, so frame
        // determinism is preserved. Cost is 1 step in the common case.
        var dt = (float)context.ElapsedSinceLastUpdate.TotalSeconds;
        int substeps = ComputeSubstepCount(dt);
        var subContext = substeps > 1
            ? context with { ElapsedSinceLastUpdate = context.ElapsedSinceLastUpdate / substeps }
            : context;

        _updating = true;
        try
        {
            for (int s = 0; s < substeps; s++)
            {
                RunOneStep(subContext);               
            }
        }
        finally
        {
            _updating = false;
        }

        ApplyPendingChanges();
    }

    // Estimate the substep count needed to keep the fastest sprite's
    // per-substep displacement under half the smallest hit radius.
    // Uses the sprite's Velocity2D.Speed as the velocity proxy — that's
    // the value Motion2D will integrate during this frame.
    private int ComputeSubstepCount(float dt)
    {
        if (dt <= 0f || MaxSubsteps <= 1)
            return 1;

        float maxStep = 0f;
        float minRadius = float.PositiveInfinity;
        for (int i = 0; i < _sprites.Count; i++)
        {
            var s = _sprites[i];
            if (!IsLive(s))
                continue;
            if (!Collider2D.TryGetHitShape(s, out var posed))
                continue;
            var r = posed.BoundingCircle.Radius;
            if (r <= 0f)
                continue;
            if (r < minRadius)
                minRadius = r;
            if (!s.TryGetTrait<Velocity2D>(out var velocity))
                continue;
            var step = MathF.Abs(velocity.Speed) * dt;
            if (step > maxStep)
                maxStep = step;
        }

        if (!float.IsFinite(minRadius) || minRadius <= 0f)
            return 1;
        var budget = 0.5f * minRadius;
        if (maxStep <= budget)
            return 1;
        int n = (int)MathF.Ceiling(maxStep / budget);
        return Math.Clamp(n, 1, MaxSubsteps);
    }

    private void RunOneStep(in UpdateContext spriteContext)
    {
        // Animated barriers (flippers, moving platforms, etc.) tick
        // before sprites so this frame's sprite-vs-barrier pass sees
        // the new geometry.
        for (int i = 0; i < _barriers.Count; i++)
        {
            var barrier = _barriers[i];
            if (IsLive(barrier))
                barrier.Update(spriteContext);
        }

        for (int i = 0; i < _sprites.Count; i++)
        {
            var sprite = _sprites[i];
            if (!IsLive(sprite))
                continue;
            sprite.Update(spriteContext);
        }

        _collider.Collide(_sprites, _barriers);
    }

    // Folds dead sprites, pending removes, and pending adds into the
    // live lists. Called once at the end of Update so the update /
    // collision passes see a stable snapshot.
    private void ApplyPendingChanges()
    {
        if (_pendingRemoveSprites.Count > 0)
        {
            for (int i = _sprites.Count - 1; i >= 0; i--)
            {
                var s = _sprites[i];
                if (!_pendingRemoveSprites.Contains(s)) continue;
                _sprites.RemoveAt(i);
                Detach(s, retired: _pendingRetireSprites.Contains(s));
            }
            _pendingRemoveSprites.Clear();
            _pendingRetireSprites.Clear();
        }

        if (_pendingAddSprites.Count > 0)
        {
            _sprites.AddRange(_pendingAddSprites);
            _pendingAddSprites.Clear();
        }

        if (_pendingRemoveBarriers.Count > 0)
        {
            for (int i = _barriers.Count - 1; i >= 0; i--)
            {
                if (_pendingRemoveBarriers.Contains(_barriers[i]))
                {
                    if (_barriers[i].Parent == this)
                        SetParent(_barriers[i], null);
                    _barriers.RemoveAt(i);
                }
            }
            _pendingRemoveBarriers.Clear();
        }
        if (_pendingAddBarriers.Count > 0)
        {
            _barriers.AddRange(_pendingAddBarriers);
            _pendingAddBarriers.Clear();
        }
    }

    protected override void DrawContent(Renderer2D renderer)
    {
        DrawBackground(renderer);

        // Barriers draw behind sprites so the ball, paddle, etc. sit
        // on top of bumpers, flippers, and other props.
        for (int i = 0; i < _barriers.Count; i++)
        {
            var barrier = _barriers[i];
            if (IsLive(barrier) && barrier is IDrawable2D drawable)
                drawable.Draw(renderer);
        }

        for (int i = 0; i < _sprites.Count; i++)
        {
            var sprite = _sprites[i];
            if (IsLive(sprite) && sprite is IDrawable2D drawable)
                drawable.Draw(renderer);
        }

        if (ShowWorldBounds && WorldBounds is not null)
            DrawWorldBoundsOutline(renderer);

        DrawForeground(renderer);
    }

    /// <summary>
    /// Draws the <see cref="WorldBounds"/> overlay. Override to customize
    /// the style (thicker lines, dashed, animated, etc.). Only called
    /// when <see cref="WorldBounds"/> is non-null.
    /// </summary>
    protected virtual void DrawWorldBoundsOutline(Renderer2D renderer)
    {
        if (WorldBounds is not Rect wb)
            return;
        // Push/pop so the boundary overlay doesn't leak draw color
        // into the user's DrawForeground hook.
        using var _ = renderer.PushState();
        renderer.DrawColor = WorldBoundsColor;
        // Inset the right and bottom by one screen pixel (in world units)
        // so they're not rasterized at the just-outside column/row when the
        // camera is clamped against the world edge.
        var inset = 1f / (renderer.Camera?.Zoom ?? 1f);
        var x0 = wb.X;
        var y0 = wb.Y;
        var x1 = wb.X + wb.Width - inset;
        var y1 = wb.Y + wb.Height - inset;
        renderer.DrawLine(x0, y0, x1, y0);
        renderer.DrawLine(x1, y0, x1, y1);
        renderer.DrawLine(x1, y1, x0, y1);
        renderer.DrawLine(x0, y1, x0, y0);
    }

    /// <summary>Hook to draw before the sprite pass.</summary>
    protected virtual void DrawBackground(Renderer2D renderer)
    {
    }

    /// <summary>Hook to draw after the sprite pass.</summary>
    protected virtual void DrawForeground(Renderer2D renderer)
    {
    }
}
