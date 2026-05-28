using Blitter.Bits;

namespace Blitter.Blocks2D;

/// <summary>
/// The 2D "world" layer: owns a set of <see cref="Sprite2D"/>s and
/// <see cref="Barrier2D"/>s, drives their per-tick updates, and
/// runs the collision pass that dispatches
/// <see cref="Sprite2D.OnHitSprite"/> and
/// <see cref="Sprite2D.OnHitBarrier"/>.
/// </summary>
public class PlayField2D : Layer2D
{
    // Live membership lists. Read-only views are exposed via
    // Sprites/Barriers; outside callers must not mutate.
    private readonly List<Sprite2D> _sprites = new();
    private readonly List<Barrier2D> _barriers = new();

    // While Update is iterating we can't mutate _sprites/_barriers
    // directly. AddSprite/AddBarrier/RemoveBarrier called during the
    // update push into these pending lists and the changes are
    // applied at the end of the frame.
    private readonly List<Sprite2D> _pendingAddSprites = new();
    private readonly List<Sprite2D> _pendingRemoveSprites = new();
    private readonly List<Barrier2D> _pendingAddBarriers = new();
    private readonly List<Barrier2D> _pendingRemoveBarriers = new();
    private bool _updating;

    public PlayField2D()
    {
    }

    public PlayField2D(IEnumerable<Sprite2D> sprites)
    {
        AdoptSprites(sprites);
    }

    public PlayField2D(IEnumerable<Sprite2D> sprites, IEnumerable<Barrier2D> barriers)
    {
        AdoptSprites(sprites);
        foreach (var b in barriers)
            _barriers.Add(b);
    }

    private void AdoptSprites(IEnumerable<Sprite2D> sprites)
    {
        foreach (var s in sprites)
        {
            s._playField?.RemoveImmediate(s);
            s._playField = this;
            s._spawnedAt = Elapsed;
            _sprites.Add(s);
        }
    }

    /// <summary>
    /// The sprites currently in this playfield.
    /// </summary>
    public IReadOnlyList<Sprite2D> Sprites => _sprites;

    /// <summary>
    /// Static, non-sprite obstacles in this playfield. 
    /// Tested against every sprite's <see cref="Sprite2D.HitCircle"/> each tick.
    /// </summary>
    public IReadOnlyList<Barrier2D> Barriers => _barriers;

    /// <summary>
    /// Total time accumulated from <see cref="UpdateContext2D"/> deltas passed through this playfield's <see cref="Update"/>.
    /// Used as the clock for <see cref="Sprite2D.Age"/>.
    /// </summary>
    public TimeSpan Elapsed { get; private set; }

    /// <summary>
    /// Optional world rectangle larger (or smaller) than the visible viewport. 
    /// When set, sprites and behaviors see this rectangle as <see cref="UpdateContext2D.Bounds"/> instead of the renderer's
    /// viewport — so edge-bounce, spawn placement, etc. operate in world space. 
    /// When <c>null</c> (the default), the playfield passes the incoming context through unchanged.
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
    public void AddSprite(Sprite2D sprite)
    {
        var existing = sprite._playField;
        if (existing == this)
        {
            // Already a member — cancel any pending removal so the
            // sprite stays around past the current frame.
            _pendingRemoveSprites.Remove(sprite);
            return;
        }
        existing?.RemoveImmediate(sprite);
        sprite._playField = this;
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

    /// <summary>
    /// Adds multiple sprites to the playfield.
    /// </summary>
    public void AddSprites(IEnumerable<Sprite2D> sprites)
    {
        foreach (var s in sprites)
            AddSprite(s);
    }

    /// <summary>
    /// Removes a sprite from the playfield. 
    /// Safe to call during <see cref="Update"/>; 
    /// the actual removal is deferred to end of frame.
    /// The normal way to retire a sprite is to set <see cref="Sprite2D.IsAlive"/> to <c>false</c>.
    /// This method is for callers that need to evict a sprite
    /// without killing it (e.g. reparenting to another playfield).
    /// </summary>
    public void RemoveSprite(Sprite2D sprite)
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

    /// <summary>
    /// Adds a barrier to the playfield.
    /// </summary>
    public void AddBarrier(Barrier2D barrier)
    {
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
    public void AddBarriers(IEnumerable<Barrier2D> barriers)
    {
        var sink = _updating ? _pendingAddBarriers : _barriers;
        foreach (var b in barriers)
        {
            sink.Add(b);           
        }
    }

    /// <summary>
    /// Removes a barrier from the playfield.
    /// </summary>
    public void RemoveBarrier(Barrier2D barrier)
    {
        if (_updating)
        {
            _pendingRemoveBarriers.Add(barrier);
        }
        else
        {
            _barriers.Remove(barrier);
        }
    }

    // Removes `sprite` from this playfield immediately, regardless of update state. 
    // Used by the reparenting path on AddSprite where we can't wait until end-of-frame. 
    // User-facing removal goes through `sprite.IsAlive = false` and is reaped via the pending pipeline.
    internal void RemoveImmediate(Sprite2D sprite)
    {
        _pendingAddSprites.Remove(sprite);
        _pendingRemoveSprites.Remove(sprite);
        if (_sprites.Remove(sprite))
            Detach(sprite, retired: false);
    }

    // Clears the sprite's playfield link and, when `retired` is true,
    // notifies the playfield via OnSpriteRetired. Reparenting paths
    // pass retired=false so pool consumers don't try to recycle a
    // sprite that's about to live in another playfield.
    private void Detach(Sprite2D sprite, bool retired)
    {
        if (sprite._playField == this)
            sprite._playField = null;
        if (retired)
            OnSpriteRetired(sprite);
    }

    /// <summary>
    /// Called once for each sprite that leaves this playfield, either because
    /// its <see cref="Sprite2D.IsAlive"/> went to <c>false</c> or because <see cref="RemoveSprite"/> evicted it. 
    /// Not called when a sprite is reparented into another playfield.
    /// Override to return the sprite to a pool, recycle resources, etc.
    /// </summary>
    protected virtual void OnSpriteRetired(Sprite2D sprite)
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
    public override void Update(in UpdateContext2D context)
    {
        Elapsed += context.ElapsedSinceLastUpdate;

        // Substitute world bounds for the viewport when configured so
        // behaviors that consult context.Bounds (BounceInBounds2D,
        // edge-spawning, etc.) operate in world space.
        var frameContext = WorldBounds is Rect wb
            ? context with { Bounds = wb }
            : context;

        // Global substepping: every sprite and barrier gets the same
        // number of substeps with the same per-substep dt, so frame
        // determinism is preserved. Cost is 1 step in the common case.
        var dt = (float)frameContext.ElapsedSinceLastUpdate.TotalSeconds;
        int substeps = ComputeSubstepCount(dt);
        var subContext = substeps > 1
            ? frameContext with { ElapsedSinceLastUpdate = frameContext.ElapsedSinceLastUpdate / substeps }
            : frameContext;

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
    // Uses the sprite's current Speed as the velocity proxy — that's
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
            if (!s.IsAlive || !s.CanBeHit)
                continue;
            var r = s.HitCircle.Radius;
            if (r <= 0f)
                continue;
            if (r < minRadius)
                minRadius = r;
            var step = MathF.Abs(s.Speed) * dt;
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

    private void RunOneStep(in UpdateContext2D spriteContext)
    {
        // Animated barriers (flippers, moving platforms, etc.) tick
        // before sprites so this frame's sprite-vs-barrier pass sees
        // the new geometry.
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
            if (aShape.BoundingCircle.Radius <= 0f)
                continue;

            for (int j = i + 1; j < _sprites.Count; j++)
            {
                if (!a.IsAlive)
                    break;

                var b = _sprites[j];
                if (!b.IsAlive || !b.CanBeHit)
                    continue;
                var bShape = b.HitShape;
                if (bShape.BoundingCircle.Radius <= 0f)
                    continue;
                if (!aShape.TestHit(bShape))
                    continue;

                a.OnHitSprite(b, spriteContext);
                if (a.IsAlive && b.IsAlive)
                    b.OnHitSprite(a, spriteContext);
            }
        }

        // sprite-vs-barrier collision
        if (_barriers.Count > 0)
        {
            for (int s = 0; s < _sprites.Count; s++)
            {
                var sprite = _sprites[s];
                if (!sprite.IsAlive || !sprite.CanBeHit)
                    continue;
                var spriteShape = sprite.HitShape;
                if (spriteShape.BoundingCircle.IsEmpty)
                    continue;

                for (int k = 0; k < _barriers.Count; k++)
                {
                    if (!sprite.IsAlive)
                        break;
                    var barrier = _barriers[k];
                    // Re-read each time: the previous barrier handler
                    // may have moved the sprite.
                    if (!sprite.HitShape.TestHit(barrier.HitShape))
                        continue;

                    // Barrier reacts first so any state change it
                    // makes (re-arming, lowering a drop target,
                    // swapping its Material) is visible to the
                    // sprite's bounce resolution on the same frame.
                    barrier.OnHitSprite(sprite, spriteContext);
                    if (sprite.IsAlive)
                        sprite.OnHitBarrier(barrier, spriteContext);
                }
            }
        }
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
                Detach(s, retired: true);
            }
            _pendingRemoveSprites.Clear();
        }

        // Reap any sprite that died during this frame.
        for (int i = _sprites.Count - 1; i >= 0; i--)
        {
            var s = _sprites[i];
            if (s.IsAlive) continue;
            _sprites.RemoveAt(i);
            Detach(s, retired: true);
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
                    _barriers.RemoveAt(i);
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
            _barriers[i].Draw(renderer);

        for (int i = 0; i < _sprites.Count; i++)
        {
            var sprite = _sprites[i];
            if (sprite.IsAlive)
                sprite.Draw(renderer);
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
