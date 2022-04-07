using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Unity.Jobs;
using Unity.Burst;


[UpdateAfter(typeof(BoidSystemGroup))]
public partial class WallSystem : SystemBase
{
    private struct ComputeWallForce
    {
        private float scale;
        private float thresh;
        private float weight;

        public void SetParam([ReadOnly] Param param)
        {
            scale = Bootstrap.WallScale * 0.5f;
            thresh = param.wallDistance;
            weight = param.wallWeight;
        }

        public Acceleration Compute(in Acceleration acc_in, in Translation pos_in)
        {
            float3 pos = pos_in.Value;
            float3 acc = acc_in.Value;

            if (IsInsideWall(pos))
            {
                acc.x += GetAccelAgainstWall(pos.x, thresh, weight);
                acc.y += GetAccelAgainstWall(pos.y, thresh, weight);
                acc.z += GetAccelAgainstWall(pos.z, thresh, weight);
            }
            else
            {
                const float solid_coef = 200f;
                acc = solid_coef * math.normalize(-pos);  // accel to origin
            }

            return new Acceleration { Value = acc };
        }
        private float GetAccelAgainstWall(float x, float thresh, float weight)
        {
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
        private bool IsInsideWall(float3 pos)
        {
            if (pos.x < -scale || scale < pos.x) return false;
            if (pos.y < -scale || scale < pos.y) return false;
            if (pos.z < -scale || scale < pos.z) return false;

            return true;
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
