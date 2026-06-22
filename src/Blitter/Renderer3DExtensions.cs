
namespace Blitter;

/// <summary>
/// DrawMesh overloads for <see cref="Renderer3D"/> that pick a sensible
/// built-in shader based on the mesh's vertex type. 
/// </summary>
public static class Renderer3DExtensions
{
    /// <summary>
    /// Draws a position-only mesh.
    /// </summary>
    public static void DrawMesh(this Renderer3D renderer, Mesh<Vertex3D> mesh)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        renderer.DrawMesh(mesh, Shaders.Position);
    }

    /// <summary>
    /// Draws a position-only mesh with the given position transform.
    /// </summary>
    public static void DrawMesh(this Renderer3D renderer, Mesh<Vertex3D> mesh, TransformArgs transform)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        renderer.DrawMesh(mesh, Shaders.PositionWithTransform, in transform);
    }

    /// <summary>
    /// Draws a position and color mesh.
    /// </summary>
    public static void DrawMesh(this Renderer3D renderer, Mesh<ColorVertex3D> mesh)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        renderer.DrawMesh(mesh, Shaders.PositionColor);
    }

    /// <summary>
    /// Draws a position and color mesh with the given position transform.
    /// </summary>
    public static void DrawMesh(this Renderer3D renderer, Mesh<ColorVertex3D> mesh, TransformArgs transform)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        renderer.DrawMesh(mesh, Shaders.PositionColorWithTransform, in transform);
    }

    /// <summary>
    /// Draws a position and texture mesh with the given texture.
    /// </summary>
    public static void DrawMesh(this Renderer3D renderer, Mesh<TextureVertex3D> mesh, Texture2D texture)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        renderer.DrawMesh(mesh, texture, Shaders.PositionTexture);
    }

    /// <summary>
    /// Draws a position and texture mesh with the given texture and position transform.
    /// </summary>
    public static void DrawMesh(this Renderer3D renderer, Mesh<TextureVertex3D> mesh, Texture2D texture, TransformArgs transform)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        renderer.DrawMesh(mesh, texture, Shaders.PositionTextureWithTransform, in transform);
    }

    // A unit cube centered at the origin, position-only, shared by every
    // skybox draw. The Skybox shader strips camera translation and pushes
    // the cube to the far plane, so one cube serves all cameras. Built
    // lazily so callers that never draw a skybox don't pay for it.
    private static readonly Lazy<Mesh<Vertex3D>> s_skyboxCube = new(static () =>
    {
        var verts = new Vertex3D[]
        {
            new(-1, -1, -1), new( 1, -1, -1), new( 1,  1, -1), new(-1,  1, -1),
            new(-1, -1,  1), new( 1, -1,  1), new( 1,  1,  1), new(-1,  1,  1),
        };
        var indices = new uint[]
        {
            4, 5, 6,  4, 6, 7,   1, 0, 3,  1, 3, 2,
            0, 4, 7,  0, 7, 3,   5, 1, 2,  5, 2, 6,
            7, 6, 2,  7, 2, 3,   0, 1, 5,  0, 5, 4,
        };
        return Mesh.Create(verts, indices);
    });

    /// <summary>
    /// Draws <paramref name="cubemap"/> as a skybox filling the background: 
    /// a camera-centered cube sampled by view direction and pushed to the far plane, 
    /// so all scene geometry draws in front of it. 
    /// </summary>
    public static void DrawSkybox(this Renderer3D renderer, TextureCube cubemap)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(cubemap);

        var camera = renderer.Camera
            ?? throw new InvalidOperationException(
                "DrawSkybox requires Renderer3D.Camera to be set.");

        var viewProjection = camera.GetSkyboxViewProjection(renderer.AspectRatio);
        using (renderer.PushState())
        {
            renderer.CullMode = CullMode.None;
            renderer.DrawMeshRaw(s_skyboxCube.Value, cubemap, Shaders.Skybox, in viewProjection);
        }
    }
}
