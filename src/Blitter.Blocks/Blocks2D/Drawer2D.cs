namespace Blitter.Blocks2D;

/// <summary>
/// Draws entity trees by calling any entity that implements <see cref="IDrawable2D"/>.
/// </summary>
public sealed class Drawer2D
{
    /// <summary>
    /// Shared stateless drawer used by built-in runners.
    /// </summary>
    public static Drawer2D Default { get; } = new();

    /// <summary>
    /// Draws <paramref name="entity"/> or, when it is not drawable, any drawable children.
    /// </summary>
    public void Draw(IEntity entity, Renderer2D renderer)
    {
        if (entity is IDrawable2D drawable)
        {
            drawable.Draw(renderer);
            DrawBehaviors(entity, renderer);
            return;
        }

        DrawBehaviors(entity, renderer);

        if (entity is not IContainer container)
            return;

        for (int i = 0; i < container.Entities.Count; i++)
            Draw(container.Entities[i], renderer);
    }

    private static void DrawBehaviors(IEntity entity, Renderer2D renderer)
    {
        for (int i = 0; i < entity.Behaviors.Count; i++)
        {
            if (entity.Behaviors[i] is IDrawable2D drawable)
                drawable.Draw(renderer);
        }
    }
}