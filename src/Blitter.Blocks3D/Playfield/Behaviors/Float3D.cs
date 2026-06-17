namespace Blitter.Blocks3D;
using Bits;
 
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

    private Sprite3D _target = null!;

    protected override void OnAttach(IEntity entity)
    {
        _target = (Sprite3D)entity;
    }

    public override void Apply(in UpdateContext context)
    {
        var time = (float)context.ElapsedSinceLastUpdate.TotalSeconds;
        var newY = _target.Position.Y + (MathF.Sin(time * Frequency) * Amplitude);
        _target.Position = _target.Position with { Y = newY };
    }
}
