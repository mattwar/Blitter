using System.Collections.Immutable;

namespace Blitter.Blocks;

public abstract class Container2D : Prop2D
{
    private ImmutableList<Prop2D> _props = ImmutableList<Prop2D>.Empty;

    public ImmutableList<Prop2D> Props => _props;

    /// <summary>
    /// Total time accumulated from the <see cref="UpdateContext2D"/>
    /// deltas passed through this container's <see cref="Update"/>.
    /// Used as the clock for child <see cref="Prop2D.Age"/>.
    /// </summary>
    public TimeSpan Elapsed { get; private set; }

    public Container2D(IEnumerable<Prop2D> props)
    {
        _props = props.ToImmutableList();
        // Adopt the initial set: each prop's Container is this. If a
        // prop was already in another container, take ownership.
        foreach (var p in props)
        {
            p.Container?.RemoveImmediate(p);
            p.Container = this;
        }
    }

    public override bool Update(in UpdateContext2D context)
    {
        Elapsed += context.ElapsedSinceLastUpdate;

        var changed = false;
        var props = _props;
        var removeDead = false;

        foreach (var prop in props)
        {
            if (!prop.IsAlive)
            {
                removeDead = true;
                continue;
            }

            if (prop.Update(context))
            {
                changed = true;
            }

            // An update may have marked the prop for removal.
            if (!prop.IsAlive)
                removeDead = true;
        }

        // Pairwise collision pass: for each unique (i<j) pair where
        // both have a HitCircle and they intersect, dispatch
        // OnCollision to both sides. Snapshot is fixed for this pass,
        // so handlers that spawn new props (via Container.Add) defer
        // those to the next tick.
        for (int i = 0; i < props.Count; i++)
        {
            var a = props[i];
            if (!a.IsAlive) continue;
            if (a.HitCircle is not { } ac) continue;

            for (int j = i + 1; j < props.Count; j++)
            {
                if (!a.IsAlive) break; // killed by an earlier pair this row
                var b = props[j];
                if (!b.IsAlive) continue;
                if (b.HitCircle is not { } bc) continue;

                if (!ac.Intersects(bc)) continue;

                a.OnCollision(b, context);
                if (a.IsAlive && b.IsAlive)
                    b.OnCollision(a, context);

                if (!a.IsAlive || !b.IsAlive)
                {
                    removeDead = true;
                    changed = true;
                }
            }
        }

        if (removeDead)
        {
            ImmutableInterlocked.Update(ref _props, list => list.RemoveAll(p =>
            {
                if (p.IsAlive) return false;
                // Clear the container link on the way out so a reaped
                // prop can be re-added elsewhere with a clean slate.
                if (p.Container == this)
                    p.Container = null;
                return true;
            }));
            changed = true;
        }

        return changed;
    }

    public override void Draw(Renderer2D renderer)
    {
        var props = _props;

        foreach (var prop in props)
        {
            // Defensive: a prop may have been marked dead between
            // Update reap and Draw (e.g. by an event handler).
            if (prop.IsAlive)
                prop.Draw(renderer);
        }
    }

    public void Add(Prop2D prop)
    {
        // Enforce single-container invariant: if the prop was in
        // another container, take ownership silently. Skips the work
        // when it's already ours.
        var existing = prop.Container;
        if (existing == this)
            return;
        existing?.RemoveImmediate(prop);

        ImmutableInterlocked.Update(ref _props, (list) => list.Add(prop));
        prop.Container = this;
        prop._spawnedAt = Elapsed;
    }

    // Removes `prop` from this container's list and clears its
    // Container link. Used only by the reparenting path on Add /
    // ctor; user-facing removal goes through IsAlive = false.
    internal void RemoveImmediate(Prop2D prop)
    {
        ImmutableInterlocked.Update(ref _props, list => list.Remove(prop));
        if (prop.Container == this)
            prop.Container = null;
    }
}
