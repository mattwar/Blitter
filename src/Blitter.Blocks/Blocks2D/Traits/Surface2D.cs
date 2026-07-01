namespace Blitter.Blocks2D;

/// <summary>
/// Physical surface characteristics (restitution, friction, kick) of an entity. 
/// Pure data, read by bounce behaviors such as <see cref="SurfaceBounce2D"/> to modulate a collision response. 
/// </summary>
public sealed class Surface2D : Trait
{
    /// <summary>
    /// The surface material. Defaults to <see cref="PhysicsMaterial.Ideal"/>.
    /// </summary>
    public PhysicsMaterial Material { get; set; } = PhysicsMaterial.Ideal;
}
