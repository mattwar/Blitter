namespace Blitter.Blocks;

/// <summary>
/// Capability for an object that reacts to a fire-and-forget event.
/// </summary>
/// <typeparam name="T">
/// The event payload, typically a small <c>readonly record struct</c>.
/// </typeparam>
public interface IEventHandler<T>
{
    /// <summary>Invoked once per raised event of type <typeparamref name="T"/>.</summary>
    void OnEvent(in T args);
}
