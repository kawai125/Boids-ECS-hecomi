using System;
using System.Diagnostics;
using UnityEngine;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

#if ENABLE_ECS_UTILITY
using Unity.Entities;
#endif

namespace Domain
{

    public struct MergedPosIndex
    {
        public PosIndex Lo { get; internal set; }
        public PosIndex Hi { get; internal set; }

        public int Length
        {
            get
            {
                var area = Hi - Lo;
                return area.ix * area.iy * area.iz;
            }
        }

        public PosIndex this[int i_cell]
        {
            get
            {
                var area = Hi - Lo;
                int y_stride = area.ix;
                int z_stride = area.iy * y_stride;

                int iz = i_cell / z_stride;
                int iy = (i_cell - z_stride * iz) / y_stride;
                int ix = (i_cell - z_stride * iz - y_stride * iy);
                return new PosIndex { ix = ix, iy = iy, iz = iz };
            }
        }

        public override string ToString()
        {
            return $"[{Lo}~{Hi})";
        }

        internal static MergedPosIndex Invalid
        {
            get
            {
                return new MergedPosIndex { Lo = PosIndex.Zero, Hi = PosIndex.Zero };
            }
        }
    }
    public struct MergedBuffer<Tvalue> : IDisposable
        where Tvalue : unmanaged
    {
        internal struct BufferRange
        {
            internal int start, length;
        }

        internal PtrHandle<MergedPosIndex> _localGrid;
        internal NativeList<Tvalue> _buffer;
        internal NativeList<BufferRange> _bufferIndex;

        public MergedBuffer(Allocator alloc)
        {
            _localGrid = new PtrHandle<MergedPosIndex>(alloc);
            _localGrid.Value = MergedPosIndex.Invalid;

            _buffer = new NativeList<Tvalue>(64, alloc);
            _bufferIndex = new NativeList<BufferRange>(8, alloc);
        }
        public MergedBuffer(int n_cell, int initialCapacity, Allocator alloc)
        {
            _localGrid = new PtrHandle<MergedPosIndex>(alloc);
            _localGrid.Value = MergedPosIndex.Invalid;

            _buffer = new NativeList<Tvalue>(initialCapacity, alloc);
            _bufferIndex = new NativeList<BufferRange>(n_cell, alloc);
        }


        public void Dispose()
        {
            _localGrid.Dispose();
            _buffer.Dispose();
            _bufferIndex.Dispose();
        }

        public int Length { get { return _bufferIndex.Length; } }

        public NativeArray<Tvalue> this[int i_cell]
        {
            get
            {
                CheckIndex(i_cell, Length);
                var range = _bufferIndex[i_cell];
                return _buffer.AsArray().GetSubArray(range.start, range.length);
            }
        }
        public NativeArray<Tvalue> this[PosIndex index]
        {
            get
            {
                CheckIndex(index, _localGrid);

                int i_buffer = GetLocalIndex(index, _localGrid);
                return this[i_buffer];
            }
        }

        internal void Clear()
        {
            _buffer.Clear();
            _bufferIndex.Clear();
        }
        internal unsafe void Add(Tvalue* ptr, int length)
        {
            int start = _buffer.Length;
            _buffer.AddRange(ptr, length);
            _bufferIndex.Add(new BufferRange { start = start, length = length });
        }

        private static int GetLocalIndex(PosIndex index, MergedPosIndex area)
        {
            int x_stride = area.Hi.ix - area.Lo.ix;
            int y_stride = (area.Hi.iy - area.Lo.iy) * x_stride;
            return index.ix - area.Lo.ix
                + (index.iy - area.Lo.iy) * x_stride
                + (index.iz - area.Lo.iz) * y_stride;
        }
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckIndex(int index, int length)
        {
            if (index < 0 || length <= index)
                throw new IndexOutOfRangeException($"index={index} is out of range. range=[0,{length - 1}]");
        }
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckIndex(PosIndex index, MergedPosIndex area)
        {
            if (index.ix < area.Lo.ix || area.Hi.ix <= index.ix ||
                index.iy < area.Lo.iy || area.Hi.iy <= index.iy ||
                index.iz < area.Lo.iz || area.Hi.iz <= index.iz)
                throw new IndexOutOfRangeException($"index={index} is out of local grid. Lo={area.Lo}, Hi={area.Hi}");
        }
    }

#if ENABLE_ECS_UTILITY 
    public static class MergedBufferUtility
    {
        public static unsafe MergedBuffer<T> GatherComponentData<T>(ComponentDataFromEntity<T> data_from_entity,
                                                                    MergedBuffer<Entity> merged_entities,
                                                                    Allocator alloc)
        where T : unmanaged, IComponentData
        {
            var buffer = new MergedBuffer<T>(merged_entities._bufferIndex.Length,
                                             merged_entities._buffer.Length,
                                             alloc);
            buffer._buffer.ResizeUninitialized(merged_entities._buffer.Length);
            buffer._bufferIndex.Clear();
            buffer._bufferIndex.AddRange(merged_entities._bufferIndex.GetUnsafePtr(),
                                         merged_entities._bufferIndex.Length);

            for(int i = 0; i < merged_entities._buffer.Length; i++)
            {
                buffer._buffer[i] = data_from_entity[merged_entities._buffer[i]];
            }

            return buffer;
        }
    }
#endif
}