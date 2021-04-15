using Unity.Entities;
using Unity.Mathematics;

public struct Velocity : IComponentData
{
    public float3 Value;
}
public struct Acceleration : IComponentData
{
    public float3 Value;
}

[InternalBufferCapacity(8)]
public struct NeighborsEntityBuffer : IBufferElementData
{
    public Entity entity;
}

public struct BoidPrefabType : IComponentData { }
public struct BoidType : IComponentData { }