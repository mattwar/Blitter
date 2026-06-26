namespace Blitter.Blocks;

/// <summary>
/// Per-frame inputs for updating an entity tree.
/// </summary>
public readonly struct EntityUpdateContext : IUpdateContext
{
    public EntityUpdateContext(
        TimeSpan elapsedSinceStart,
        TimeSpan elapsedSinceLastUpdate,
        FrameInput? input = null,
        IEntityRunControl? runControl = null)
    {
        ElapsedSinceStart = elapsedSinceStart;
        ElapsedSinceLastUpdate = elapsedSinceLastUpdate;
        Input = input;
        RunControl = runControl;
    }

    public EntityUpdateContext(IUpdateContext context, IEntityRunControl? runControl = null, FrameInput? input = null)
        : this(context.ElapsedSinceStart, context.ElapsedSinceLastUpdate, input, runControl)
    {
    }

    public TimeSpan ElapsedSinceStart { get; init; }

    public TimeSpan ElapsedSinceLastUpdate { get; init; }

    public FrameInput? Input { get; init; }

    public IEntityRunControl? RunControl { get; init; }
}

/// <summary>
/// Runtime controls exposed to entity update logic by an active runner.
/// </summary>
public interface IEntityRunControl
{
    RunState RunState { get; }

    void RequestExit();

    void RequestExitAfter(TimeSpan duration);

    void RequestExitWhen(EntityExitCondition until);
}

/// <summary>
/// Updates an entity or operation using the current entity update context.
/// </summary>
public delegate void EntityUpdateAction(in EntityUpdateContext context);

/// <summary>
/// A predicate that determines when a requested run exit may complete.
/// </summary>
public delegate bool EntityExitCondition(in EntityUpdateContext context);