using System.Numerics;
 
namespace Blitter.Blocks3D;

 
/// <summary>
/// Makes the sprite bob up and down over time, like a floating coin or 
/// hovering item. This is a simple way to add "life" to objects without 
/// complex physics.
/// </summary>
public sealed class Float3D : SpriteBehavior3D
{
    /// <summary>
    /// The height of the oscillation in world units.
    /// </summary>
    public float Amplitude { get; set; } = 1f;
    
    /// <summary>
    /// How fast the sprite bobs (in radians per second, though used as a 
    /// frequency multiplier). A value of 1.0 is a standard slow bob.
    /// </summary>
    public float Frequency { get; set; } = 1f;

    public override void Apply(Sprite3D target, in UpdateContext3D context)
    {
        var time = (float)context.ElapsedSinceLastUpdate.TotalSeconds;
        var newY = target.Position.Y + (MathF.Sin(time * Frequency) * Amplitude);
        target.Position = target.Position with { Y = newY };
    }
}
