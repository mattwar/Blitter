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
        if (entity.TryGetCapability<IVisibility>(out var visibility) && !visibility.Visible)
            return;

        if (HasDrawableSetup(entity))
        {
            using var _ = renderer.PushState();
            SetupDrawables(entity, renderer);
            DrawCore(entity, renderer);
            return;
        }

        DrawCore(entity, renderer);
    }

    private void DrawCore(IEntity entity, Renderer2D renderer)
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
        {
            Draw(container.Entities[i], renderer);
        }
    }

    private static bool HasDrawableSetup(IEntity entity)
    {
        if (entity is IDrawableSetup2D)
            return true;

        for (int i = 0; i < entity.Behaviors.Count; i++)
        {
            if (entity.Behaviors[i] is IDrawableSetup2D)
                return true;
        }

        return false;
    }

    private static void SetupDrawables(IEntity entity, Renderer2D renderer)
    {
        if (entity is IDrawableSetup2D entitySetup)
            entitySetup.Setup(renderer);

        for (int i = 0; i < entity.Behaviors.Count; i++)
        {
            if (entity.Behaviors[i] is IDrawableSetup2D setup)
                setup.Setup(renderer);
        }
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