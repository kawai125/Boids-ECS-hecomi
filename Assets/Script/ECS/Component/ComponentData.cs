﻿using System;

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
public struct NeighborsEntityBuffer : IBufferElementData
{
    public Entity entity;
}

public struct BoidsSpawner : IComponentData
{
    public Entity Prefab;
    public int n;
    public float scale;
    public float initSpeed;
}