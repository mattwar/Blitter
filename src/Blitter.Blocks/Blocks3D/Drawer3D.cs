namespace Blitter.Blocks3D;

/// <summary>
/// Draws entity trees by calling any entity that implements <see cref="IDrawable3D"/>.
/// </summary>
public sealed class Drawer3D
{
    /// <summary>
    /// Shared stateless drawer used by built-in runners.
    /// </summary>
    public static Drawer3D Default { get; } = new();

    /// <summary>
    /// Draws <paramref name="entity"/> or, when it is not drawable, any drawable children.
    /// </summary>
    public void Draw(IEntity entity, Renderer3D renderer)
    {
        if (entity.TryGetCapability<IVisibility>(out var visibility) && !visibility.Visible)
            return;

        if (entity is IDrawable3D drawable)
        {
            drawable.Draw(renderer);
            DrawBehaviors(entity, renderer);
            return;
        }

        DrawBehaviors(entity, renderer);

        if (entity is not IContainer container)
            return;

        for (int i = 0; i < container.Entities.Count; i++)
        {
            Draw(container.Entities[i], renderer);
        }
    }

    private static void DrawBehaviors(IEntity entity, Renderer3D renderer)
    {
        for (int i = 0; i < entity.Behaviors.Count; i++)
        {
            if (entity.Behaviors[i] is IDrawable3D drawable)
                drawable.Draw(renderer);
        }
    }
}