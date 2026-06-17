namespace Blitter.Blocks3D;

/// <summary>
/// A layer that delegates update and draw to caller-supplied callbacks.
/// Handy for HUDs, background fills, debug overlays, and other one-off
/// layers that don't warrant their own subclass.
/// </summary>
public sealed class CustomLayer3D : Layer3D
{
    public Action<UpdateContext>? OnUpdate { get; set; }
    public Action<Renderer3D>? OnRender { get; set; }

    public override void Update(in UpdateContext context)
    {
        OnUpdate?.Invoke(context);
    }

    public override void Draw(Renderer3D renderer)
    {
        OnRender?.Invoke(renderer);
    }
}
