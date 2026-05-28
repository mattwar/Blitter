using System.Numerics;
using System.Runtime.InteropServices;

namespace Blitter;

/// <summary>
/// A one-field args struct whose only payload is a 4x4 transform matrix
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct TransformArgs : IUniformArgs<TransformArgs>
{
    /// <summary>The transform matrix the shader receives.</summary>
    public Matrix4x4 Matrix;

    public TransformArgs(Matrix4x4 matrix)
    {
        Matrix = matrix;
    }

    /// <summary>
    /// Implicit conversion from <see cref="Matrix4x4"/> so callers
    /// can pass a bare model matrix to scene-aware draw overloads.
    /// </summary>
    public static implicit operator TransformArgs(Matrix4x4 matrix) =>
        new TransformArgs(matrix);

    /// <inheritdoc cref="IUniformArgs{TSelf}.GetTransform"/>
    public static Func<TransformArgs, Matrix4x4>? GetTransform { get; } =
        args => args.Matrix;

    /// <inheritdoc cref="IUniformArgs{TSelf}.SetTransform"/>
    public static Func<TransformArgs, Matrix4x4, TransformArgs>? SetTransform { get; } =
        (args, m) => new TransformArgs(m);
}
