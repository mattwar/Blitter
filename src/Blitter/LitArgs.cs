using System.Numerics;
using System.Runtime.InteropServices;

namespace Blitter;

/// <summary>
/// Per-draw arguments for <see cref="Shaders.LitColor"/> shader.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct LitArgs : IUniformArgs<LitArgs>
{
    /// <summary>
    /// World transform.
    /// You need to specify this one.
    /// </summary>
    public Matrix4x4 Model;

    /// <summary>
    /// Camera view-projection. 
    /// Filled in by the renderer.
    /// </summary>
    public Matrix4x4 ViewProjection;

    /// <summary>
    /// Ambient color (RGBA, 0..1). 
    /// Filled in by the renderer.
    /// </summary>
    public Vector4 AmbientLight;

    /// <summary>
    /// Directional light direction in world space (xyz; w unused).
    /// Filled in by the renderer. 
    /// </summary>
    public Vector4 LightDirection;

    /// <summary>
    /// Directional light color (RGBA, 0..1). 
    /// Filled in by the renderer.
    /// </summary>
    public Vector4 LightColor;

    /// <summary>
    /// Point light count packed into <c>.X</c>
    /// Filled in by the renderer.
    /// Uses Vector4 for alignment; other components unused.
    /// </summary>
    public Vector4 PointLightCount;

    /// <summary>
    /// Constructs a <see cref="LitArgs"/> instance with the only required argument: the world transform.
    /// </summary>
    public LitArgs(Matrix4x4 model)
    {
        Model = model;
        ViewProjection = Matrix4x4.Identity;
        AmbientLight = Vector4.Zero;
        LightDirection = Vector4.Zero;
        LightColor = Vector4.Zero;
        PointLightCount = Vector4.Zero;
    }

    public static implicit operator LitArgs(Matrix4x4 model) => 
        new(model);

    /// <inheritdoc cref="IUniformArgs{TSelf}.SetViewProjection"/>
    public static Func<LitArgs, Matrix4x4, LitArgs>? SetViewProjection { get; } =
        (a, vp) => { a.ViewProjection = vp; return a; };

    /// <inheritdoc cref="IUniformArgs{TSelf}.SetAmbientLight"/>
    public static Func<LitArgs, Vector4, LitArgs>? SetAmbientLight { get; } =
        (a, amb) => { a.AmbientLight = amb; return a; };

    /// <inheritdoc cref="IUniformArgs{TSelf}.SetDirectionalLight"/>
    public static Func<LitArgs, DirectionalLight, LitArgs>? SetDirectionalLight { get; } =
        (a, light) =>
        {
            a.LightDirection = new Vector4(light.Direction, 0f);
            a.LightColor = light.Color;
            return a;
        };

    /// <inheritdoc cref="IUniformArgs{TSelf}.SetPointLightCount"/>
    public static Func<LitArgs, int, LitArgs>? SetPointLightCount { get; } =
        (a, count) => { a.PointLightCount = new Vector4(count, 0f, 0f, 0f); return a; };
}
