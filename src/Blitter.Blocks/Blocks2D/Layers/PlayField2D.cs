namespace Blitter.Blocks2D;

/// <summary>
/// A high-volume 2D world container.
/// </summary>
public class PlayField2D : DeferredMutationContainer
{
    public PlayField2D()
    {
        GetOrAddTrait<Bounds2D>();
        Behaviors = [ new CollisionSpace2D { MaxSubsteps = 8 } ];
    }

    /// <summary>
    /// Optional world rectangle larger (or smaller) than the visible viewport.
    /// Backed by this entity's <see cref="Bounds2D"/> trait so behaviors can
    /// resolve it by walking up the entity tree. Resolved live so it reflects
    /// the current trait even when one is supplied via a <c>Traits</c> initializer.
    /// </summary>
    public Rect? WorldBounds
    {
        get => GetOrAddTrait<Bounds2D>().Rect;
        set => GetOrAddTrait<Bounds2D>().Rect = value;
    }
}
