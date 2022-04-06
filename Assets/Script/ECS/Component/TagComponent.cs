using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Unity.Entities;


public struct BoidType : IComponentData { }

public struct Tag_UpdateInteraction : IComponentData { }

public struct Tag_ComputeNeighbors_Direct : IComponentData { }
public struct Tag_ComputeNeighbors_CellIndex_Entity_NeighborList : IComponentData { }
public struct Tag_ComputeNeighbors_CellIndex_Cell_NeighborList : IComponentData { }
public struct Tag_ComputeNeighbors_CellIndex_Cell_Cell : IComponentData { }
public struct Tag_ComputeNeighbors_CellIndex_Combined : IComponentData { }

public enum ComputeNeighborsPlan
{
    Direct,

    CellIndex_Entity_NeighborList,
    CellIndex_Cell_NeighborList,
    CellIndex_Cell_Cell,

    CellIndex_Combined,
}

public struct ComputePlanSwicher : IComponentData
{
    public Entity Prefab;
    public EntityQuery Query;
    public ComputeNeighborsPlan RemoveTarget, AddTarget;
}
