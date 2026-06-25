namespace Blitter.Blocks2D;

/// <summary>
/// Raised after a <see cref="BounceInBounds2D"/> reflection.
/// </summary>
/// <param name="Source">The behavior instance that raised the event.</param>
/// <param name="Self">The entity that bounced off the bounds.</param>
public readonly record struct BoundsBounced2DEventArgs(BounceInBounds2D Source, IEntity Self);

/// <summary>
/// Reflects a sprite's velocity when its center crosses the edge of the
/// update context bounds, so the sprite stays inside the playfield.
/// </summary>
public class BounceInBounds2D : Behavior, IUpdatable
{
    /// <summary>Optional handler invoked after the velocity is reflected.</summary>
    public IEventHandler<BoundsBounced2DEventArgs>? Bounced { get; set; }

    private IEntity _entity = null!;
    private Bounds2D? _bounds;
    private Velocity2D _velocity = null!;
    private Transform2D _transform = null!;

    protected override void OnAttach(IEntity entity)
    {
        _entity = entity;
        _velocity = entity.GetOrAddTrait<Velocity2D>();
        _transform = entity.GetOrAddTrait<Transform2D>();
    }

    public void Update(in EntityUpdateContext context)
    {
        // Bounds live on an ancestor (the playfield), which isn't reachable
        // when this behavior is attached (the sprite may not be parented
        // yet). Resolve it opportunistically once the entity is parented.
        _bounds ??= _entity.TryFindTrait<Bounds2D>(out var found) ? found : null;
        if (_bounds is null)
            return;

        var bounds = _bounds.Rect;
        var v = Sprite2D.GetVelocity(_velocity.Speed, _velocity.Heading);
        var bounced = false;

        if (_transform.Position.X < bounds.X)
        {
            v.X = MathF.Abs(v.X);
            bounced = true;
        }
        else if (_transform.Position.X > bounds.X + bounds.Width)
        {
            v.X = -MathF.Abs(v.X);
            bounced = true;
        }

        if (_transform.Position.Y < bounds.Y)
        {
            v.Y = MathF.Abs(v.Y);
            bounced = true;
        }
        else if (_transform.Position.Y > bounds.Y + bounds.Height)
        {
            v.Y = -MathF.Abs(v.Y);
            bounced = true;
        }

        if (!bounced)
            return;

        (_velocity.Speed, _velocity.Heading) = Sprite2D.GetSpeedAndHeading(v);
    
        var args = new BoundsBounced2DEventArgs(this, _entity);
        Bounced?.OnEvent(in args);
    }
}
