namespace Blitter.Bits;

/// <summary>
/// Base class for camera-driving controllers — objects that own a
/// <see cref="Blitter.Camera3D"/> and mutate it each frame (typically from
/// input or a tracked target). Plug into a render loop by calling
/// <see cref="Update"/> with the renderer's update
/// context, then either assign <see cref="Camera"/> to
/// <see cref="Renderer3D.Camera"/> directly or call
/// <see cref="IDrawable3D.Draw"/> to do the same as a one-liner.
/// </summary>
public abstract class CameraController : IDrawable3D
{
    /// <summary>
    /// The camera this controller drives. Defaults to a fresh
    /// <see cref="PerspectiveCamera"/>; assign your own to use a
    /// different projection or pre-configured starting pose.
    /// </summary>
    public Camera3D Camera { get; set; } = new PerspectiveCamera();

    public abstract void Update(in UpdateContext context);

    /// <summary>
    /// Default render-side action for a controller: install
    /// <see cref="Camera"/> on the renderer. Override only if your
    /// controller also wants to issue debug draws or push other
    /// per-frame scene state.
    /// </summary>
    public virtual void Draw(Renderer3D renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        renderer.Camera = Camera;
    }
}
