namespace Blitter.Blocks3D;

/// <summary>
/// Handler for <see cref="IVoxelWorld.VoxelsChanged"/>.
/// </summary>
public delegate void VoxelsChangedHandler(IVoxelWorld world, in VoxelBox change);
