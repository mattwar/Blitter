using System.Numerics;


namespace Blitter.Blocks2D;

/// <summary>
/// A stacked drawable layer in a <see cref="Scene2D"/>. Scenes
/// composite layers back-to-front each tick: every <see cref="Enabled"/>
/// layer is updated, every <see cref="Visible"/> layer is drawn.
/// Concrete layers manage their own contents (see
/// <see cref="PlayField2D"/> for sprites + barriers).
/// </summary>
public abstract class Layer2D : Entity, IDrawable2D
{
    /// <summary>
    /// Optional scene-unique name. When set, other nodes can resolve this
    /// layer through <see cref="Scene2D.GetLayer{T}(string)"/> in their
    /// <c>OnAttach</c> hook instead of capturing it in a local variable.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The <see cref="Scene2D"/> this layer belongs to.
    /// </summary>
    public Scene2D Scene =>
        this.Parent as Scene2D 
            ?? throw new InvalidOperationException("Layer is not attached to a Scene. Access Scene only while the layer is part of a running scene.");

    /// <summary>
    /// When false the scene skips this layer's update.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// When false the scene skips this layer's draw.
    /// </summary>
    public bool Visible { get; set; } = true;

    /// <summary>
    /// Per-axis parallax factor applied to the active camera when this
    /// layer draws. <c>(1, 1)</c> moves with the foreground (default),
    /// <c>(0, 0)</c> is locked to the screen, values in between drift
    /// (distant background), and values &gt; 1 move faster than the
    /// foreground. Has no effect when the renderer has no camera.
    /// </summary>
    public Vector2 ParallaxFactor { get; set; } = Vector2.One;

    /// <summary>
    /// Renders the layer. Applies <see cref="ParallaxFactor"/> to the
    /// renderer's camera (if any) and then calls
    /// <see cref="DrawContent"/>. Subclasses implement
    /// <see cref="DrawContent"/>, not this method.
    /// </summary>
    public void Draw(Renderer2D renderer)
    {
        if (renderer.Camera is { } main && ParallaxFactor != Vector2.One)
        {
            using var _ = renderer.PushState();
            renderer.Camera = new Camera2D
            {
                Position = main.Position * ParallaxFactor,
                Zoom = main.Zoom,
            };
            DrawContent(renderer);
        }
        else
        {
            DrawContent(renderer);
        }
    }

    /// <summary>Render the layer's current state.</summary>
    protected abstract void DrawContent(Renderer2D renderer);
}

