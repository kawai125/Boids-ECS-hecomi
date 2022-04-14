using System;
using System.Diagnostics;
using UnityEngine;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace HashCellIndex
{
    public struct HashCellIndex<T> : IDisposable
        where T : unmanaged, IEquatable<T>
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
            _info.Target->GetNeighborListImpl(GetIndex(pos), _info.Target->GetIndexRange(r_search), boundary, _map, ref list);
        }
        public unsafe NativeList<T> GetNeighborList(float3 pos, float r_search, BoundaryCondition boundary,
                                                    int initialCapacity, Allocator alloc)
        {
            var list = new NativeList<T>(initialCapacity, alloc);
            _info.Target->GetNeighborListImpl(GetIndex(pos), _info.Target->GetIndexRange(r_search), boundary, _map, ref list);
            return list;
        }
        public unsafe void GetNeighborList(PosIndex origin, float r_search, BoundaryCondition boundary,
                                           NativeList<T> list)
        {
            list.Clear();
            _info.Target->GetNeighborListImpl(origin, _info.Target->GetIndexRange(r_search), boundary, _map, ref list);
        }
        public unsafe NativeList<T> GetNeighborList(PosIndex origin, float r_search, BoundaryCondition boundary,
                                                    int initialCapacity, Allocator alloc)
        {
            var list = new NativeList<T>(initialCapacity, alloc);
            _info.Target->GetNeighborListImpl(origin, _info.Target->GetIndexRange(r_search), boundary, _map, ref list);
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
            _info.Target->GetMergedNeighborListImpl(target, _info.Target->GetIndexRange(r_search), boundary, _map, ref buffer);
        }
        public unsafe MergedNeighborList<T> GetMergedNeighborList(MergedPosIndex target, float r_search, BoundaryCondition boundary,
                                                                  int listCapacityPerCell, Allocator alloc)
        {
            var search_range = _info.Target->GetIndexRange(r_search);
            MergedNeighborList<T>.CalcCapacity(target, search_range, listCapacityPerCell,
                                               out int n_index,
                                               out int buffer_capacity);

            var buffer = new MergedNeighborList<T>(n_index, buffer_capacity, alloc);

            _info.Target->GetMergedNeighborListImpl(target, search_range, boundary, _map, ref buffer);
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
        public MergedPosIndex GetMergedIndex(float3 Lo, float3 Hi)
        {
            CheckMergedCellSize(Lo, Hi);
            return new MergedPosIndex
            {
                Lo = GetIndex(Lo),
                Hi = GetIndex(Hi) + new PosIndex(1, 1, 1),
            };
        }
        public void GetValuesInMergedCell(float3 Lo, float3 Hi, MergedCell<T> buffer)
        {
            var merged_cell = GetMergedIndex(Lo, Hi);
            CellIndexInfo.GetMappedData(merged_cell, _map, ref buffer._cells);
        }
        public MergedCell<T> GetValuesInMergedCell(float3 Lo, float3 Hi,
                                                   int capacityForBuffer, Allocator alloc)
        {
            var merged_cell = GetMergedIndex(Lo, Hi);

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

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        internal static void CheckMergedCellSize(float3 Lo, float3 Hi)
        {
            if (Lo.x <= Hi.x &&
                Lo.y <= Hi.y &&
                Lo.z <= Hi.z) return;

            throw new ArgumentOutOfRangeException($"must be valid size. Lo={Lo} <= Hi={Hi}");
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
            public void GetNeighborListImpl(PosIndex origin, PosIndex indexRange, BoundaryCondition boundary,
                                            NativeMultiHashMap<PosIndex, T> map,
                                            ref NativeList<T> list)
            {
                var i_lo = origin - indexRange;
                var i_hi = origin + indexRange;

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
            public unsafe void GetMergedNeighborListImpl(MergedPosIndex target, PosIndex index_range,
                                                         BoundaryCondition boundary,
                                                         NativeMultiHashMap<PosIndex, T> map,
                                                         ref MergedNeighborList<T> mnl)
            {
                //--- naive plan
                //GetMergedNeighborListNaive(target, index_range, boundary, map, buffer);
                //return;

                mnl.Clear();

                //--- direct loading with x-zxis compress neighborlist plan
                mnl._info.Target->localGrid = target;
                mnl._info.Target->indexRange = index_range;
                mnl._info.Target->n_cell = target.Length;

                //------ load cells
                for(int iz = target.Lo.iz; iz < target.Hi.iz; iz++)
                {
                    for (int iy = target.Lo.iy; iy < target.Hi.iy; iy++)
                    {
                        for (int ix = target.Lo.ix; ix < target.Hi.ix; ix++)
                        {
                            var index_cell = new PosIndex(ix, iy, iz);

                            //--- read data into cache
                            int start = mnl._buffer.Length;
                            GetMappedData(index_cell, map, ref mnl._buffer);

                            var range = new MergedNeighborList<T>.BufferRange
                            {
                                start = start,
                                length = mnl._buffer.Length - start
                            };
                            mnl._bufferIndex.Add(range);
                        }
                    }
                }

                //------ load neighbors with x-zxis compress
                mnl._info.Target->neighborsOffset = mnl._bufferIndex.Length;
                var startInBuffer = new NativeList<int>(target.Hi.ix - target.Lo.ix + index_range.ix * 2, Allocator.Temp);
                for (int iz = target.Lo.iz; iz < target.Hi.iz; iz++)
                {
                    for (int iy = target.Lo.iy; iy < target.Hi.iy; iy++)
                    {
                        startInBuffer.Clear();
                        startInBuffer.Add(mnl._buffer.Length);

                        //--- first part
                        {
                            int ix = target.Lo.ix;

                            var index_cell = new PosIndex(ix, iy, iz);
                            var neighbor_lo = index_cell - index_range;
                            var neighbor_hi = index_cell + index_range;

                            for (int i_nx = neighbor_lo.ix; i_nx <= neighbor_hi.ix; i_nx++)
                            {
                                LoadNeighbors_YZ_Plane(i_nx, neighbor_lo, neighbor_hi, map, ref mnl._buffer);
                                startInBuffer.Add(mnl._buffer.Length);
                            }

                            int b_start = startInBuffer[ix - target.Lo.ix];
                            mnl.PushBackIndex(b_start);
                        }

                        //--- add difference part
                        for (int ix = target.Lo.ix + 1; ix < target.Hi.ix; ix++)
                        {
                            var index_cell = new PosIndex(ix, iy, iz);
                            var neighbor_lo = index_cell - index_range;
                            var neighbor_hi = index_cell + index_range;

                            LoadNeighbors_YZ_Plane(neighbor_hi.ix, neighbor_lo, neighbor_hi, map, ref mnl._buffer);
                            startInBuffer.Add(mnl._buffer.Length);

                            int b_start = startInBuffer[ix - target.Lo.ix];
                            mnl.PushBackIndex(b_start);
                        }
                    }
                }
                startInBuffer.Dispose();

                /*
                //--- cache data and build neighborlist from cache plan
                buffer.SetGridInfo(target, Hi - Lo, index_range);
                int i_cache = 0;
                for(int iz = Lo.iz; iz < Hi.iz; iz++)
                {
                    int i_nz = GetPeriodicIndex(iz, GridSize.iz, boundary.IsMatch(Boundary.Periodic_Z), Lo.iz, Hi.iz);
                    for(int iy = Lo.iy; iy < Hi.iy; iy++)
                    {
                        int i_ny = GetPeriodicIndex(iy, GridSize.iy, boundary.IsMatch(Boundary.Periodic_Y), Lo.iy, Hi.iy);
                        for (int ix = Lo.ix; ix < Hi.ix; ix++)
                        {
                            int i_nx = GetPeriodicIndex(ix, GridSize.ix, boundary.IsMatch(Boundary.Periodic_X), Lo.ix, Hi.ix);
                            var index_cell = new PosIndex(i_nx, i_ny, i_nz);

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
                */

                ////--- validate with naive implementation
                //var ref_mnl = new MergedNeighborList<T>(Allocator.Temp);
                //GetMergedNeighborListNaive(target, index_range, boundary, map, ref_mnl);
                //ValidateMergedNeighborList(mnl, ref_mnl);
                //ref_mnl.Dispose();
            }
            private static void LoadNeighbors_YZ_Plane(int i_nx, in PosIndex neighbor_lo, in PosIndex neighbor_hi,
                                                       NativeMultiHashMap<PosIndex, T> map,
                                                       ref NativeList<T> buffer)
            {
                for (int i_ny = neighbor_lo.iy; i_ny <= neighbor_hi.iy; i_ny++)
                {
                    for (int i_nz = neighbor_lo.iz; i_nz <= neighbor_hi.iz; i_nz++)
                    {
                        var neighbor_cell = new PosIndex(i_nx, i_ny, i_nz);
                        GetMappedData(neighbor_cell, map, ref buffer);
                    }
                }
            }
            private unsafe void GetMergedNeighborListNaive(MergedPosIndex target,
                                                           PosIndex index_range,
                                                           BoundaryCondition boundary,
                                                           NativeMultiHashMap<PosIndex, T> map,
                                                           MergedNeighborList<T> ref_mnl)
            {
                ref_mnl._info.Target->localGrid = target;
                ref_mnl._info.Target->indexRange = index_range;
                ref_mnl._info.Target->n_cell = target.Length;

                for (int i = 0; i < ref_mnl.Length; i++)
                {
                    int start = ref_mnl._buffer.Length;
                    var index = ref_mnl.GetIndex(i);

                    GetMappedData(index, map, ref ref_mnl._buffer);

                    var range = new MergedNeighborList<T>.BufferRange
                    {
                        start = start,
                        length = ref_mnl._buffer.Length - start
                    };
                    ref_mnl._bufferIndex.Add(range);
                }

                ref_mnl._info.Target->neighborsOffset = ref_mnl._bufferIndex.Length;
                for (int iz = target.Lo.iz; iz < target.Hi.iz; iz++)
                {
                    for (int iy = target.Lo.iy; iy < target.Hi.iy; iy++)
                    {
                        for (int ix = target.Lo.ix; ix < target.Hi.ix; ix++)
                        {
                            var index_cell = new PosIndex(ix, iy, iz);

                            int start = ref_mnl._buffer.Length;

                            GetNeighborListImpl(index_cell, index_range, boundary, map, ref ref_mnl._buffer);

                            var range = new MergedNeighborList<T>.BufferRange
                            {
                                start = start,
                                length = ref_mnl._buffer.Length - start
                            };
                            ref_mnl._bufferIndex.Add(range);
                        }
                    }
                }
            }
            private unsafe void ValidateMergedNeighborList(MergedNeighborList<T> buffer,
                                                           MergedNeighborList<T> ref_mnl)
            {
                bool is_valid = true;
                var sb = new System.Text.StringBuilder();
                if (buffer.Length != ref_mnl.Length)
                {
                    is_valid = false;
                    sb.Append($"n_cell was differ. tgt={buffer.Length}, ref={ref_mnl.Length}\n");
                }
                for (int i_cell = 0; i_cell < ref_mnl.Length; i_cell++)
                {
                    var index_global = buffer.GetIndex(i_cell);
                    sb.Append($"i_cell={i_cell}");
                    sb.Append($", cache index={index_global - buffer._info.Target->localGrid.Lo + buffer._info.Target->indexRange}");
                    sb.Append($", global index={index_global}:\n");

                    var tgt_cell = buffer.GetCell(i_cell);
                    var ref_cell = ref_mnl.GetCell(i_cell);

                    if (tgt_cell.Length != ref_cell.Length)
                    {
                        is_valid = false;
                        sb.Append($"  cell.Length was differ. tgt={tgt_cell.Length}, ref={ref_cell.Length}:\n");
                    }
                    for (int i = 0; i < ref_cell.Length; i++)
                    {
                        var value = ref_cell[i];
                        if (!tgt_cell.Contains(value))
                        {
                            is_valid = false;
                            sb.Append($"  ref[{i}]={value} is not found in tgt_cell. in cache: {SearchTargetFromCache(buffer, value)}\n");
                        }
                    }
                    for (int i = 0; i < tgt_cell.Length; i++)
                    {
                        var value = tgt_cell[i];
                        if (!ref_cell.Contains(value))
                        {
                            is_valid = false;
                            sb.Append($"  tgt[{i}]={value} is error. in cache: {SearchTargetFromCache(buffer, value)}\n");
                        }
                    }

                    var tgt_neighbors = buffer.GetNeighbors(i_cell);
                    var ref_neighbors = ref_mnl.GetNeighbors(i_cell);

                    if(tgt_neighbors.Length != ref_neighbors.Length)
                    {
                        is_valid = false;
                        sb.Append($"  neighbors.Length was differ. tgt={tgt_neighbors.Length}, ref={ref_neighbors.Length}:\n");
                    }
                    for (int i = 0; i < ref_neighbors.Length; i++)
                    {
                        var value = ref_neighbors[i];
                        if (!tgt_neighbors.Contains(value))
                        {
                            is_valid = false;
                            sb.Append($"  ref[{i}]={value} is not found in tgt_neighbors. in cache: {SearchTargetFromCache(buffer, value)}\n");
                        }
                    }
                    for (int i = 0; i < tgt_neighbors.Length; i++)
                    {
                        var value = tgt_neighbors[i];
                        if (!ref_neighbors.Contains(value))
                        {
                            is_valid = false;
                            sb.Append($"  tgt[{i}]={value} is error. in cache: {SearchTargetFromCache(buffer, value)}\n");
                        }
                    }
                }

                if (!is_valid)
                {
                    sb.Append($"\nMergedNeighborList<T> target:\n{buffer}");
                    sb.Append($"\nMergedNeighborList<T> ref:\n{ref_mnl}");
                    throw new InvalidProgramException(sb.ToString());
                }
                else
                {
                    sb.Clear();
                    sb.Append($"MergedCell={buffer._info.Target->localGrid}");
                    sb.Append($": buffer compress={(float)buffer._buffer.Length / ref_mnl._buffer.Length}");
                    UnityEngine.Debug.Log(sb);
                }
            }
            private unsafe string SearchTargetFromCache(MergedNeighborList<T> buffer, T tgt)
            {
                var cacheGrid = buffer._info.Target->cacheGrid;
                if (buffer._info.Target->cellsOffset <= 0 ||
                    buffer._info.Target->cellsOffset != cacheGrid.ix * cacheGrid.iy * cacheGrid.iz)
                    return "not found";

                int i_cache = 0;
                for (int iz = 0; iz < cacheGrid.iz; iz++)
                {
                    for (int iy = 0; iy < cacheGrid.iy; iy++)
                    {
                        for (int ix = 0; ix < cacheGrid.ix; ix++)
                        {
                            var range = buffer._bufferIndex[i_cache];
                            for(int i_buffer = range.start; i_buffer < range.end; i_buffer++)
                            {
                                if (tgt.Equals(buffer._buffer[i_buffer]))
                                {
                                    var index = new PosIndex(ix, iy, iz);
                                    var sb = new System.Text.StringBuilder();
                                    sb.Append($"[i={i_cache}, cache index={index}");
                                    sb.Append($" global index={index - buffer._info.Target->indexRange + buffer._info.Target->localGrid.Lo}");
                                    return sb.ToString();
                                }
                            }
                            i_cache++;
                        }
                    }
                }
                return "not found";
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
            private static int GetPeriodicIndex(int index, int grid_size, bool is_periodic, int i_range_lo, int i_range_hi)
            {
                if (!is_periodic) return index;

                if (index < 0)
                {
                    var p_index = index + grid_size;
                    if (i_range_hi < p_index && p_index < grid_size)
                    {
                        return p_index;
                    }
                    else return index;
                }
                else if (index >= grid_size)
                {
                    var p_index = index - grid_size;
                    if (0 <= p_index && p_index < i_range_lo)
                    {
                        return p_index;
                    }
                    else return index;
                }

                return index;
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

                            var range = new MergedNeighborList<T>.BufferRange
                            {
                                start = start,
                                length = buffer._buffer.Length - start
                            };
                            buffer._bufferIndex.Add(range);
                        }
                    }
                }
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
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
