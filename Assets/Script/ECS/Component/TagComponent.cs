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
public struct Tag_ComputeNeighbors_CellIndex_Combined_CNL : IComponentData { }
public struct Tag_ComputeNeighbors_CellIndex_Combined_CC : IComponentData { }
public struct Tag_ComputeNeighbors_CellIndex_MergedCell_NL : IComponentData { }

public enum ComputeNeighborsPlan
{
    Direct,

    CellIndex_Entity_NeighborList,
    CellIndex_Cell_NeighborList,
    CellIndex_Cell_Cell,

    CellIndex_Combined_CNL,
    CellIndex_Combined_CC,

    CellIndex_MergedCell_NL,
}

public struct ComputePlanSwicher : IComponentData
{
    public Entity Prefab;
    public EntityQuery Query;
    public ComputeNeighborsPlan RemoveTarget, AddTarget;
}
