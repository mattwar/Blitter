using System.Numerics;

namespace Blitter;

/// <summary>
/// World-space placement of 3D geometry: position, orientation, and uniform scale. 
/// </summary>
public readonly struct Pose3D
{
    /// <summary>Identity pose: at the origin, unrotated, unit scale.</summary>
    public static readonly Pose3D Identity = new(Vector3.Zero);

    /// <summary>World-space position of the local origin.</summary>
    public readonly Vector3 Position;

    /// <summary>Orientation of the local axes in world space.</summary>
    public readonly Quaternion Rotation;

    /// <summary>Uniform scale applied to the local geometry.</summary>
    public readonly float Scale;

    public Pose3D(
        Vector3 position,
        Quaternion rotation,
        float scale = 1f)
    {
        Position = position;
        Rotation = rotation;
        Scale = scale;
    }

    public Pose3D(Vector3 position, float scale = 1f)
        : this(position, Quaternion.Identity, scale)
    {
    }

    /// <summary>
    /// Transforms a local-space point to world space: scale, then
    /// rotation, then translation.
    /// </summary>
    public Vector3 Transform(Vector3 local)
    {
        var scaled = local * Scale;
        var rotated = Vector3.Transform(scaled, Rotation);
        return Position + rotated;
    }

    /// <summary>
    /// Builds the equivalent model matrix
    /// (<c>scale * rotation * translation</c>) for use in shader args.
    /// </summary>
    public Matrix4x4 ToMatrix() =>
        Matrix4x4.CreateScale(Scale)
        * Matrix4x4.CreateFromQuaternion(Rotation)
        * Matrix4x4.CreateTranslation(Position);
}
