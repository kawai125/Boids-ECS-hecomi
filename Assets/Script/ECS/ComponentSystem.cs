using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Unity.Jobs;
using Unity.Burst;


[UpdateInGroup(typeof(SimulationSystemGroup))]
public class BoidSystemGroup : ComponentSystemGroup { }



[UpdateInGroup(typeof(BoidSystemGroup))]
public partial class WallSystem : SystemBase
{
    private struct ComputeWallForce
    {
        private float scale;
        private float thresh;
        private float weight;

        public void SetParam([ReadOnly] Param param)
        {
            scale = param.wallScale * 0.5f;
            thresh = param.wallDistance;
            weight = param.wallWeight;
        }

        public Acceleration Compute(in Acceleration acc_in, in Translation pos_in)
        {
            float3 pos = pos_in.Value;
            float3 acc = acc_in.Value;

            acc.x += GetAccelAgainstWall(pos.x, thresh, weight);
            acc.y += GetAccelAgainstWall(pos.y, thresh, weight);
            acc.z += GetAccelAgainstWall(pos.z, thresh, weight);

            return new Acceleration { Value = acc };
        }
        private float GetAccelAgainstWall(float x, float thresh, float weight)
        {
            const float solid_coef = 10f;

            // outside of wall
            if(x >= scale)
            {
                return -solid_coef * weight * thresh;
            }
            else if(x <= -scale)
            {
                return solid_coef * weight * thresh;
            }

            // inside of wall
            float dist = scale - math.abs(x);
            if(x >= 0f)
            {
                return -weight * thresh / dist;
            }
            else
            {
                return weight * thresh / dist;
            }
        }
    }

    protected override void OnUpdate()
    {
        var compute_wall = new ComputeWallForce();
        compute_wall.SetParam(Bootstrap.Param);

        Dependency = Entities.
            WithName("WallJob").
            WithAll<BoidType>().
            WithBurst().
            ForEach((ref Acceleration accel, in Translation pos) =>
            {
                accel = compute_wall.Compute(accel, pos);
        }).ScheduleParallel(Dependency);
    }
}


[UpdateAfter(typeof(BoidSystemGroup))]
public partial class MoveSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float dt = Time.DeltaTime;
        float minSpeed = Bootstrap.Param.maxSpeed;
        float maxSpeed = Bootstrap.Param.maxSpeed;

        Dependency = Entities.
            WithName("MoveJob").
            WithAll<BoidType>().
            WithBurst().
            ForEach((ref Translation pos, ref Rotation rotate, ref Velocity vel, ref Acceleration accel) =>
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

[UpdateBefore(typeof(BoidSystemGroup))]
public partial class NeighborDetectionSystem : SystemBase
{
    private EntityQuery query;

    protected override void OnCreate()
    {
        base.OnCreate();

        query = GetEntityQuery(new EntityQueryDesc
        {
            All = new[] { 
                ComponentType.ReadOnly<BoidType>(),
                ComponentType.ReadOnly<Translation>(),
                ComponentType.ReadOnly<Velocity>(),
                ComponentType.ReadWrite<NeighborsEntityBuffer>()
            }
        });
    }

    private struct NeighborDetectionDataContainer
    {
        [ReadOnly] public float prodThresh;
        [ReadOnly] public float distThresh;

        [ReadOnly]
        public ComponentDataFromEntity<Translation> positionFromGrovalEntity;

        [DeallocateOnJobCompletion]
        [ReadOnly]
        public NativeArray<Entity> entitiesGroval;
    }

    protected override void OnUpdate()
    {
        var common_data = new NeighborDetectionDataContainer
        {
            prodThresh = math.cos(math.radians(Bootstrap.Param.neighborFov)),
            distThresh = Bootstrap.Param.neighborDistance,

            positionFromGrovalEntity = GetComponentDataFromEntity<Translation>(true),

            entitiesGroval = query.ToEntityArray(Allocator.TempJob),
        };

        // args for Entities.ForEach() must be ordered as (Entity), (ref args), and (in args).
        Dependency = Entities.
            WithName("NeighborDetectionJob").
            WithAll<BoidType>().
            WithBurst().
            ForEach(
        (Entity entity, ref DynamicBuffer<NeighborsEntityBuffer> neighbors, in Translation position, in Velocity velocity) =>
        {
            neighbors.Clear();

            float3 pos0 = position.Value;
            float3 fwd0 = math.normalize(velocity.Value);

            for(int i=0; i<common_data.entitiesGroval.Length; i++)
            {
                var neighbor = common_data.entitiesGroval[i];
                if (entity == neighbor) continue;

                float3 pos1 = common_data.positionFromGrovalEntity[neighbor].Value;
                var to = pos1 - pos0;
                var dist = math.length(to);

                if(dist < common_data.distThresh)
                {
                    var dir = math.normalize(to);
                    var prod = math.dot(dir, fwd0);
                    if(prod > common_data.prodThresh)
                    {
                        neighbors.Add(new NeighborsEntityBuffer { entity = neighbor });
                    }
                }
            }
        }).ScheduleParallel(Dependency);
    }
}


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
            separationWeight = Bootstrap.Param.separationWeight,
            positionFromGrovalEntity = GetComponentDataFromEntity<Translation>(true), // bool input = is_read_only (default = false)
        };

        Dependency = Entities.
            WithName("SeparationJob").
            WithAll<BoidType>().
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
            WithAll<BoidType>().
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


[UpdateInGroup(typeof(BoidSystemGroup))]
public partial class CohesionSystem : SystemBase
{
    private struct CohesionDataContainer
    {
        [ReadOnly] public float alignmentWeight;
        [ReadOnly] public ComponentDataFromEntity<Translation> positionFromGrovalEntity;
    }

    protected override void OnUpdate()
    {
        var common_data = new CohesionDataContainer
        {
            alignmentWeight = Bootstrap.Param.alignmentWeight,
            positionFromGrovalEntity = GetComponentDataFromEntity<Translation>(true), // bool input = is_read_only (default = false)
        };

        Dependency = Entities.
            WithName("CohesionJob").
            WithAll<BoidType>().
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

            acc += (pos_avg - pos0) * common_data.alignmentWeight;

            accel = new Acceleration { Value = acc };
        }
        ).ScheduleParallel(Dependency);
    }
}
