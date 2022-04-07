using System;
using System.Diagnostics;
using UnityEngine;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Domain
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
        public unsafe void GetNeighborList(float3 pos, float r_search, BoundaryCondition boundary,
                                           NativeList<T> list)
        {
            GetNeighborList(GetIndex(pos), r_search, boundary, list);
        }
        public unsafe void GetNeighborList(PosIndex origin, float r_search, BoundaryCondition boundary,
                                           NativeList<T> list)
        {
            list.Clear();
            if (_map.Count() <= 0) return;
            _info.Target->GetNeighborListImpl(origin, r_search, boundary, _map, list);
        }

        public unsafe PosIndex GridSize { get { return _info.Target->GridSize; } }
        public unsafe void GetGridIndexList(NativeList<PosIndex> index_list)
        {
            index_list.Clear();
            _info.Target->GetGridIndexListImpl(index_list);
        }
        /*
        // cannot use for counting species of keys
        // (remarks: A key with N values is included N times in the array)
        public NativeArray<PosIndex> GetContainsIndexList(Allocator alloc)
        {
            return _map.GetKeyArray(alloc);
        }
        */
        public unsafe void GetContainsIndexList(NativeList<PosIndex> index_list)
        {
            index_list.Clear();
            _info.Target->GetContainsIndexListImpl(index_list, _map);
        }
        public void GetValuesInCell(PosIndex index, NativeList<T> list)
        {
            list.Clear();
            if (_map.Count() <= 0) return;
            CellIndexInfo.GetMappedData(index, _map, list);
        }
        public unsafe void GetSearchIndexList(PosIndex index, float r_search, BoundaryCondition boundary,
                                              NativeList<PosIndex> search_target_list)
        {
            search_target_list.Clear();
            if(_map.Count() <= 0) return;
            _info.Target->GetSearchIndexListImpl(index, r_search, boundary, search_target_list);
        }

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

                var grid_size = new PosIndex(math.max((int)(box.Size.x / r_search) + 1, 4),
                                             math.max((int)(box.Size.y / r_search) + 1, 4),
                                             math.max((int)(box.Size.z / r_search) + 1, 4));
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

                RSearchMax = math.sqrt(math.dot(Box.Size, Box.Size)) * 1.01f;  // all cells in range if r > RSearchMax.
                RSearchMargin = math.sqrt(math.dot(CellSize, CellSize));

                return true;
            }
            public PosIndex GetIndex(float3 pos)
            {
                CheckPos(pos);
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
                                            NativeList<T> list)
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
                            GetMappedData(index, map, list);

                            //UnityEngine.Debug.Log($"  --> load from cell={index}");
                        }
                    }
                }
            }
            public void GetSearchIndexListImpl(PosIndex origin, float r_search, BoundaryCondition boundary,
                                               NativeList<PosIndex> search_target_list)
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
            public void GetGridIndexListImpl(NativeList<PosIndex> list)
            {
                for(int iz=0; iz<GridSize.iz; iz++)
                {
                    for(int iy=0; iy<GridSize.iy; iy++)
                    {
                        for(int ix=0; ix<GridSize.ix; ix++)
                        {
                            list.Add(new PosIndex(ix, iy, iz));
                        }
                    }
                }
            }
            public void GetContainsIndexListImpl(NativeList<PosIndex> list, NativeMultiHashMap<PosIndex, T> map)
            {
                for (int iz = 0; iz < GridSize.iz; iz++)
                {
                    for (int iy = 0; iy < GridSize.iy; iy++)
                    {
                        for (int ix = 0; ix < GridSize.ix; ix++)
                        {
                            var index = new PosIndex(ix, iy, iz);
                            if (map.ContainsKey(index)) list.Add(new PosIndex(ix, iy, iz));
                        }
                    }
                }
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
                                             NativeList<T> list)
            {
                var is_exist = map.TryGetFirstValue(index, out T item, out var iterator);
                if (!is_exist) return;

                list.Add(item);

                while (map.TryGetNextValue(out item, ref iterator))
                {
                    list.Add(item);
                }
            }

            [Conditional("UNITY_EDITOR")]
            private void CheckPos(float3 pos)
            {
                if (!Box.IsInside(pos))
                    throw new ArgumentOutOfRangeException($"pos: {pos} is out of box. box = {Box}");
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
