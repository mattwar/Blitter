namespace Blitter.Blocks2D;

/// <summary>
/// The physical properties of a barrier's material,
/// used to calculate bouncing and deflection when sprites collide with it.
/// </summary>
/// <param name="Restitution">How much kinetic energy is conserved in a bounce. 1 = perfect bounce, 0 = no bounce.</param>
/// <param name="Friction">How much tangential velocity is lost to friction. 0 = frictionless, 1 = full stop.</param>
/// <param name="KickSpeed">Outward speed added along the contact normal on a successful bounce. 0 = passive.</param>
public readonly record struct BarrierMaterial(
    float Restitution = 1f,
    float Friction = 0f,
    float KickSpeed = 0f)
{
    /// <summary>Perfectly elastic, frictionless, no kick. Adds no character beyond the barrier's shape.</summary>
    public static BarrierMaterial Ideal => new(1f, 0f, 0f);

    // Passive materials.
    public static BarrierMaterial Metal => new(0.95f, 0.05f, 0f);
    public static BarrierMaterial Wood => new(0.45f, 0.30f, 0f);
    public static BarrierMaterial Concrete => new(0.50f, 0.55f, 0f);
    public static BarrierMaterial Dirt => new(0.20f, 0.80f, 0f);
    public static BarrierMaterial Grass => new(0.25f, 0.70f, 0f);
    public static BarrierMaterial Sand => new(0.05f, 0.95f, 0f);
    public static BarrierMaterial Rubber => new(0.85f, 0.55f, 0f);
    public static BarrierMaterial Felt => new(0.50f, 0.40f, 0f);
    public static BarrierMaterial Pillow => new(0.10f, 0.90f, 0f);
    public static BarrierMaterial Ice => new(0.30f, 0.02f, 0f);
    public static BarrierMaterial OilSlick => new(0.35f, 0.005f, 0f);

    // Active devices — KickSpeed > 0 means the barrier injects energy on each hit.
    public static BarrierMaterial Trampoline => new(1.0f, 0.10f, 80f);
}
