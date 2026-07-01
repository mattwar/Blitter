namespace Blitter.Blocks2D;

/// <summary>
/// Window-backed run loops for 2D entity trees.
/// </summary>
public static class RunnerExtensions2D
{
    /// <summary>
    /// Runs <paramref name="entity"/> until it requests exit, the window is closed,
    /// or the cancellation token fires.
    /// </summary>
    public static Task RunAsync(
        this IEntity entity,
        Window2D window,
        CancellationToken cancellationToken = default) =>
        entity.RunAsync(window, static _ => false, cancellationToken);

    /// <summary>
    /// Runs <paramref name="entity"/> until it requests exit, <paramref name="shouldExit"/>
    /// returns true, the window is closed, or the cancellation token fires.
    /// </summary>
    public static async Task RunAsync<TEntity>(
        this TEntity entity,
        Window2D window,
        Func<TEntity, bool> shouldExit,
        CancellationToken cancellationToken = default)
        where TEntity : IEntity
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(shouldExit);

        await new Runner2D().RunAsync(
            window,
            (in EntityUpdateContext context) => Updater.Default.Update(entity, in context),
            renderer => Drawer2D.Default.Draw(entity, renderer),
            () => shouldExit(entity),
            cancellationToken
            );
    }
}