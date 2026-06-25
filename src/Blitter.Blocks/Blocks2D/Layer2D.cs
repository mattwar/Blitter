using System.Numerics;


namespace Blitter.Blocks2D;

/// <summary>
/// A stacked drawable layer in a 2D entity container. Entity runners
/// composite layers back-to-front each tick: every <see cref="Enabled"/>
/// layer is updated, every <see cref="Visible"/> layer is drawn.
/// Concrete layers manage their own contents (see
/// <see cref="PlayField2D"/> for sprites + barriers).
/// </summary>
public abstract class Layer2D : Entity, IDrawable2D, IUpdateEnabled, INamedEntity
{
    /// <summary>
    /// Optional scene-unique name. When set, other nodes can resolve this
    /// entity through <see cref="EntityExtensions.GetEntity{T}(IContainerEntity, string)"/> in their
    /// <c>OnAttach</c> hook instead of capturing it in a local variable.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// When false the updater skips this layer's update.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// When false the drawer skips this layer's draw.
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
        if (!Visible)
            return;

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

