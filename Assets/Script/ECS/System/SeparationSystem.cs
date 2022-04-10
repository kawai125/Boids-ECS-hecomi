using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Unity.Jobs;
using Unity.Burst;


[UpdateInGroup(typeof(BoidSystemGroup))]
public partial class SeparationSystem : SystemBase
{
    private struct SeparationDataContainer
    {
        [ReadOnly] public float separationWeight;
        [ReadOnly] public ComponentDataFromEntity<Translation> positionFromGrovalEntity;
    }

    protected override void OnUpdate()
    {
        var common_data = new SeparationDataContainer
        {
            separationWeight = BoidParams_Bootstrap.Param.separationWeight,
            positionFromGrovalEntity = GetComponentDataFromEntity<Translation>(true), // bool input = is_read_only (default = false)
        };

        Dependency = Entities.
            WithName("SeparationJob").
            WithAll<BoidType, Tag_UpdateInteraction>().
            WithBurst().
            ForEach(
        (ref Acceleration accel, in Translation position, in DynamicBuffer<NeighborsEntityBuffer> neighbors) =>
        {
            if (neighbors.Length == 0) return;

            float3 pos0 = position.Value;
            float3 force = float3.zero;
            for(int i=0; i<neighbors.Length; i++)
            {
                float3 pos1 = common_data.positionFromGrovalEntity[neighbors[i].entity].Value;
                force += math.normalize(pos0 - pos1);
            }
            force /= neighbors.Length;

            accel = new Acceleration { Value = accel.Value + force * common_data.separationWeight };
        }).ScheduleParallel(Dependency);
    }
}
