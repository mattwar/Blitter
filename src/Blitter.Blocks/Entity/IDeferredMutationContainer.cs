namespace Blitter.Blocks;

/// <summary>
/// A container that buffers membership changes while an external operation traverses its entities.
/// </summary>
public interface IDeferredMutationContainer : IContainer
{
    /// <summary>Begin buffering add/remove operations.</summary>
    void BeginMutationBuffer();

    /// <summary>Flush buffered add/remove operations.</summary>
    void EndMutationBuffer();
}