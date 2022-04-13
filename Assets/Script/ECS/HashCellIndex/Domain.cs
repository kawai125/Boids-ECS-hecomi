using System;
using System.Diagnostics;
using UnityEngine;

using Unity.Mathematics;

namespace HashCellIndex
{
    [Flags]
    public enum BoundaryCondition
    {
        Open = 0x00,

        Periodic_X = 0x01,
        Periodic_Y = 0x02,
        Periodic_Z = 0x04,
    }
    public static class Boundary
    {
        public const BoundaryCondition Open = BoundaryCondition.Open;

        public const BoundaryCondition Periodic_X = BoundaryCondition.Periodic_X;
        public const BoundaryCondition Periodic_Y = BoundaryCondition.Periodic_Y;
        public const BoundaryCondition Periodic_Z = BoundaryCondition.Periodic_Z;

        public const BoundaryCondition Periodic_XY = BoundaryCondition.Periodic_X | BoundaryCondition.Periodic_Y;
        public const BoundaryCondition Periodic_YZ = BoundaryCondition.Periodic_Y | BoundaryCondition.Periodic_Z;
        public const BoundaryCondition Periodic_ZX = BoundaryCondition.Periodic_Z | BoundaryCondition.Periodic_X;

        public const BoundaryCondition Periodic_XYZ = BoundaryCondition.Periodic_X |
                                                      BoundaryCondition.Periodic_Y |
                                                      BoundaryCondition.Periodic_Z;

        public static bool IsMatch(this BoundaryCondition condition, BoundaryCondition target)
        {
            if ((condition & target) == target) return true;
            return false;
        }
    }

    public readonly struct Box
    {
        public readonly float3 Lo { get; }
        public readonly float3 Hi { get; }
        public readonly float3 Center { get; }
        public readonly float3 Size { get; }
        public readonly float3 SizeInv { get; }

        public Box(float3 lo, float3 hi)
        {
            if (lo.x >= hi.x || lo.y >= hi.y || lo.z >= hi.z)
                throw new ArgumentException($"the upper bounds must be lager than lower bounds. lo = {lo}, hi = {hi}");

            Lo = lo;
            Hi = hi;

            Center = 0.5f * (hi - lo) + lo;
            Size = hi - lo;
            SizeInv = new float3(1) / Size;
        }

        public float Volume { get { return Size.x * Size.y * Size.z; } }
        public float VolumeInv { get { return SizeInv.x * SizeInv.y * SizeInv.z; } }

        public bool IsInside(float3 pos)
        {
            return (Lo.x <= pos.x) && (pos.x < Hi.x) &&
                   (Lo.y <= pos.y) && (pos.y < Hi.y) &&
                   (Lo.z <= pos.z) && (pos.z < Hi.z);
        }
        /// <summary>
        /// adjust position into the box
        /// </summary>
        /// <returns>adjust into [box.Lo,box.Hi)</returns>
        public float3 ApplyPeriadicBoundary(float3 pos)
        {
            CheckPosRangeForPeriodicBoundary(pos, Lo - Size, Hi + Size, Boundary.Periodic_XYZ);

            var ret = pos;
            if (ret.x < Lo.x) ret.x += Size.x;
            if (ret.y < Lo.y) ret.y += Size.y;
            if (ret.z < Lo.z) ret.z += Size.z;
            if (ret.x >= Hi.x) ret.x -= Size.x;
            if (ret.y >= Hi.y) ret.y -= Size.y;
            if (ret.z >= Hi.z) ret.z -= Size.z;
            return ret;
        }
        /// <summary>
        /// adjust position into the box by given boundary
        /// </summary>
        /// <returns>adjust into [box.Lo,box.Hi)</returns>
        public float3 ApplyPeriadicBoundary(float3 pos, BoundaryCondition boundary)
        {
            CheckPosRangeForPeriodicBoundary(pos, Lo - Size, Hi + Size, boundary);

            var ret = pos;
            if (boundary.IsMatch(BoundaryCondition.Periodic_X))
            {
                if (ret.x < Lo.x) ret.x += Size.x;
                if (ret.x >= Hi.x) ret.x -= Size.x;
            }
            if (boundary.IsMatch(BoundaryCondition.Periodic_Y))
            {
                if (ret.y < Lo.y) ret.y += Size.y;
                if (ret.y >= Hi.y) ret.y -= Size.y;
            }
            if (boundary.IsMatch(BoundaryCondition.Periodic_Z))
            {
                if (ret.z < Lo.z) ret.z += Size.z;
                if (ret.z >= Hi.z) ret.z -= Size.z;
            }
            return ret;
        }
        /// <summary>
        /// adjust relative displacement into the box by given boundary.
        /// </summary>
        /// <returns>adjust into [-0.5,0.5) of box.Size</returns>
        public float3 GetDisplacement(float3 displacement, BoundaryCondition boundary)
        {
            CheckPosRangeForPeriodicBoundary(displacement, Center - Size, Center + Size, boundary);

            var ret = displacement;
            var range = Center - Lo;
            if (boundary.IsMatch(BoundaryCondition.Periodic_X))
            {
                if (ret.x < -range.x) ret.x += Size.x;
                if (ret.x >= range.x) ret.x -= Size.x;
            }
            if (boundary.IsMatch(BoundaryCondition.Periodic_Y))
            {
                if (ret.y < -range.y) ret.y += Size.y;
                if (ret.y >= range.y) ret.y -= Size.y;
            }
            if (boundary.IsMatch(BoundaryCondition.Periodic_Z))
            {
                if (ret.z < -range.z) ret.z += Size.z;
                if (ret.z >= range.z) ret.z -= Size.z;
            }
            return ret;
        }
        public static Box Unit { get { return new Box(0f, 1f); } }

        public override string ToString()
        {
            return $"(Lo({Lo.x}, {Lo.y}, {Lo.z}) - Hi({Hi.x}, {Hi.y}, {Hi.z}))";
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckPosRangeForPeriodicBoundary(float3 pos, float3 lo_limit, float3 hi_limit, BoundaryCondition boundary)
        {
            bool is_valid = true;
            if (boundary.IsMatch(BoundaryCondition.Periodic_X))
            {
                if (pos.x < lo_limit.x ||
                    pos.x >= hi_limit.x) is_valid = false;
            }
            if (boundary.IsMatch(BoundaryCondition.Periodic_Y))
            {
                if (pos.y < lo_limit.y ||
                    pos.y >= hi_limit.y) is_valid = false;
            }
            if (boundary.IsMatch(BoundaryCondition.Periodic_Z))
            {
                if (pos.z < lo_limit.z ||
                    pos.z >= hi_limit.z) is_valid = false;
            }
            if (!is_valid)
                throw new ArgumentOutOfRangeException($"pos={pos} was out of range for convertion.");
        }
    }

    public struct PosIndex : IEquatable<PosIndex>
    {
        public int3 index;

        public int ix { get { return index.x; } set { index.x = value; } }
        public int iy { get { return index.y; } set { index.y = value; } }
        public int iz { get { return index.z; } set { index.z = value; } }

        public PosIndex(int ix, int iy, int iz)
        {
            index.x = ix;
            index.y = iy;
            index.z = iz;
        }
        public PosIndex(int3 new_index)
        {
            index = new_index;
        }

        public float3 GetCenterPos(Box box, PosIndex gridSize)
        {
            float3 size = box.Size;
            float3 local_center = new float3
            {
                x = (size.x * ix) / gridSize.ix,
                y = (size.y * iy) / gridSize.iy,
                z = (size.z * iz) / gridSize.iz,
            };
            return local_center + box.Lo;
        }

        public bool Equals(PosIndex index)
        {
            return (ix == index.ix) && (iy == index.iy) && (iz == index.iz);
        }
        public override int GetHashCode()
        {
            int hash = ix.GetHashCode();
            hash = HashUtility.Combine(hash, iy.GetHashCode());
            hash = HashUtility.Combine(hash, iz.GetHashCode());
            return hash;
        }

        public static PosIndex operator + (PosIndex lhs, PosIndex rhs)
        {
            return new PosIndex(lhs.ix + rhs.ix, lhs.iy + rhs.iy, lhs.iz + rhs.iz);
        }
        public static PosIndex operator - (PosIndex lhs, PosIndex rhs)
        {
            return new PosIndex(lhs.ix - rhs.ix, lhs.iy - rhs.iy, lhs.iz - rhs.iz);
        }
        public static PosIndex operator * (PosIndex lhs, int n)
        {
            return new PosIndex(lhs.ix * n, lhs.iy * n, lhs.iz * n);
        }
        public static PosIndex operator * (int n, PosIndex rhs)
        {
            return new PosIndex(rhs.ix * n, rhs.iy * n, rhs.iz * n);
        }
        public static PosIndex Zero { get { return new PosIndex(0, 0, 0); } }
        public override string ToString()
        {
            return $"[{ix}, {iy}, {iz}]";
        }
    }
}