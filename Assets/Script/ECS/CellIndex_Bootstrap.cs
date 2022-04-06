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
    public static CellIndex_Bootstrap Instance { get; private set; }

    public HashCellIndex<Entity> HashCellIndex;
    public NativeList<Entity> EscapedEntities;

    public float RangeCoef;

    public int NumberOfContainsCells { get; private set; }
    public int CellBatchSize { get; private set; }

    private NativeList<PosIndex> _containsIndexList;
    private Dictionary<string, JobHandle> _handles;
    private bool _allocated;

    private void Awake()
    {
        Instance = this;
    }

    // Start is called before the first frame update
    void Start()
    {
        HashCellIndex = new HashCellIndex<Entity>(Allocator.Persistent);
        EscapedEntities = new NativeList<Entity>(Allocator.Persistent);
        RangeCoef = Bootstrap.Param.cellIndexRangeCoef;

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

            HashCellIndex.Dispose();
            EscapedEntities.Dispose();

            _containsIndexList.Dispose();

            _allocated = false;
        }
    }

    public void InitDomain(int n_boids)
    {
        float wall_scale_for_cell_index = Bootstrap.Param.wallScale * 0.5f + 1f;  // add margin 1.0f for box
        HashCellIndex.Clear();
        HashCellIndex.InitDomainWithRange(new Box(new float3(-(wall_scale_for_cell_index)),
                                                  new float3( (wall_scale_for_cell_index))),
                                          n_boids,
                                          Bootstrap.Param.neighborDistance * RangeCoef);
    }
    public void UpdateBatchSize()
    {
        HashCellIndex.GetContainsIndexList(_containsIndexList);
        NumberOfContainsCells = _containsIndexList.Length;

        //--- grid size based
    //    var grid = HashCellIndex.GridSize;
    //    int n_cell = grid.ix * grid.iy * grid.iz;
    //    CellBatchSize = math.max(1, (int)math.pow(n_cell, 1f / 3));

        //--- number of significant cells based
    //    CellBatchSize = math.max(1, (int)math.pow(NumberOfContainsCells, 0.34f));

        //--- number of worker thread based
        const int n_pack = 6;
        CellBatchSize = math.max(1, NumberOfContainsCells / (n_pack * JobsUtility.JobWorkerCount));
    }
    public void SetJobHandle(string job_identifier, JobHandle handle)
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
