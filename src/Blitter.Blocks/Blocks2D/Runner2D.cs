namespace Blitter.Blocks2D;

/// <summary>
/// Runs a 2D entity update/draw loop and owns transient run-loop state.
/// </summary>
public sealed class Runner2D : IEntityRunControl
{
    private readonly List<EntityExitCondition> _exitConditions = new();

    public RunState RunState { get; private set; } = RunState.Exited;

    public Task RunAsync(
        Window2D window,
        IEntity entity,
        Action<Renderer2D> draw,
        Func<bool>? shouldExit = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return RunAsync(window, (in EntityUpdateContext context) => Updater.Default.Update(entity, in context), draw, shouldExit, cancellationToken);
    }

    public async Task RunAsync(
        Window2D window,
        EntityUpdateAction update,
        Action<Renderer2D> draw,
        Func<bool>? shouldExit = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(update);
        ArgumentNullException.ThrowIfNull(draw);

        _exitConditions.Clear();
        RunState = RunState.Running;

        try
        {
            await window.RunAsync(
                shouldExit: () => RunState == RunState.Exited || (shouldExit?.Invoke() ?? false),
                renderFrame: rd =>
                {
                    var context = new EntityUpdateContext(rd.GetUpdateContext(), this);
                    update(in context);
                    EvaluateExitConditions(in context);
                    draw(rd);
                },
                cancellationToken);
        }
        finally
        {
            RunState = RunState.Exited;
        }
    }

    public void RequestExit() => RequestExitWhen(static (in _) => true);

    public void RequestExitAfter(TimeSpan duration)
    {
        TimeSpan? deadline = null;
        RequestExitWhen((in context) =>
        {
            deadline ??= context.ElapsedSinceStart + duration;
            return context.ElapsedSinceStart >= deadline.Value;
        });
    }

    public void RequestExitWhen(EntityExitCondition until)
    {
        ArgumentNullException.ThrowIfNull(until);
        if (RunState == RunState.Exited)
            return;
        _exitConditions.Add(until);
        if (RunState == RunState.Running)
            RunState = RunState.Exiting;
    }

    private void EvaluateExitConditions(in EntityUpdateContext context)
    {
        if (RunState != RunState.Exiting)
            return;

        for (int i = _exitConditions.Count - 1; i >= 0; i--)
        {
            if (_exitConditions[i](context))
                _exitConditions.RemoveAt(i);
        }
        if (_exitConditions.Count == 0)
            RunState = RunState.Exited;
    }
}