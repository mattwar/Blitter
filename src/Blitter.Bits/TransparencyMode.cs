namespace Blitter.Bits;

/// <summary>
/// How a surface's alpha channel is composited when it is drawn.
/// </summary>
public enum TransparencyMode
{
    /// <summary>
    /// Fully opaque: the alpha channel is ignored. Writes depth and
    /// occludes everything behind it. The default and the cheapest.
    /// </summary>
    Opaque,

    /// <summary>
    /// Alpha-cutout (alpha test): texels whose alpha is below a fixed
    /// 0.5 cutoff are discarded, the rest draw fully opaque. Gives crisp
    /// see-through holes (foliage, grates, chain-link) that still write
    /// depth, so no sorting is needed. Binary -- there are no partly
    /// translucent pixels.
    /// </summary>
    Cutout,

    /// <summary>
    /// Alpha blend: the surface is composited over what's already in the
    /// frame using its alpha, so partly transparent pixels (tinted glass,
    /// water) show the scene behind them. The draw tests depth but does
    /// not write it, and the result is order-dependent -- it must be
    /// drawn after the opaque scene behind it.
    /// </summary>
    Blend,
}
