using System.Collections.Immutable;

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
    /// Static, non-sprite obstacles in this playfield. Tested against
    /// every sprite's <see cref="Sprite2D.HitCircle"/> each tick.
    /// </summary>
    public ImmutableList<Barrier2D> Barriers => _barriers;

    /// <summary>
    /// Total time accumulated from <see cref="UpdateContext2D"/>
    /// deltas passed through this playfield's <see cref="Update"/>.
    /// Used as the clock for <see cref="Sprite2D.Age"/>.
    /// </summary>
    public TimeSpan Elapsed { get; private set; }

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

        var sprites = _sprites;
        var removeDead = false;

        foreach (var sprite in sprites)
        {
            if (!sprite.IsAlive)
            {
                removeDead = true;
                continue;
            }

            sprite.Update(context);

            if (!sprite.IsAlive)
                removeDead = true;
        }

        // sprite-vs-sprite collision
        for (int i = 0; i < sprites.Count; i++)
        {
            var a = sprites[i];
            if (!a.IsAlive)
                continue;
            var ac = a.HitCircle;
            if (ac.Radius <= 0f)
                continue;

            for (int j = i + 1; j < sprites.Count; j++)
            {
                if (!a.IsAlive)
                    break;

                var b = sprites[j];
                if (!b.IsAlive)
                    continue;
                var bc = b.HitCircle;
                if (bc.Radius <= 0f)
                    continue;
                if (!ac.Intersects(bc))
                    continue;

                a.OnHitSprite(b, context);
                if (a.IsAlive && b.IsAlive)
                    b.OnHitSprite(a, context);
            }
        }

        // sprite-vs-barrier collision
        var barriers = _barriers;
        if (barriers.Count > 0)
        {
            foreach (var sprite in sprites)
            {
                if (!sprite.IsAlive)
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

                    sprite.OnHitBarrier(barrier, context);
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

    public override void Draw(Renderer2D renderer)
    {
        DrawBackground(renderer);

        var sprites = _sprites;
        foreach (var sprite in sprites)
        {
            if (sprite.IsAlive)
                sprite.Draw(renderer);
        }

        DrawForeground(renderer);
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
