using System;
using System.Collections.Generic;
using UnityEngine;

using Unity.Entities;

[UpdateInGroup(typeof(ManagerSystemGroup))]
public partial class ReplaceTagComponentSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities.
            WithStructuralChanges().
            ForEach(
            (Entity trigger, in ComputePlanSwicher swicher) =>
            {
                Dependency.Complete();

                SwitchComputeNeighborsPlan(EntityManager, swicher);

                EntityManager.DestroyEntity(trigger);
            }).Run();
    }

    public static void SwitchComputeNeighborsPlan(EntityManager manager, ComputePlanSwicher swicher)
    {
        var prefab_entity = swicher.Prefab;
        var boids_query = swicher.Query;
        var old_plan = swicher.RemoveTarget;
        var new_plan = swicher.AddTarget;

        switch (old_plan)
        {
            case ComputeNeighborsPlan.Direct:
                RemoveComponentFromBoids<Tag_ComputeNeighbors_Direct>(manager, boids_query, prefab_entity);
                break;
            case ComputeNeighborsPlan.CellIndex_Entity_NeighborList:
                RemoveComponentFromBoids<Tag_ComputeNeighbors_CellIndex_Entity_NeighborList>(manager, boids_query, prefab_entity);
                break;
            case ComputeNeighborsPlan.CellIndex_Cell_NeighborList:
                RemoveComponentFromBoids<Tag_ComputeNeighbors_CellIndex_Cell_NeighborList>(manager, boids_query, prefab_entity);
                break;
            case ComputeNeighborsPlan.CellIndex_Cell_Cell:
                RemoveComponentFromBoids<Tag_ComputeNeighbors_CellIndex_Cell_Cell>(manager, boids_query, prefab_entity);
                break;
            case ComputeNeighborsPlan.CellIndex_Combined_CNL:
                RemoveComponentFromBoids<Tag_ComputeNeighbors_CellIndex_Combined_CNL>(manager, boids_query, prefab_entity);
                break;
            case ComputeNeighborsPlan.CellIndex_Combined_CC:
                RemoveComponentFromBoids<Tag_ComputeNeighbors_CellIndex_Combined_CC>(manager, boids_query, prefab_entity);
                break;
            default: throw new ArgumentOutOfRangeException(nameof(old_plan));
        }

        switch (old_plan)
        {
            case ComputeNeighborsPlan.CellIndex_Combined_CNL:
            case ComputeNeighborsPlan.CellIndex_Combined_CC:
                AddComponentToBoids<Tag_UpdateInteraction>(manager, boids_query, prefab_entity);
                AddBufferToBoids<NeighborsEntityBuffer>(manager, boids_query, prefab_entity);
                break;
            default:
                break;
        }

        switch (new_plan)
        {
            case ComputeNeighborsPlan.Direct:
                AddComponentToBoids<Tag_ComputeNeighbors_Direct>(manager, boids_query, prefab_entity);
                break;
            case ComputeNeighborsPlan.CellIndex_Entity_NeighborList:
                AddComponentToBoids<Tag_ComputeNeighbors_CellIndex_Entity_NeighborList>(manager, boids_query, prefab_entity);
                break;
            case ComputeNeighborsPlan.CellIndex_Cell_NeighborList:
                AddComponentToBoids<Tag_ComputeNeighbors_CellIndex_Cell_NeighborList>(manager, boids_query, prefab_entity);
                break;
            case ComputeNeighborsPlan.CellIndex_Cell_Cell:
                AddComponentToBoids<Tag_ComputeNeighbors_CellIndex_Cell_Cell>(manager, boids_query, prefab_entity);
                break;
            case ComputeNeighborsPlan.CellIndex_Combined_CNL:
                AddComponentToBoids<Tag_ComputeNeighbors_CellIndex_Combined_CNL>(manager, boids_query, prefab_entity);
                break;
            case ComputeNeighborsPlan.CellIndex_Combined_CC:
                AddComponentToBoids<Tag_ComputeNeighbors_CellIndex_Combined_CC>(manager, boids_query, prefab_entity);
                break;
            default: throw new ArgumentOutOfRangeException(nameof(new_plan));
        }

        switch (new_plan)
        {
            case ComputeNeighborsPlan.CellIndex_Combined_CNL:
            case ComputeNeighborsPlan.CellIndex_Combined_CC:
                RemoveComponentFromBoids<Tag_UpdateInteraction>(manager, boids_query, prefab_entity);
                RemoveBufferFromBoids<NeighborsEntityBuffer>(manager, boids_query, prefab_entity);
                break;
            default:
                break;
        }
    }
    private static void RemoveComponentFromBoids<T>(EntityManager manager, EntityQuery boids_query, Entity prefab)
        where T : IComponentData
    {
        manager.RemoveComponent<T>(prefab);
        manager.RemoveComponent<T>(boids_query);
    }
    private static void AddComponentToBoids<T>(EntityManager manager, EntityQuery boids_query, Entity prefab)
        where T : IComponentData
    {
        manager.AddComponent<T>(prefab);
        manager.AddComponent<T>(boids_query);
    }
    private static void RemoveBufferFromBoids<T>(EntityManager manager, EntityQuery boids_query, Entity prefab)
        where T : IBufferElementData
    {
        manager.RemoveComponent<T>(prefab);
        manager.RemoveComponent<T>(boids_query);
    }
    private static void AddBufferToBoids<T>(EntityManager manager, EntityQuery boids_query, Entity prefab)
        where T : IBufferElementData
    {
        manager.AddComponent<T>(prefab);
        manager.AddComponent<T>(boids_query);
    }
}
