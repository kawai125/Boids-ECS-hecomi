using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Unity.Jobs;
using Unity.Burst;


[UpdateInGroup(typeof(BoidSystemGroup))]
public partial class CohesionSystem : SystemBase
{
    private struct CohesionDataContainer
    {
        [ReadOnly] public float cohesionWeight;
        [ReadOnly] public ComponentDataFromEntity<Translation> positionFromGrovalEntity;
    }

    protected override void OnUpdate()
    {
        var common_data = new CohesionDataContainer
        {
            cohesionWeight = BoidParams_Bootstrap.Param.cohesionWeight,
            positionFromGrovalEntity = GetComponentDataFromEntity<Translation>(true), // bool input = is_read_only (default = false)
        };

        Dependency = Entities.
            WithName("CohesionJob").
            WithAll<BoidType, Tag_UpdateInteraction>().
            WithBurst().
            ForEach(
        (ref Acceleration accel, in Translation pos, in DynamicBuffer<NeighborsEntityBuffer> neighbors) =>
        {
            if (neighbors.Length == 0) return;

            float3 pos_avg = float3.zero;
            float3 pos0 = pos.Value;
            float3 acc = accel.Value;

            for(int i=0; i<neighbors.Length; i++)
            {
                pos_avg += common_data.positionFromGrovalEntity[neighbors[i].entity].Value;
            }
            pos_avg /= neighbors.Length;

            acc += (pos_avg - pos0) * common_data.cohesionWeight;

            accel = new Acceleration { Value = acc };
        }
        ).ScheduleParallel(Dependency);
    }
}
