using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

[UpdateAfter(typeof(BoidSystemGroup))]
public partial class VortexSystem : SystemBase
{
    private const float vortex_v_period = 4f / 5f * Mathf.PI;  // 450 degree
    private const float vortex_h_period = 5f / 8f * Mathf.PI;  // 225 degree
    private const float center_mask = 0.1f;

    public const float vortexVertical_C0 = 0.6f;
    public const float vortexVertical_C1 = -0.4f;  // grad = C1 / wall_distance
    public const float vortexHorizontal = 0.05f;

    //private readonly float3 axis = math.normalize(new float3 { x = 1, y = 3, z = 2 });
    private readonly float3 axis = math.normalize(new float3 { x = 0, y = 0, z = 1 });
    private readonly float3 center = float3.zero;

    protected override void OnUpdate()
    {
        quaternion quate = Quaternion.LookRotation(axis);

        float dist_wall = Bootstrap.WallScale * 0.5f;
        float dist_wall_inv = 1f / dist_wall;

        float vortex_v_c0 =  vortexVertical_C0;
        float vortex_v_grad = vortexVertical_C1 * dist_wall_inv;

        float vortex_h = vortexHorizontal;

        float intensity = BoidParams_Bootstrap.Param.vortexIntensity;

        float3 center_pos = center;

        Dependency = Entities.
            WithAll<BoidType>().
            WithBurst().
            ForEach(
            (Entity entity, ref Acceleration accel, in Translation position) =>
            {
                float3 vec_origin = position.Value - center_pos;
                //float3 vec = RotateVec(quate, vec_origin);
                float3 vec = vec_origin;

                float2 vec_xz = new float2(vec.x, vec.z);
                float r_2d = math.length(vec_xz);

                if (r_2d <= center_mask)
                {
                    float3 acc_c = accel.Value;
                    acc_c.z += vortex_v_c0;
                    accel.Value = acc_c;
                    return;
                }

                float2 side_unit = math.normalize(new float2(-vec_xz.y, vec_xz.x));  // rotate 90 degree in xz plane

                //--- vertical part
                float acc_v = (vortex_v_c0 + vortex_v_grad * r_2d) * math.cos(vortex_v_period * r_2d * dist_wall_inv);

                //--- horizontal part
                float2 acc_h = (vortex_h * (1f - math.cos(vortex_h_period * r_2d * dist_wall_inv))) * side_unit;

                float3 acc_rotated = new float3 { x = acc_h.x, y = acc_v, z = acc_h.y };

                //quaternion q_inv = quate;
                //q_inv.value.w = -quate.value.w;
                //float3 acc = RotateVec(q_inv, acc_rotated);
                float3 acc = acc_rotated * intensity;
                
                accel.Value += acc;
            }).ScheduleParallel(Dependency);
    }
    private static float3 RotateVec(quaternion q, float3 vec)
    {
        float4 q_sq = math.dot(q, q);
        float q_01 = q.value.w * q.value.x;
        float q_02 = q.value.w * q.value.y;
        float q_03 = q.value.w * q.value.z;
        float q_12 = q.value.x * q.value.y;
        float q_13 = q.value.x * q.value.z;
        float q_23 = q.value.y * q.value.z;

        var new_vec = new float3
        {
            x = vec.x * (q_sq.w + q_sq.x - q_sq.y - q_sq.z)
              + vec.y * 2f * (q_12 - q_03)
              + vec.z * 2f * (q_13 + q_02),
            y = vec.x * 2f * (q_12 + q_03)
              + vec.y * (q_sq.w - q_sq.x + q_sq.y - q_sq.z)
              + vec.z * 2f * (q_23 - q_01),
            z = vec.x * 2f * (q_13 - q_02)
              + vec.y * 2f * (q_23 + q_01)
              * vec.z * (q_sq.w - q_sq.x - q_sq.y + q_sq.z),
        };
        return new_vec;
    }
}
