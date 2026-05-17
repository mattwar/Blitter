namespace Blitter.Blocks;

/// <summary>
/// A stacked drawable layer in a <see cref="Scene2D"/>. Scenes
/// composite layers back-to-front each tick: every <see cref="Enabled"/>
/// layer is updated, every <see cref="Visible"/> layer is drawn.
/// Concrete layers manage their own contents (see
/// <see cref="PlayField2D"/> for sprites + barriers).
/// </summary>
public abstract class Layer2D : IUpdatable<UpdateContext2D>, IDrawable2D
{
    /// <summary>When false the scene skips this layer's update.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>When false the scene skips this layer's draw.</summary>
    public bool Visible { get; set; } = true;

    /// <summary>
    /// The <see cref="Scene2D"/> this layer is a member of.
    /// </summary>
    public Scene2D Scene =>
        _scene ?? throw new InvalidOperationException("Layer is not attached to a Scene. Access Scene only while the layer is a member of one.");

    // Scene backing field; set by Scene2D when the layer is added.
    internal Scene2D? _scene;

    /// <summary>Advance the layer's contents by one tick.</summary>
    public abstract void Update(in UpdateContext2D context);

    /// <summary>Render the layer's current state.</summary>
    public abstract void Draw(Renderer2D renderer);
}
