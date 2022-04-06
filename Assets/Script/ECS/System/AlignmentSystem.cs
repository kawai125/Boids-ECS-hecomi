using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Unity.Jobs;
using Unity.Burst;


[UpdateInGroup(typeof(BoidSystemGroup))]
public partial class AlignmentSystem : SystemBase
{
    private struct AlignmentDataContiner
    {
        [ReadOnly] public float alignmentWeight;
        [ReadOnly] public ComponentDataFromEntity<Velocity> velocityFromGrovalEntity;
    }

    protected override void OnUpdate()
    {
        var common_data = new AlignmentDataContiner
        {
            alignmentWeight = Bootstrap.Param.alignmentWeight,
            velocityFromGrovalEntity = GetComponentDataFromEntity<Velocity>(true),  // bool input = is_read_only (default = false)
        };

        Dependency = Entities.
            WithName("AlignmentJob").
            WithAll<BoidType, Tag_UpdateInteraction>().
            WithBurst().
            ForEach(
        (ref Acceleration accel, in Velocity velocity, in DynamicBuffer<NeighborsEntityBuffer> neighbors) =>
        {
            if (neighbors.Length == 0) return;

            float3 vel_avg = float3.zero;
            float3 vel0 = velocity.Value;

            for(int i=0; i<neighbors.Length; i++)
            {
                vel_avg += common_data.velocityFromGrovalEntity[neighbors[i].entity].Value;
            }
            vel_avg /= neighbors.Length;
            accel = new Acceleration { Value = accel.Value + (vel_avg - vel0) * common_data.alignmentWeight };
        }).ScheduleParallel(Dependency);
    }
}
