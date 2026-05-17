namespace Blitter.Blocks;

public sealed class CustomProp2D : Prop2D
{
    public Func<UpdateContext2D, bool>? OnUpdate { get; set; }
    public Action<Renderer2D>? OnRender { get; set; }

    public CustomProp2D()
    { 
    }

    public override bool Update(in UpdateContext2D context)
    {
        return this.OnUpdate?.Invoke(context) ?? false;
    }

    public override void Draw(Renderer2D renderer)
    {
        this.OnRender?.Invoke(renderer);
    }
}