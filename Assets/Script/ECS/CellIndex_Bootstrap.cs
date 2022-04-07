using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Unity.Collections;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

using Domain;

public class CellIndex_Bootstrap : MonoBehaviour
{
    private static CellIndex_Bootstrap Instance;
    private void Awake()
    {
        Instance = this;
    }

    public static HashCellIndex<Entity> HashCellIndex { get { return Instance._cellIndex; } }

    public static float RangeCoef {
        get { return Instance._range_coef; }
        set { Instance._range_coef = value; }
    }
    public static int NumberOfContainsCells { get { return Instance._num_contains_cells; } }
    public static int CellBatchSize { get { return Instance._cell_batch_size; } }
    public static float MapBufferUsed
    {
        get { return Instance._map_buffer_used; }
        set { Instance._map_buffer_used = value; }
    }

    private HashCellIndex<Entity> _cellIndex;
    private NativeList<PosIndex> _containsIndexList;

    private float _range_coef;
    private int _num_contains_cells;
    private int _cell_batch_size;
    private float _map_buffer_used;

    private Dictionary<string, JobHandle> _handles;
    private bool _allocated;

    // Start is called before the first frame update
    void Start()
    {
        _cellIndex = new HashCellIndex<Entity>(Allocator.Persistent);
        _range_coef = Define.InitialCellIndexRangeCoef;

        _containsIndexList = new NativeList<PosIndex>(Allocator.Persistent);

        _handles = new Dictionary<string, JobHandle>();

        _allocated = true;
    }

    private void OnDestroy()
    {
        Dispose();
    }
    ~CellIndex_Bootstrap()
    {
        Dispose();
    }
    public void Dispose()
    {
        if (_allocated)
        {
            foreach(var handle in _handles.Values) handle.Complete();

            _cellIndex.Dispose();

            _containsIndexList.Dispose();

            _allocated = false;
        }
    }

    public static void InitDomain(int n_boids) => Instance.InitDomainImpl(n_boids);
    private void InitDomainImpl(int n_boids)
    {
        float wall_scale_for_cell_index = Bootstrap.WallScale * 0.5f + 1f;  // add margin 1.0f for box
        _cellIndex.Clear();
        _cellIndex.InitDomainWithRange(new Box(new float3(-(wall_scale_for_cell_index)),
                                               new float3( (wall_scale_for_cell_index))),
                                       n_boids,
                                       Bootstrap.NeighborSearchRange * _range_coef);
    }
    public static void UpdateBatchSize() => Instance.UpdateBatchSizeImpl();
    private void UpdateBatchSizeImpl()
    {
        _cellIndex.GetContainsIndexList(_containsIndexList);
        _num_contains_cells = _containsIndexList.Length;

        //--- grid size based
        //    var grid = _cellIndex.GridSize;
        //    int n_cell = grid.ix * grid.iy * grid.iz;
        //    _cell_batch_size = math.max(1, (int)math.pow(n_cell, 1f / 3));

        //--- number of significant cells based
        //    _cell_batch_size = math.max(1, (int)math.pow(_num_contains_cells, 0.34f));

        //--- number of worker thread based
        const int n_pack = 8;
        _cell_batch_size = math.max(1, _num_contains_cells / (n_pack * JobsUtility.JobWorkerCount));
    }
    public static void SetJobHandle(string job_identifier, JobHandle handle) => Instance.SetJobHandleImpl(job_identifier, handle);
    private void SetJobHandleImpl(string job_identifier, JobHandle handle)
    {
        if (_handles.ContainsKey(job_identifier))
        {
            _handles[job_identifier] = handle;
        }
        else
        {
            _handles.Add(job_identifier, handle);
        }
    }
}
