using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Unity.Jobs;
using Unity.Burst;


[UpdateInGroup(typeof(MoveSystemGroup))]
public partial class MoveSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float dt = Time.DeltaTime;
        float minSpeed = Bootstrap.Param.minSpeed;
        float maxSpeed = Bootstrap.Param.maxSpeed;

        Dependency = Entities.
            WithName("MoveJob").
            WithAll<BoidType>().
            WithBurst().
            ForEach((ref Translation pos,
                     ref Rotation rotate,
                     ref Velocity vel,
                     ref Acceleration accel) =>
        {
            vel.Value += accel.Value * dt;

            var dir = math.normalize(vel.Value);
            var speed = math.length(vel.Value);
            vel.Value = math.clamp(speed, minSpeed, maxSpeed) * dir;

            pos.Value += vel.Value * dt;

            var rot = quaternion.LookRotationSafe(dir, new float3(0, 1, 0));

            rotate.Value = rot;
            accel.Value = float3.zero;
        }).ScheduleParallel(Dependency);
    }
}
