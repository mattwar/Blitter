namespace Blitter.Blocks2D;

/// <summary>
/// Aligns a sprite's <see cref="Sprite2D.Rotation"/> to its
/// <see cref="Sprite2D.Heading"/> each tick, so the image points
/// in the direction of travel.
/// </summary>
public class FaceHeading2D : SpriteBehavior2D
{
    /// <summary>
    /// Degrees added to <see cref="Sprite2D.Heading"/> when assigning
    /// <see cref="Sprite2D.Rotation"/>. Use this when the sprite's
    /// artwork doesn't already point "up" at heading 0.
    /// </summary>
    public float RotationOffset { get; set; }

    public override void Apply(Sprite2D target, in UpdateContext2D context)
    {
        target.Rotation = target.Heading + RotationOffset;
    }
}
