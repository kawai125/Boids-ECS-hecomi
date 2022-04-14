using System;
using System.Diagnostics;
using UnityEngine;

using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

#if ENABLE_ECS_UTILITY
using Unity.Entities;
#endif

namespace HashCellIndex
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
                return new PosIndex { ix = ix, iy = iy, iz = iz } + Lo;
            }
        }

        public bool Contains(PosIndex index)
        {
            if (Lo.ix <= index.ix && index.ix < Hi.ix &&
                Lo.iy <= index.iy && index.iy < Hi.iy &&
                Lo.iz <= index.iz && index.iz < Hi.iz) return true;
            return false;
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
    internal struct MergedBufferInfo
    {
        internal MergedPosIndex localGrid;
        internal PosIndex indexRange;
        internal int n_cell;  // = neighborsOffset

        internal void Clear()
        {
            localGrid = MergedPosIndex.Invalid;
            indexRange = PosIndex.Zero;
            n_cell = 0;
        }
    }

    public unsafe struct MergedNeighborList<T> : IDisposable
        where T : unmanaged
    {
        internal struct BufferRange
        {
            internal int start, length;

            internal int end { get { return start + length; } }

            public override string ToString()
            {
                return $"[start={start}, length={length}]";
            }
        }

        internal PtrHandle<MergedBufferInfo> _info;
        internal NativeList<T> _buffer;
        internal NativeList<BufferRange> _bufferIndex;

        public MergedNeighborList(Allocator alloc)
        {
            _info = new PtrHandle<MergedBufferInfo>(alloc);
            _info.Target->Clear();

            _buffer = new NativeList<T>(64, alloc);
            _bufferIndex = new NativeList<BufferRange>(32, alloc);
        }
        public MergedNeighborList(int indexCapacity, int bufferCapacity, Allocator alloc)
        {
            _info = new PtrHandle<MergedBufferInfo>(alloc);
            _info.Target->Clear();

            _buffer = new NativeList<T>(bufferCapacity, alloc);
            _bufferIndex = new NativeList<BufferRange>(indexCapacity, alloc);
        }


        public void Dispose()
        {
            _info.Dispose();
            _buffer.Dispose();
            _bufferIndex.Dispose();
        }

        public MergedPosIndex MergedGrid { get { return _info.Target->localGrid; } }

        /// <summary>
        /// generate PosIndex from i_cell is not fast. using i_cell directly to access buffers is recommended.
        /// </summary>
        /// <param name="i_cell"></param>
        /// <returns>PosIndex(ix, iy, iz)</returns>
        public PosIndex GetIndex(int i_cell) { return _info.Target->localGrid[i_cell]; }

        public void SetGridInfo(MergedPosIndex localGrid, PosIndex indexRange)
        {
            _info.Target->Clear();

            _info.Target->localGrid = localGrid;
            _info.Target->indexRange = indexRange;

            _buffer.Clear();
            _bufferIndex.Clear();
        }


        public int Length { get { return _info.Target->n_cell; } }

        public NativeArray<T> GetCell(int i_cell)
        {
            CheckIndex(i_cell, Length);
            var range = _bufferIndex[i_cell];
            return _buffer.AsArray().GetSubArray(range.start, range.length);
        }
        public NativeArray<T> GetCell(PosIndex index)
        {
            CheckIndex(index, _info.Target->localGrid);
            int i_cell = GetLocalIndex(index, _info.Target->localGrid);
            return GetCell(i_cell);
        }

        public NativeArray<T> GetNeighbors(int i_cell)
        {
            CheckIndex(i_cell, Length);
            var range = _bufferIndex[i_cell + _info.Target->n_cell];
            return _buffer.AsArray().GetSubArray(range.start, range.length);
        }
        public NativeArray<T> GetNeighbors(PosIndex index)
        {
            CheckIndex(index, _info.Target->localGrid);
            int i_cell = GetLocalIndex(index, _info.Target->localGrid);
            return GetNeighbors(i_cell);
        }

        public MergedCell<T> ExtractMergedCell(Allocator alloc)
        {
            int n_cell = _info.Target->n_cell;
            var mc = new MergedCell<T>(n_cell,
                                       _bufferIndex[_info.Target->n_cell].start,  // not equal but enough capacity.
                                       alloc);
            mc._cells._info.Target->localGrid = _info.Target->localGrid;
            mc._cells._info.Target->n_cell = n_cell;
            for (int i_cell = 0; i_cell < n_cell; i_cell++)
            {
                mc._cells.Add(GetCell(i_cell));
            }
            return mc;
        }

        internal void Clear()
        {
            _info.Target->Clear();
            _buffer.Clear();
            _bufferIndex.Clear();
        }
        internal unsafe void Add(T* ptr, int length)
        {
            int start = _buffer.Length;
            _buffer.AddRange(ptr, length);
            _bufferIndex.Add(new BufferRange { start = start, length = length });
        }
        internal void Add(NativeArray<T> source)
        {
            Add((T*)source.GetUnsafePtr(), source.Length);
        }
        internal void PushBackIndex(int start)
        {
            _bufferIndex.Add(new BufferRange { start = start, length = _buffer.Length - start });
        }
        internal void AddIndex(int start, int length)
        {
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

        public static void CalcCapacity(MergedPosIndex target, PosIndex searchRange, int listCapacityPerCell,
                                        out int n_index, out int buffer_capacity)
        {
            //--- for optimized version (compress neighborlist)
            CalcCapacityForCompressedNeighborList(target, searchRange, listCapacityPerCell, out n_index, out buffer_capacity);

            //--- for naive version
            //CalcCapacityForNaive(target, searchRange, listCapacityPerCell, out n_index, out buffer_capacity);
        }
        private static void CalcCapacityForCompressedNeighborList(in MergedPosIndex target, in PosIndex searchRange, int listCapacityPerCell,
                                                                  out int n_index, out int buffer_capacity)
        {
            int n_cell = target.Length;
            var merge_size = target.Hi - target.Lo;
            float cap = merge_size.iy * merge_size.iz
                      * (float)(merge_size.ix + 2 * searchRange.ix) / (1 + 2 * searchRange.ix);
            n_index = n_cell * 2;
            buffer_capacity = (int)(listCapacityPerCell * cap) + 1;
        }
        private static void CalcCapacityForNaive(in MergedPosIndex target, in PosIndex searchRange, int listCapacityPerCell,
                                                 out int n_index, out int buffer_capacity)
        {
            int n_cell = target.Length;
            n_index = n_cell * 2;
            buffer_capacity = listCapacityPerCell * (n_cell + 1);
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
        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();

            sb.Append($"LocalGrid={_info.Target->localGrid}\n");
            sb.Append($"IndexRange={_info.Target->indexRange}\n");
            sb.Append($", n_cell={_info.Target->n_cell}\n");

            sb.Append($"len buffer={_buffer.Length}\n");
            sb.Append($"len bufferIndex={_bufferIndex.Length}\n");

            sb.Append($"\n  ref to cells:\n");
            for (int i = 0; i < _info.Target->n_cell; i++)
            {
                sb.Append($"  i={i}, range={_bufferIndex[i]}");
                var range = _bufferIndex[i];
                for (int j = range.start; j < range.end; j++) sb.Append($", {_buffer[j]}");
                sb.Append("\n");
            }

            if(_bufferIndex.Length > _info.Target->n_cell)
            {
                sb.Append($"\n  ref to neighborlists:\n");
                for (int i = _info.Target->n_cell; i < _bufferIndex.Length; i++)
                {
                    sb.Append($"  i={i - _info.Target->n_cell}, range={_bufferIndex[i]}");
                    var range = _bufferIndex[i];
                    for (int j = range.start; j < range.end; j++) sb.Append($", {_buffer[j]}");
                    sb.Append("\n");
                }
            }

            return sb.ToString();
        }
    }

    public struct MergedCell<T> : IDisposable
        where T : unmanaged
    {
        internal MergedNeighborList<T> _cells;

        internal unsafe MergedCell(int n_cell, int bufferCapacity, Allocator alloc)
        {
            _cells = new MergedNeighborList<T>(n_cell, bufferCapacity, alloc);
        }

        public int Length { get { return _cells.Length; } }

        /// <summary>
        /// generate PosIndex from i_cell is not fast. using i_cell directly to access buffers is recommended.
        /// </summary>
        /// <param name="i_cell"></param>
        /// <returns>PosIndex(ix, iy, iz)</returns>
        public PosIndex GetIndex(int i_cell) { return _cells.GetIndex(i_cell); }

        public NativeArray<T> GetCell(int i_cell)
        {
            return _cells.GetCell(i_cell);
        }
        public NativeArray<T> GetCell(PosIndex index)
        {
            return _cells.GetCell(index);
        }

        public void Dispose() { _cells.Dispose(); }

        public override string ToString()
        {
            return _cells.ToString();
        }
    }

#if ENABLE_ECS_UTILITY 
    public static class MergedCellUtility
    {
        public static unsafe MergedNeighborList<T> GatherComponentData<T>(ComponentDataFromEntity<T> data_from_entity,
                                                                          MergedNeighborList<Entity> merged_entities,
                                                                          Allocator alloc)
        where T : unmanaged, IComponentData
        {
            var buffer = new MergedNeighborList<T>(merged_entities._bufferIndex.Length,
                                                   merged_entities._buffer.Length,
                                                   alloc);
            GatherComponentDataImpl(data_from_entity, merged_entities, ref buffer);
            return buffer;
        }
        public static void GatherComponentData<T>(ComponentDataFromEntity<T> data_from_entity,
                                                  MergedNeighborList<Entity> merged_entities,
                                                  MergedNeighborList<T> buffer)
        where T : unmanaged, IComponentData
        {
            GatherComponentDataImpl(data_from_entity, merged_entities, ref buffer);
        }
        internal static unsafe void GatherComponentDataImpl<T>(ComponentDataFromEntity<T> data_from_entity,
                                                               MergedNeighborList<Entity> merged_entities,
                                                               ref MergedNeighborList<T> buffer)
        where T : unmanaged, IComponentData
        {
            buffer.Clear();
            buffer._info.Value = merged_entities._info.Value;

            buffer._buffer.ResizeUninitialized(merged_entities._buffer.Length);
            for (int i = 0; i < merged_entities._buffer.Length; i++)
            {
                buffer._buffer[i] = data_from_entity[merged_entities._buffer[i]];
            }

            buffer._bufferIndex.AddRange(merged_entities._bufferIndex.GetUnsafePtr(), merged_entities._bufferIndex.Length);
        }

        public static unsafe MergedCell<T> GatherComponentData<T>(ComponentDataFromEntity<T> data_from_entity,
                                                                  MergedCell<Entity> merged_entities,
                                                                  Allocator alloc)
        where T : unmanaged, IComponentData
        {
            var buffer = new MergedCell<T>(merged_entities._cells._bufferIndex.Length,
                                           merged_entities._cells._buffer.Length,
                                           alloc);
            GatherComponentDataImpl(data_from_entity, merged_entities, ref buffer);
            return buffer;
        }
        public static void GatherComponentData<T>(ComponentDataFromEntity<T> data_from_entity,
                                                  MergedCell<Entity> merged_entities,
                                                  MergedCell<T> buffer)
        where T : unmanaged, IComponentData
        {
            GatherComponentDataImpl(data_from_entity, merged_entities, ref buffer);
        }
        internal static unsafe void GatherComponentDataImpl<T>(ComponentDataFromEntity<T> data_from_entity,
                                                               MergedCell<Entity> merged_entities,
                                                               ref MergedCell<T> buffer)
        where T : unmanaged, IComponentData
        {

            //--- copy info
            buffer._cells._info.Value = merged_entities._cells._info.Value;

            //--- copy data in merged cell
            int len_data = merged_entities._cells._buffer.Length;
            int len_index = merged_entities._cells._bufferIndex.Length;

            buffer._cells._buffer.ResizeUninitialized(len_data);
            for (int i = 0; i < len_data; i++)
            {
                buffer._cells._buffer[i] = data_from_entity[merged_entities._cells._buffer[i]];
            }

            buffer._cells._bufferIndex.Clear();
            buffer._cells._bufferIndex.AddRange(merged_entities._cells._bufferIndex.GetUnsafePtr(), len_index);
        }
    }
#endif
}