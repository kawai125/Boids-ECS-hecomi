using System;

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

[InternalBufferCapacity(12)]
public struct NeighborsEntityBuffer : IBufferElementData, IEquatable<NeighborsEntityBuffer>
{
    public Entity entity;

    public bool Equals(NeighborsEntityBuffer rhs) { return entity.Equals(rhs.entity); }
    public override string ToString()
    {
        return entity.ToString();
    }
}

public struct BoidsSpawner : IComponentData
{
    public Entity Prefab;
    public int n;
    public float scale;
    public float initSpeed;
}
