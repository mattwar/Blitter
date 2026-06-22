namespace Blitter.Blocks2D;


/// <summary>
/// Aligns a sprite's <see cref="Sprite2D.Rotation"/> to its
/// <see cref="Sprite2D.Heading"/> each tick, so the image points
/// in the direction of travel.
/// </summary>
public class FaceHeading2D : Behavior
{
    /// <summary>
    /// Degrees added to <see cref="Sprite2D.Heading"/> when assigning
    /// <see cref="Sprite2D.Rotation"/>. Use this when the sprite's
    /// artwork doesn't already point "up" at heading 0.
    /// </summary>
    public float RotationOffset { get; set; }

    private Transform2D _transform = null!;
    private Velocity2D _velocity = null!;

    protected override void OnAttach(IEntity entity)
    {
        _transform = entity.GetOrAddTrait<Transform2D>();
        _velocity = entity.GetOrAddTrait<Velocity2D>();
    }

    public override void Apply(in UpdateContext context)
    {
        _transform.Rotation = _velocity.Heading + RotationOffset;           
    }
}
