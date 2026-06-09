namespace Blitter.Blocks3D;

/// <summary>
/// One pass of a multi-pass 3D render. Geometry is drawn front-to-back in
/// the <see cref="Opaque"/> pass (filling the depth buffer) before any
/// <see cref="Transparent"/> geometry is composited over the finished
/// opaque scene. Splitting the passes lets every chunk's solid terrain
/// land in the depth buffer before any chunk's alpha-blended surfaces
/// (glass, water) draw, so transparent voxels always show the world
/// behind them instead of being repainted by a later chunk's opaque pass.
/// </summary>
public enum DrawPass3D
{
    /// <summary>
    /// Opaque and cutout geometry. Writes depth so later passes test
    /// against a complete solid scene.
    /// </summary>
    Opaque,

    /// <summary>
    /// Alpha-blended geometry, composited over the finished opaque pass
    /// with depth writes off.
    /// </summary>
    Transparent,
}
