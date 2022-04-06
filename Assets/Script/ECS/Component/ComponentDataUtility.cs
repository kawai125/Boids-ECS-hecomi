using System;
using System.Diagnostics;
using UnityEngine;

using Unity.Collections;
using Unity.Entities;

static internal class ComponentDataUtility
{
    internal static NativeArray<T> GatherComponentData<T>(ComponentDataFromEntity<T> data_from_entity,
                                                          NativeArray<Entity> entities,
                                                          Allocator alloc)
        where T : unmanaged, IComponentData
    {
        var array = new NativeArray<T>(entities.Length, alloc, NativeArrayOptions.UninitializedMemory);
        GatherComponentDataImpl(data_from_entity, entities, array);
        return array;
    }
    /*
    // cannot pass containers which allocated by Allocator.Temp
    // at Unity 2020.3.32 LTS
    internal static void GatherComponentData<T>(ComponentDataFromEntity<T> data_from_entity,
                                                NativeArray<Entity> entities,
                                                NativeList<T> data_list)
        where T : unmanaged, IComponentData
    {
        data_list.ResizeUninitialized(entities.Length);
        GatherComponentDataImpl(data_from_entity, entities, data_list.AsArray());
    }
    internal static void GatherComponentData<T>(ComponentDataFromEntity<T> data_from_entity,
                                                NativeArray<Entity> entities,
                                                NativeArray<T> array)
        where T : unmanaged, IComponentData
    {
        CheckArrayLength(entities, array);
        GatherComponentDataImpl(data_from_entity, entities, array);
    }
    */
    private static void GatherComponentDataImpl<T>(ComponentDataFromEntity<T> data_from_entity,
                                                   NativeArray<Entity> entities,
                                                   NativeArray<T> data_array)
        where T : unmanaged, IComponentData
    {
        for (int i = 0; i < entities.Length; i++)
        {
            data_array[i] = data_from_entity[entities[i]];
        }
    }

    [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
    private static void CheckArrayLength<T>(NativeArray<Entity> entities, NativeArray<T> array)
        where T : unmanaged
    {
        if (array.Length != entities.Length)
            throw new ArgumentException($"length of entities={entities.Length} and array={array.Length} are differ.");
    }
}
