namespace Blitter.Blocks2D;

/// <summary>
/// A layer that delegates update and draw to caller-supplied
/// callbacks. Handy for HUDs, background fills, debug overlays, and
/// other one-off layers that don't warrant their own subclass.
/// </summary>
public sealed class CustomLayer2D : Layer2D
{
    public Action<UpdateContext>? OnUpdate { get; set; }
    public Action<Renderer2D>? OnRender { get; set; }

    public override void Update(in UpdateContext context)
    {
        OnUpdate?.Invoke(context);
    }

    protected override void DrawContent(Renderer2D renderer)
    {
        OnRender?.Invoke(renderer);
    }
}
