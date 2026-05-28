using System.Numerics;
using System.Runtime.InteropServices;

namespace Blitter;

/// <summary>
/// A point light source. 
/// A position in world space that radiates light equally in every direction. 
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct PointLight
{
    private readonly Vector4 _positionRange;   // xyz = position, w = range
    private readonly Vector4 _colorIntensity;  // rgb = color, a = intensity

    /// <summary>World-space position of the light.</summary>
    public Vector3 Position => new(_positionRange.X, _positionRange.Y, _positionRange.Z);

    /// <summary>
    /// Distance at which the light's contribution fades to zero. 
    /// </summary>
    public float Range => _positionRange.W;

    /// <summary>
    /// The light's color.
    /// </summary>
    public Color Color => new(
        (byte)Math.Clamp(_colorIntensity.X * 255f, 0, 255),
        (byte)Math.Clamp(_colorIntensity.Y * 255f, 0, 255),
        (byte)Math.Clamp(_colorIntensity.Z * 255f, 0, 255),
        255);

    /// <summary>
    /// Multiplier on top of <see cref="Color"/>. 
    /// Lets you scale a light brighter than 1.0 without leaving the 0..255 color space; 
    /// </summary>
    public float Intensity => _colorIntensity.W;

    /// <summary>
    /// Creates a <see cref="PointLight"/>
    /// </summary>
    /// <param name="position">World-space position of the light.</param>
    /// <param name="color">The light's color.</param>
    /// <param name="range">Distance at which the light's contribution fades to zero.</param>
    /// <param name="intensity">Brightness multiplier on top of <see cref="Color"/>.</param>
    public PointLight(Vector3 position, Color color, float range, float intensity = 1f)
    {
        _positionRange = new Vector4(position, range);
        Vector4 c = color;
        _colorIntensity = new Vector4(c.X, c.Y, c.Z, intensity);
    }
}
