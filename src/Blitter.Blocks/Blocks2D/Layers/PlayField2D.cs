namespace Blitter.Blocks2D;

/// <summary>
/// A high-volume 2D world container.
/// </summary>
public class PlayField2D : DeferredMutationContainer
{
    private readonly Bounds2D _bounds;

    public PlayField2D()
    {
        _bounds = GetOrAddTrait<Bounds2D>();
        Behaviors = [ new CollisionSpace2D { MaxSubsteps = 8 } ];
    }

    /// <summary>
    /// Optional world rectangle larger (or smaller) than the visible viewport.
    /// Backed by this entity's <see cref="Bounds2D"/> trait so behaviors can
    /// resolve it by walking up the entity tree.
    /// </summary>
    public Rect? WorldBounds
    {
        get => _bounds.Rect;
        set => _bounds.Rect = value;
    }
}
