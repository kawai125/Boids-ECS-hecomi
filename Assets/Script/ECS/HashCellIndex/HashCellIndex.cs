using System;
using System.Diagnostics;
using UnityEngine;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace HashCellIndex
{
    public struct HashCellIndex<T> : IDisposable
        where T : unmanaged
    {
        internal PtrHandle<CellIndexInfo> _info;
        internal NativeMultiHashMap<PosIndex, T> _map;

        public unsafe HashCellIndex(Allocator alloc)
        {
            _info = new PtrHandle<CellIndexInfo>(alloc);
            _info.Target->alloc = alloc;

            _map = new NativeMultiHashMap<PosIndex, T>(64, alloc);
        }
        public void Dispose()
        {
            _info.Dispose();
            _map.Dispose();
        }
        public void Clear()
        {
            _map.Clear();
        }
        public int Capacity
        { 
            get { return _map.Capacity; }
            set 
            {
                //_map.Capacity = value;  //  Exception: Shrinking a hash map is not supported (Collections 0.15.0)
                if(_map.Capacity < value) _map.Capacity = value;
            }
        }
        /// <summary>
        /// Initialize using the average density per cell for evenly distributed targets. 
        /// </summary>
        public unsafe bool InitDomainWithDensity(Box box, int n_total, float n_in_cell = 8f)
        {
            Clear();
            Capacity = n_total;

            return _info.Target->InitDomain(box, n_total, n_in_cell);
        }
        /// <summary>
        /// Initialize using the search radius for targets with a skewed distribution.
        /// </summary>
        public unsafe bool InitDomainWithRange(Box box, int n_total, float r_search)
        {
            Clear();
            Capacity = n_total;

            return _info.Target->InitDomain(box, r_search);
        }
        public unsafe Box Box { get { return _info.Target->Box; } }
        
        public unsafe bool TryAdd(float3 pos, T value, BoundaryCondition boundary)
        {
            return TryAdd(_info.Target->Box.ApplyPeriadicBoundary(pos, boundary), value);
        }
        public unsafe bool TryAdd(float3 pos, T value)
        {
            if (!_info.Target->Box.IsInside(pos)) return false;

            _map.Add(_info.Target->GetIndexImpl(pos), value);

            return true;
        }
        public unsafe void Add(float3 pos, T value, BoundaryCondition boundary)
        {
            Add(_info.Target->Box.ApplyPeriadicBoundary(pos, boundary), value);
        }
        public unsafe void Add(float3 pos, T value)
        {
            _map.Add(_info.Target->GetIndex(pos), value);
        }
        public unsafe PosIndex GetIndex(float3 pos)
        {
            return _info.Target->GetIndex(pos);
        }

        //------------------
        // accessor for cells
        //------------------
        public unsafe void GetNeighborList(float3 pos, float r_search, BoundaryCondition boundary,
                                           NativeList<T> list)
        {
            list.Clear();
            _info.Target->GetNeighborListImpl(GetIndex(pos), r_search, boundary, _map, ref list);
        }
        public unsafe NativeList<T> GetNeighborList(float3 pos, float r_search, BoundaryCondition boundary,
                                                    int initialCapacity, Allocator alloc)
        {
            var list = new NativeList<T>(initialCapacity, alloc);
            _info.Target->GetNeighborListImpl(GetIndex(pos), r_search, boundary, _map, ref list);
            return list;
        }
        public unsafe void GetNeighborList(PosIndex origin, float r_search, BoundaryCondition boundary,
                                           NativeList<T> list)
        {
            list.Clear();
            _info.Target->GetNeighborListImpl(origin, r_search, boundary, _map, ref list);
        }
        public unsafe NativeList<T> GetNeighborList(PosIndex origin, float r_search, BoundaryCondition boundary,
                                                    int initialCapacity, Allocator alloc)
        {
            var list = new NativeList<T>(initialCapacity, alloc);
            _info.Target->GetNeighborListImpl(origin, r_search, boundary, _map, ref list);
            return list;
        }

        public unsafe PosIndex GridSize { get { return _info.Target->GridSize; } }
        public void GetGridIndexList(NativeList<PosIndex> index_list)
        {
            index_list.Clear();
            CellIndexInfo.GetGridIndexListImpl(GridSize, index_list);
        }
        public NativeList<PosIndex> GetGridIndexList(int initialCapacity, Allocator alloc)
        {
            var index_list = new NativeList<PosIndex>(initialCapacity, alloc);
            CellIndexInfo.GetGridIndexListImpl(GridSize, index_list);
            return index_list;
        }
        public void GetContainsIndexList(NativeList<PosIndex> index_list)
        {
            index_list.Clear();
            CellIndexInfo.GetContainsIndexListImpl(GridSize, index_list, _map);
        }
        public NativeList<PosIndex> GetContainsIndexList(int initialCapacity, Allocator alloc)
        {
            var index_list = new NativeList<PosIndex>(initialCapacity, alloc);
            CellIndexInfo.GetContainsIndexListImpl(GridSize, index_list, _map);
            return index_list;
        }
        public void GetValuesInCell(PosIndex index, NativeList<T> list)
        {
            list.Clear();
            CellIndexInfo.GetMappedData(index, _map, ref list);
        }
        public NativeList<T> GetValuesInCell(PosIndex index, int initialCapacity, Allocator alloc)
        {
            var list = new NativeList<T>(initialCapacity, alloc);
            CellIndexInfo.GetMappedData(index, _map, ref list);
            return list;
        }
        public unsafe void GetSearchIndexList(PosIndex index, float r_search, BoundaryCondition boundary,
                                              NativeList<PosIndex> search_target_list)
        {
            search_target_list.Clear();
            _info.Target->GetSearchIndexListImpl(index, r_search, boundary, ref search_target_list);
        }
        public unsafe NativeList<PosIndex> GetSearchIndexList(PosIndex index, float r_search, BoundaryCondition boundary,
                                                              int initialCapacity, Allocator alloc)
        {
            var search_target_list = new NativeList<PosIndex>(initialCapacity, alloc);
            _info.Target->GetSearchIndexListImpl(index, r_search, boundary, ref search_target_list);
            return search_target_list;
        }

        //------------------
        // for merged cell
        //------------------
        public unsafe void GetContainsMergedCellList(int n_merged, NativeList<MergedPosIndex> cell_list)
        {
            if (n_merged <= 0) throw new ArgumentOutOfRangeException();
            cell_list.Clear();
            CellIndexInfo.GetContainsMergedCellListImpl(GridSize, n_merged, cell_list, _map);
        }
        public unsafe NativeList<MergedPosIndex> GetContainsMergedCellList(int n_merged, Allocator alloc)
        {
            if (n_merged <= 0) throw new ArgumentOutOfRangeException();
            var cell_list = new NativeList<MergedPosIndex>(16, alloc);
            CellIndexInfo.GetContainsMergedCellListImpl(GridSize, n_merged, cell_list, _map);
            return cell_list;
        }
        public unsafe void GetMergedNeighborList(MergedPosIndex target, float r_search, BoundaryCondition boundary,
                                                 MergedNeighborList<T> buffer)
        {
            CellIndexInfo.GetMergedNeighborListImpl(target, _info.Target->GetIndexRange(r_search), boundary, _map, ref buffer);
        }
        public unsafe MergedNeighborList<T> GetMergedNeighborList(MergedPosIndex target, float r_search, BoundaryCondition boundary,
                                                                  int capacityPerCell, Allocator alloc)
        {
            int n_cell = target.Length;
            var cache_size = new MergedPosIndex { Lo = target.Lo, Hi = target.Hi + new PosIndex(2, 2, 2) };
            int n_index = cache_size.Length + n_cell * 2;
            var buffer = new MergedNeighborList<T>(n_index, capacityPerCell * (n_cell + 2), alloc);
            CellIndexInfo.GetMergedNeighborListImpl(target, _info.Target->GetIndexRange(r_search), boundary, _map, ref buffer);
            return buffer;
        }
        public void GetValuesInMergedCell(MergedPosIndex target, MergedCell<T> buffer)
        {
            CellIndexInfo.GetMappedData(target, _map, ref buffer._cells);
        }
        public MergedCell<T> GetValuesInMergedCell(MergedPosIndex target,
                                                     int capacityForBuffer, Allocator alloc)
        {
            int n_cell = target.Length;
            var buffer = new MergedCell<T>(n_cell, capacityForBuffer * n_cell, alloc);
            CellIndexInfo.GetMappedData(target, _map, ref buffer._cells);
            return buffer;
        }
        public void GetValuesInMergedCell(float3 Lo, float3 Hi, MergedCell<T> buffer)
        {
            var merged_cell = new MergedPosIndex { Lo = GetIndex(Lo), Hi = GetIndex(Hi) };
            CellIndexInfo.GetMappedData(merged_cell, _map, ref buffer._cells);
        }
        public MergedCell<T> GetValuesInMergedCell(float3 Lo, float3 Hi,
                                                     int capacityForBuffer, Allocator alloc)
        {
            var merged_cell = new MergedPosIndex { Lo = GetIndex(Lo), Hi = GetIndex(Hi) };

            int n_cell = merged_cell.Length;
            var buffer = new MergedCell<T>(n_cell, capacityForBuffer * n_cell, alloc);
            CellIndexInfo.GetMappedData(merged_cell, _map, ref buffer._cells);
            return buffer;
        }

        //------------------
        // parallel writer
        //------------------
        public unsafe ParallelWriter AsParallelWriter()
        {
            return new ParallelWriter() { _info = _info.Target,
                                          _parallelWriter = _map.AsParallelWriter() };
        }

        public unsafe struct ParallelWriter
        {
            [NativeDisableUnsafePtrRestriction]
            internal CellIndexInfo* _info;
            internal NativeMultiHashMap<PosIndex, T>.ParallelWriter _parallelWriter;

            public Box Box { get { return _info->Box; } }

            public int Capacity { get { return _parallelWriter.Capacity; } }
            public int m_ThreadIndex { get { return _parallelWriter.m_ThreadIndex; } }

            public bool TryAdd(float3 pos, T value)
            {
                if (!_info->Box.IsInside(pos)) return false;

                _parallelWriter.Add(_info->GetIndexImpl(pos), value);
                return true;
            }
        }

        internal struct CellIndexInfo
        {
            internal PosIndex GridSize;
            internal Box Box;
            internal float3 CellSize;
            internal float3 CellSizeInv;
            internal float RSearchMax;
            internal float RSearchMargin;

            internal Allocator alloc;

            public bool InitDomain(Box box, int n_total, float n_in_cell)
            {
                return InitDomain(box, ComputeGridSize(box, n_total, n_in_cell));
            }
            public bool InitDomain(Box box, float r_search)
            {
                if (r_search <= 0)
                    throw new ArgumentOutOfRangeException($"r_search={r_search}, must be > 0.");

                var grid_size = new PosIndex(math.max((int)(box.Size.x / r_search) + 1, 1),
                                             math.max((int)(box.Size.y / r_search) + 1, 1),
                                             math.max((int)(box.Size.z / r_search) + 1, 1));
                return InitDomain(box, grid_size);
            }
            public bool InitDomain(Box box, PosIndex index_size)
            {
                if (index_size.ix <= 0 || index_size.iy <= 0 || index_size.iz <= 0)
                    return false;
                    //throw new ArgumentException($"index size must be > 0. index size = {index_size}");

                GridSize = index_size;
                Box = box;

                var box_size = box.Size;
                CellSize = new float3(box_size.x / GridSize.ix,
                                      box_size.y / GridSize.iy,
                                      box_size.z / GridSize.iz);
                CellSizeInv = new float3(1) / CellSize;

                RSearchMax = math.length(Box.Size) * 1.01f;  // all cells in range if r > RSearchMax.
                RSearchMargin = math.length(CellSize);

                return true;
            }
            public PosIndex GetIndex(float3 pos)
            {
                CheckPos(Box, pos);
                return GetIndexImpl(pos);
            }
            internal PosIndex GetIndexImpl(float3 pos)
            {
                var pos_local = pos - Box.Lo;
                return new PosIndex((int)(pos_local.x * CellSizeInv.x),
                                    (int)(pos_local.y * CellSizeInv.y),
                                    (int)(pos_local.z * CellSizeInv.z));
            }
            public PosIndex GetIndexRange(float r_search)
            {
                return new PosIndex((int)(math.min(r_search, RSearchMax) * CellSizeInv.x) + 1,
                                    (int)(math.min(r_search, RSearchMax) * CellSizeInv.y) + 1,
                                    (int)(math.min(r_search, RSearchMax) * CellSizeInv.z) + 1);
            }
            public void GetNeighborListImpl(PosIndex origin, float r_search, BoundaryCondition boundary,
                                            NativeMultiHashMap<PosIndex, T> map,
                                            ref NativeList<T> list)
            {
                var i_range = GetIndexRange(r_search);

                var i_lo = origin - i_range;
                var i_hi = origin + i_range;

                //UnityEngine.Debug.Log($"origin={origin}, r_Search={r_search}, range: {i_lo}, {i_hi}");

                for (int iz=i_lo.iz; iz<=i_hi.iz; iz++)
                {
                    int iz_cell = iz;
                    if (IsSkipIndex(ref iz_cell, GridSize.iz, boundary.IsMatch(Boundary.Periodic_Z), i_lo.iz, i_hi.iz)) continue;

                    for (int iy=i_lo.iy; iy<=i_hi.iy; iy++)
                    {
                        int iy_cell = iy;
                        if (IsSkipIndex(ref iy_cell, GridSize.iy, boundary.IsMatch(Boundary.Periodic_Y), i_lo.iy, i_hi.iy)) continue;

                        for (int ix=i_lo.ix; ix<=i_hi.ix; ix++)
                        {
                            int ix_cell = ix;
                            if (IsSkipIndex(ref ix_cell, GridSize.ix, boundary.IsMatch(Boundary.Periodic_X), i_lo.ix, i_hi.ix)) continue;

                            var index = new PosIndex(ix_cell, iy_cell, iz_cell);
                            GetMappedData(index, map, ref list);

                            //UnityEngine.Debug.Log($"  --> load from cell={index}");
                        }
                    }
                }
            }
            public void GetSearchIndexListImpl(PosIndex origin, float r_search, BoundaryCondition boundary,
                                               ref NativeList<PosIndex> search_target_list)
            {
                var i_range = GetIndexRange(r_search);

                var i_lo = origin - i_range;
                var i_hi = origin + i_range;

                for (int iz = i_lo.iz; iz <= i_hi.iz; iz++)
                {
                    int iz_cell = iz;
                    if (IsSkipIndex(ref iz_cell, GridSize.iz, boundary.IsMatch(Boundary.Periodic_Z), i_lo.iz, i_hi.iz)) continue;

                    for (int iy = i_lo.iy; iy <= i_hi.iy; iy++)
                    {
                        int iy_cell = iy;
                        if (IsSkipIndex(ref iy_cell, GridSize.iy, boundary.IsMatch(Boundary.Periodic_Y), i_lo.iy, i_hi.iy)) continue;

                        for (int ix = i_lo.ix; ix <= i_hi.ix; ix++)
                        {
                            int ix_cell = ix;
                            if (IsSkipIndex(ref ix_cell, GridSize.ix, boundary.IsMatch(Boundary.Periodic_X), i_lo.ix, i_hi.ix)) continue;

                            var index = new PosIndex(ix_cell, iy_cell, iz_cell);
                            search_target_list.Add(index);
                        }
                    }
                }
            }
            public static unsafe void GetMergedNeighborListImpl(MergedPosIndex target, PosIndex index_range, BoundaryCondition boundary,
                                                                NativeMultiHashMap<PosIndex, T> map,
                                                                ref MergedNeighborList<T> buffer)
            {
                buffer.Clear();
                var Lo = target.Lo - index_range;
                var Hi = target.Hi + index_range;

                //--- cache cell data
                buffer.SetGridInfo(target, Hi - Lo, index_range);
                int i_cache = 0;
                for(int iz = Lo.iz; iz < Hi.iz; iz++)
                {
                    for(int iy = Lo.iy; iy < Hi.iy; iy++)
                    {
                        for(int ix = Lo.ix; ix < Hi.ix; ix++)
                        {
                            var index_cell = new PosIndex(ix, iy, iz);

                            //--- read data into cache
                            int start = buffer._buffer.Length;
                            GetMappedData(index_cell, map, ref buffer._buffer);

                            var range = new MergedNeighborList<T>.BufferRange
                            {
                                start = start,
                                length = buffer._buffer.Length - start
                            };
                            buffer._bufferIndex[i_cache] = range;

                            //--- record cells in merged area
                            if (target.Contains(index_cell))
                            {
                                buffer._bufferIndex.Add(range);
                            }

                            i_cache++;
                        }
                    }
                }

                buffer.BuildNeighborListFromCache();
            }
            internal static void GetGridIndexListImpl(PosIndex gridSize,
                                                      NativeList<PosIndex> list)
            {
                for(int iz=0; iz < gridSize.iz; iz++)
                {
                    for(int iy=0; iy < gridSize.iy; iy++)
                    {
                        for(int ix=0; ix < gridSize.ix; ix++)
                        {
                            list.Add(new PosIndex(ix, iy, iz));
                        }
                    }
                }
            }
            internal static void GetContainsIndexListImpl(PosIndex gridSize,
                                                          NativeList<PosIndex> list,
                                                          NativeMultiHashMap<PosIndex, T> map)
            {
                for (int iz = 0; iz < gridSize.iz; iz++)
                {
                    for (int iy = 0; iy < gridSize.iy; iy++)
                    {
                        for (int ix = 0; ix < gridSize.ix; ix++)
                        {
                            var index = new PosIndex(ix, iy, iz);
                            if (map.ContainsKey(index)) list.Add(new PosIndex(ix, iy, iz));
                        }
                    }
                }
            }
            internal static void GetContainsMergedCellListImpl(PosIndex gridSize, int n_merge,
                                                               NativeList<MergedPosIndex> list,
                                                               NativeMultiHashMap<PosIndex, T> map)
            {
                for (int iz = 0; iz < gridSize.iz; iz += n_merge)
                {
                    for (int iy = 0; iy < gridSize.iy; iy += n_merge)
                    {
                        for (int ix = 0; ix < gridSize.ix; ix += n_merge)
                        {
                            var Lo = new PosIndex(ix, iy, iz);
                            var Hi = new PosIndex(math.min(ix + n_merge, gridSize.ix),
                                                  math.min(iy + n_merge, gridSize.iy),
                                                  math.min(iz + n_merge, gridSize.iz));
                            if (ContainsInMergedCell(Lo, Hi, map))
                            {
                                list.Add(new MergedPosIndex { Lo = Lo, Hi = Hi });
                            }
                        }
                    }
                }
            }
            private static bool ContainsInMergedCell(PosIndex Lo, PosIndex Hi, NativeMultiHashMap<PosIndex, T> map)
            {
                for (int i_mz = Lo.iz; i_mz < Hi.iz; i_mz++)
                {
                    for (int i_my = Lo.iy; i_my < Hi.iy; i_my++)
                    {
                        for (int i_mx = Lo.ix; i_mx < Hi.ix; i_mx++)
                        {
                            var index = new PosIndex(i_mx, i_my, i_mz);
                            if (map.ContainsKey(index)) return true;
                        }
                    }
                }
                return false;
            }
            private static bool IsSkipIndex(ref int index, int grid_size, bool is_periodic, int i_range_lo, int i_range_hi)
            {
                if(index < 0)
                {
                    if (!is_periodic) return true;

                    index += grid_size;
                    if (i_range_hi < index && index < grid_size)
                    {
                        return false;
                    }
                    else return true;
                }
                else if(index >= grid_size)
                {
                    if (!is_periodic) return true;

                    index -= grid_size;
                    if (0 <= index && index < i_range_lo)
                    {
                        return false;
                    }
                    else return true;
                }

                return false;
            }
            public static void GetMappedData(PosIndex index,
                                             NativeMultiHashMap<PosIndex, T> map,
                                             ref NativeList<T> list)
            {
                var is_exist = map.TryGetFirstValue(index, out T item, out var iterator);
                if (!is_exist) return;

                list.Add(item);

                while (map.TryGetNextValue(out item, ref iterator))
                {
                    list.Add(item);
                }
            }
            public static void GetMappedData(MergedPosIndex target,
                                             NativeMultiHashMap<PosIndex, T> map,
                                             ref MergedNeighborList<T> buffer)
            {
                buffer.Clear();
                for (int iz = target.Lo.iz; iz < target.Hi.iz; iz++)
                {
                    for (int iy = target.Lo.iy; iy < target.Hi.iy; iy++)
                    {
                        for (int ix = target.Lo.ix; ix < target.Hi.ix; ix++)
                        {
                            var index = new PosIndex(ix, iy, iz);

                            int start = buffer._buffer.Length;
                            GetMappedData(index, map, ref buffer._buffer);

                            var range = new MergedNeighborList<T>.BufferRange { start = start, length = buffer._buffer.Length - start };
                            buffer._bufferIndex.Add(range);
                        }
                    }
                }
            }

            [Conditional("UNITY_EDITOR")]
            private static void CheckPos(Box box, float3 pos)
            {
                if (!box.IsInside(pos))
                    throw new ArgumentOutOfRangeException($"pos: {pos} is out of box. box = {box}");
            }

            public static PosIndex ComputeGridSize(Box box, int n_total, float n_in_cell)
            {
                if (n_total <= 0 || n_in_cell <= 0)
                    throw new ArgumentOutOfRangeException($"the numbers must be > 0. n_total = {n_total}, n_in_cell = {n_in_cell}");

                var ratio = new float3(1, box.Size.y / box.Size.x, box.Size.z / box.Size.x);
                ratio = ratio * math.pow(n_total / n_in_cell, 1 / 3);
                var cell_ratio = new float3(1) / ratio;
                return new PosIndex(math.max(4, (int)(cell_ratio.x) + 2),
                                    math.max(4, (int)(cell_ratio.y) + 2),
                                    math.max(4, (int)(cell_ratio.z) + 2));
            }
        }
    }
}
