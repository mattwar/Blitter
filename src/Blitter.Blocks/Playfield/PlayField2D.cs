using System.Collections.Immutable;
using Blitter.Bits;

namespace Blitter.Blocks;

/// <summary>
/// The 2D "world" layer: owns a set of <see cref="Sprite2D"/>s and
/// <see cref="Barrier2D"/>s, drives their per-tick updates, and
/// runs the collision pass that dispatches
/// <see cref="Sprite2D.OnHitSprite"/> and
/// <see cref="Sprite2D.OnHitBarrier"/>.
/// </summary>
public class PlayField2D : Layer2D
{
    private ImmutableList<Sprite2D> _sprites = ImmutableList<Sprite2D>.Empty;
    private ImmutableList<Barrier2D> _barriers = ImmutableList<Barrier2D>.Empty;

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
        _barriers = barriers.ToImmutableList();
    }

    private void AdoptSprites(IEnumerable<Sprite2D> sprites)
    {
        var list = sprites.ToImmutableList();
        foreach (var s in list)
        {
            s._playField?.RemoveImmediate(s);
            s._playField = this;
            s._spawnedAt = Elapsed;
        }
        _sprites = list;
    }

    /// <summary>The sprites currently in this playfield.</summary>
    public ImmutableList<Sprite2D> Sprites => _sprites;

    /// <summary>
    /// Static, non-sprite obstacles in this playfield. 
    /// Tested against every sprite's <see cref="Sprite2D.HitCircle"/> each tick.
    /// </summary>
    public ImmutableList<Barrier2D> Barriers => _barriers;

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

    public void AddSprite(Sprite2D sprite)
    {
        var existing = sprite._playField;
        if (existing == this)
            return;
        existing?.RemoveImmediate(sprite);

        ImmutableInterlocked.Update(ref _sprites, (list, s) => list.Add(s), sprite);
        sprite._playField = this;
        sprite._spawnedAt = Elapsed;
    }

    public void AddBarrier(Barrier2D barrier)
        => ImmutableInterlocked.Update(ref _barriers, (list, b) => list.Add(b), barrier);

    public void AddBarriers(IEnumerable<Barrier2D> barriers)
        => ImmutableInterlocked.Update(ref _barriers, (list, bs) => list.AddRange(bs), barriers);

    public void RemoveBarrier(Barrier2D barrier)
        => ImmutableInterlocked.Update(ref _barriers, (list, b) => list.Remove(b), barrier);

    // Removes `sprite` from this playfield's list and clears its
    // PlayField link. Used by the reparenting path on AddSprite.
    // User-facing removal goes through `sprite.IsAlive = false`.
    internal void RemoveImmediate(Sprite2D sprite)
    {
        ImmutableInterlocked.Update(ref _sprites, list => list.Remove(sprite));
        if (sprite._playField == this)
            sprite._playField = null;
    }

    public override void Update(in UpdateContext2D context)
    {
        Elapsed += context.ElapsedSinceLastUpdate;

        // Substitute world bounds for the viewport when configured so
        // behaviors that consult context.Bounds (BounceInBounds2D,
        // edge-spawning, etc.) operate in world space.
        var spriteContext = WorldBounds is Rect wb
            ? context with { Bounds = wb }
            : context;

        var sprites = _sprites;
        var removeDead = false;

        foreach (var sprite in sprites)
        {
            if (!sprite.IsAlive)
            {
                removeDead = true;
                continue;
            }

            sprite.Update(spriteContext);

            if (!sprite.IsAlive)
                removeDead = true;
        }

        // sprite-vs-sprite collision
        for (int i = 0; i < sprites.Count; i++)
        {
            var a = sprites[i];
            if (!a.IsAlive || !a.CanBeHit)
                continue;
            var aShape = a.HitShape;
            if (aShape.BroadCircle.Radius <= 0f)
                continue;

            for (int j = i + 1; j < sprites.Count; j++)
            {
                if (!a.IsAlive)
                    break;

                var b = sprites[j];
                if (!b.IsAlive || !b.CanBeHit)
                    continue;
                var bShape = b.HitShape;
                if (bShape.BroadCircle.Radius <= 0f)
                    continue;
                if (!HitShape.Intersects(aShape, bShape))
                    continue;

                a.OnHitSprite(b, spriteContext);
                if (a.IsAlive && b.IsAlive)
                    b.OnHitSprite(a, spriteContext);
            }
        }

        // sprite-vs-barrier collision
        var barriers = _barriers;
        if (barriers.Count > 0)
        {
            foreach (var sprite in sprites)
            {
                if (!sprite.IsAlive || !sprite.CanBeHit)
                    continue;
                var sc = sprite.HitCircle;
                if (sc.Radius <= 0f)
                    continue;

                foreach (var barrier in barriers)
                {
                    if (!sprite.IsAlive)
                        break;
                    // Re-read each time: the previous barrier handler
                    // may have moved the sprite.
                    if (!barrier.Intersects(sprite.HitCircle))
                        continue;

                    sprite.OnHitBarrier(barrier, spriteContext);
                }
            }
        }

        // re-scan after collision/barrier handlers may have killed sprites
        if (!removeDead)
        {
            foreach (var s in _sprites)
            {
                if (!s.IsAlive)
                {
                    removeDead = true;
                    break;
                }
            }
        }

        if (removeDead)
        {
            ImmutableInterlocked.Update(ref _sprites, list => list.RemoveAll(s =>
            {
                if (s.IsAlive) return false;
                if (s._playField == this)
                    s._playField = null;
                return true;
            }));
        }
    }

    protected override void DrawContent(Renderer2D renderer)
    {
        DrawBackground(renderer);

        var sprites = _sprites;
        foreach (var sprite in sprites)
        {
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
