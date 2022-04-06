using System;
using System.Collections.Generic;
using UnityEngine;

using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;

using Domain;


[UpdateInGroup(typeof(NeighborDetectionSystemGroup))]
public partial class NeighborDetectionSystem_Direct : SystemBase
{
    private EntityQuery query;

    protected override void OnCreate()
    {
        base.OnCreate();

        query = GetEntityQuery(new EntityQueryDesc
        {
            All = new[] {
                ComponentType.ReadOnly<BoidType>(),
                ComponentType.ReadOnly<Translation>(),
                ComponentType.ReadOnly<Velocity>(),
                ComponentType.ReadWrite<NeighborsEntityBuffer>(),

                ComponentType.ReadOnly<Tag_ComputeNeighbors_Direct>()
            }
        });
    }

    private struct NeighborDetectionDataContainer
    {
        [ReadOnly] public float prodThresh;
        [ReadOnly] public float distThresh;

        [ReadOnly]
        public ComponentDataFromEntity<Translation> positionFromGrovalEntity;

        [DeallocateOnJobCompletion]
        [ReadOnly]
        public NativeArray<Entity> entitiesGroval;
    }

    protected override void OnUpdate()
    {
        var common_data = new NeighborDetectionDataContainer
        {
            prodThresh = math.cos(math.radians(Bootstrap.Param.neighborFov)),
            distThresh = Bootstrap.Param.neighborDistance,

            positionFromGrovalEntity = GetComponentDataFromEntity<Translation>(true),

            entitiesGroval = query.ToEntityArray(Allocator.TempJob),
        };

        // args for Entities.ForEach() must be ordered as (Entity), (ref args), and (in args).
        Dependency = Entities.
            WithName("NeighborDetectionJob").
            WithAll<BoidType, Tag_ComputeNeighbors_Direct>().
            WithBurst().
            ForEach(
        (Entity entity, ref DynamicBuffer<NeighborsEntityBuffer> neighbors, in Translation position, in Velocity velocity) =>
        {
            neighbors.Clear();

            float3 pos0 = position.Value;
            float3 fwd0 = math.normalize(velocity.Value);

            float r2_search = common_data.distThresh * common_data.distThresh;
            for (int i = 0; i < common_data.entitiesGroval.Length; i++)
            {
                var target = common_data.entitiesGroval[i];
                if (entity == target) continue;

                float3 pos1 = common_data.positionFromGrovalEntity[target].Value;
                var to = pos1 - pos0;
                var dist2 = math.lengthsq(to);

                if (dist2 < r2_search)
                {
                    var dir = math.normalize(to);
                    var prod = math.dot(dir, fwd0);
                    if (prod > common_data.prodThresh)
                    {
                        neighbors.Add(new NeighborsEntityBuffer { entity = target });
                    }
                }
            }
        }).ScheduleParallel(Dependency);
    }
}

[UpdateInGroup(typeof(BuildCellIndexSystemGroup))]
public partial class BuildCellIndexSystetm : SystemBase
{
    protected override void OnUpdate()
    {
        Dependency.Complete();

        //--- update cell index
        var cellIndex = CellIndex_Bootstrap.Instance.HashCellIndex;
        CellIndex_Bootstrap.Instance.InitDomain(Bootstrap.Instance.GetBoidsCount());

        var cellIndexWriter = cellIndex.AsParallelWriter();
        Dependency = Entities.
            WithName("UpdateCellIndexJob").
            WithAll<BoidType>().
            WithNone<Tag_ComputeNeighbors_Direct>().
            WithBurst().
            ForEach(
            (Entity entity, in Translation pos) =>
            {
                cellIndexWriter.TryAdd(pos.Value, entity);
            }).ScheduleParallel(Dependency);

        CellIndex_Bootstrap.Instance.SetJobHandle("BuildCellIndexSystetm", Dependency);

        Dependency.Complete();

        CellIndex_Bootstrap.Instance.UpdateBatchSize();
    }
}

[UpdateInGroup(typeof(NeighborDetectionSystemGroup))]
public partial class NeighborDetectionSystem_CellIndex_Entity_NeighborList : SystemBase
{

    private struct NeighborDetectionDataContainer
    {
        [ReadOnly] public float prodThresh;
        [ReadOnly] public float distThresh;

        [ReadOnly]
        public ComponentDataFromEntity<Translation> positionFromGrovalEntity;

        [ReadOnly]
        public HashCellIndex<Entity> cellIndex;

        public void GetNeighborList(float3 pos, NativeList<Entity> list)
        {
            list.Clear();
            if (cellIndex.Box.IsInside(pos)) cellIndex.GetNeighborList(pos, distThresh, Boundary.Open, list);
        }
    }

    protected override void OnUpdate()
    {

        //--- search neighbors
        var common_data = new NeighborDetectionDataContainer
        {
            prodThresh = math.cos(math.radians(Bootstrap.Param.neighborFov)),
            distThresh = Bootstrap.Param.neighborDistance,

            positionFromGrovalEntity = GetComponentDataFromEntity<Translation>(true),

            cellIndex = CellIndex_Bootstrap.Instance.HashCellIndex,
        };
        Dependency = Entities.
            WithName("NeighborDetectionJob_CellIndex_Entity_NeighborList").
            WithAll<BoidType, Tag_ComputeNeighbors_CellIndex_Entity_NeighborList>().
            WithBurst().
            ForEach(
        (Entity entity, ref DynamicBuffer<NeighborsEntityBuffer> neighbors, in Translation position, in Velocity velocity) =>
        {
            neighbors.Clear();

            float3 pos0 = position.Value;
            float3 fwd0 = math.normalize(velocity.Value);

            if (!common_data.cellIndex.Box.IsInside(pos0)) return;

            var entitiesForSearch = new NativeList<Entity>(32, Allocator.Temp);
            common_data.GetNeighborList(pos0, entitiesForSearch);

            var positionsForSearch = ComponentDataUtility.GatherComponentData(common_data.positionFromGrovalEntity,
                                                                              entitiesForSearch, Allocator.Temp);

            NeighborDetectionFunc.SearchNeighbors(entity,
                                                  pos0,
                                                  fwd0,
                                                  common_data.distThresh,
                                                  common_data.prodThresh,
                                                  entitiesForSearch,
                                                  positionsForSearch,
                                                  neighbors);

            entitiesForSearch.Dispose();
            positionsForSearch.Dispose();

        }).ScheduleParallel(Dependency);

        CellIndex_Bootstrap.Instance.SetJobHandle("NeighborDetectionSystem_CellIndex_Entity_NeighborList", Dependency);
    }
}

[UpdateInGroup(typeof(NeighborDetectionSystemGroup))]
public partial class NeighborDetectionSystem_CellIndex_Cell_NeighborList : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();

        RequireForUpdate(
            GetEntityQuery(typeof(BoidType),
                           typeof(Translation),
                           typeof(Velocity),
                           typeof(Tag_ComputeNeighbors_CellIndex_Cell_NeighborList)));
    }

    [BurstCompile]
    private struct NeighborDetectionJob_Cell_NeighborList : IJobParallelFor
    {
        [ReadOnly] public float prodThresh;
        [ReadOnly] public float distThresh;

        [ReadOnly]
        public ComponentDataFromEntity<Translation> positionFromGrovalEntity;
        [ReadOnly]
        public ComponentDataFromEntity<Velocity> velocityFromGrovalEntity;

        [ReadOnly]
        public HashCellIndex<Entity> cellIndex;

        [ReadOnly]
        public NativeArray<PosIndex> cellList;

        [NativeDisableContainerSafetyRestriction]
        public BufferFromEntity<NeighborsEntityBuffer> neighborsFromGrobalEntity;

        public void Execute(int i_job)
        {
            var index = cellList[i_job];

            var entitiesInCell = new NativeList<Entity>(64, Allocator.Temp);
            cellIndex.GetValuesInCell(index, entitiesInCell);

            if (entitiesInCell.Length <= 0)
            {
                entitiesInCell.Dispose();
                return;
            }

            var entitiesForSearch = new NativeList<Entity>(256, Allocator.Temp);
            cellIndex.GetNeighborList(index, distThresh, Boundary.Open, entitiesForSearch);

            //--- gather data in cell
            var positionsInCell = ComponentDataUtility.GatherComponentData(positionFromGrovalEntity, entitiesInCell, Allocator.Temp);
            var velocitiesInCell = ComponentDataUtility.GatherComponentData(velocityFromGrovalEntity, entitiesInCell, Allocator.Temp);

            //--- gather data for search
            var positionsForSearch = ComponentDataUtility.GatherComponentData(positionFromGrovalEntity, entitiesForSearch, Allocator.Temp);

            //--- search neighbors by cell to neighbors from cellIndex
            for (int i_entity = 0; i_entity < entitiesInCell.Length; i_entity++)
            {
                var neighbors = neighborsFromGrobalEntity[entitiesInCell[i_entity]];
                neighbors.Clear();
                NeighborDetectionFunc.SearchNeighbors(entitiesInCell[i_entity],
                                                      positionsInCell[i_entity].Value,
                                                      math.normalize(velocitiesInCell[i_entity].Value),
                                                      distThresh,
                                                      prodThresh,
                                                      entitiesForSearch,
                                                      positionsForSearch,
                                                      neighbors);
            }

            entitiesInCell.Dispose();
            entitiesForSearch.Dispose();
            positionsForSearch.Dispose();

            positionsInCell.Dispose();
            velocitiesInCell.Dispose();
        }
    }

    protected override void OnUpdate()
    {

        //--- search neighbors
        var cellIndex = CellIndex_Bootstrap.Instance.HashCellIndex;
        var cell_list = new NativeList<PosIndex>(Allocator.TempJob);
        cellIndex.GetContainsIndexList(cell_list);

        var neighborDetectionJob = new NeighborDetectionJob_Cell_NeighborList()
        {
            prodThresh = math.cos(math.radians(Bootstrap.Param.neighborFov)),
            distThresh = Bootstrap.Param.neighborDistance,

            positionFromGrovalEntity = GetComponentDataFromEntity<Translation>(true),
            velocityFromGrovalEntity = GetComponentDataFromEntity<Velocity>(true),

            cellIndex = cellIndex,
            cellList = cell_list,

            neighborsFromGrobalEntity = GetBufferFromEntity<NeighborsEntityBuffer>(),
        };
        Dependency = neighborDetectionJob.Schedule(cell_list.Length,
                                                   CellIndex_Bootstrap.Instance.CellBatchSize,
                                                   Dependency);

        cell_list.Dispose(Dependency);

        CellIndex_Bootstrap.Instance.SetJobHandle("NeighborDetectionSystem_CellIndex_Cell_NeighborList", Dependency);
    }
}

[UpdateInGroup(typeof(NeighborDetectionSystemGroup))]
public partial class NeighborDetectionSystem_CellIndex_Cell_Cell : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();

        RequireForUpdate(
            GetEntityQuery(typeof(BoidType),
                           typeof(Translation),
                           typeof(Velocity),
                           typeof(Tag_ComputeNeighbors_CellIndex_Cell_Cell)));
    }

    [BurstCompile]
    private struct NeighborDetectionJob_Cell_Cell : IJobParallelFor
    {
        [ReadOnly] public float prodThresh;
        [ReadOnly] public float distThresh;

        [ReadOnly]
        public ComponentDataFromEntity<Translation> positionFromGrovalEntity;
        [ReadOnly]
        public ComponentDataFromEntity<Velocity> velocityFromGrovalEntity;

        [ReadOnly]
        public HashCellIndex<Entity> cellIndex;

        [ReadOnly]
        public NativeArray<PosIndex> cellList;

        [NativeDisableContainerSafetyRestriction]
        public BufferFromEntity<NeighborsEntityBuffer> neighborsFromGrobalEntity;

        public void Execute(int i_job)
        {
            var index = cellList[i_job];

            var entitiesInCell = new NativeList<Entity>(64, Allocator.Temp);
            cellIndex.GetValuesInCell(index, entitiesInCell);

            if(entitiesInCell.Length <= 0)
            {
                entitiesInCell.Dispose();
                return;
            }

            var searchIndexList = new NativeList<PosIndex>(64, Allocator.Temp);
            var entitiesForSearch = new NativeList<Entity>(64, Allocator.Temp);
            var positionsForSearch = new NativeList<Translation>(64, Allocator.Temp);

            //--- gather data in cell
            var positionsInCell = ComponentDataUtility.GatherComponentData(positionFromGrovalEntity, entitiesInCell, Allocator.Temp);
            var velocitysInCell = ComponentDataUtility.GatherComponentData(velocityFromGrovalEntity, entitiesInCell, Allocator.Temp);

            //--- clear neighbors
            for(int i = 0; i < entitiesInCell.Length; i++)
            {
                var entity = entitiesInCell[i];
                neighborsFromGrobalEntity[entity].Clear();
            }

            //--- search neighbors by cell to cell
            cellIndex.GetSearchIndexList(index, distThresh, Boundary.Open, searchIndexList);
            for (int i_cell = 0; i_cell < searchIndexList.Length; i_cell++)
            {
                entitiesForSearch.Clear();
                cellIndex.GetValuesInCell(searchIndexList[i_cell], entitiesForSearch);

                //--- gather data for search target cell
                positionsForSearch.ResizeUninitialized(entitiesForSearch.Length);
                for(int i=0; i<entitiesForSearch.Length; i++)
                {
                    positionsForSearch[i] = positionFromGrovalEntity[entitiesForSearch[i]];
                }

                //--- detect neighbors
                for (int i_entity = 0; i_entity < entitiesInCell.Length; i_entity++)
                {
                    NeighborDetectionFunc.SearchNeighbors(entitiesInCell[i_entity],
                                                          positionsInCell[i_entity].Value,
                                                          math.normalize(velocitysInCell[i_entity].Value),
                                                          distThresh,
                                                          prodThresh,
                                                          entitiesForSearch,
                                                          positionsForSearch,
                                                          neighborsFromGrobalEntity[entitiesInCell[i_entity]]);
                }
            }

            searchIndexList.Dispose();
            entitiesInCell.Dispose();
            entitiesForSearch.Dispose();
            positionsForSearch.Dispose();

            positionsInCell.Dispose();
            velocitysInCell.Dispose();
        }
    }

    protected override void OnUpdate()
    {
        //--- search neighbors
        var cellIndex = CellIndex_Bootstrap.Instance.HashCellIndex;
        var cell_list = new NativeList<PosIndex>(Allocator.TempJob);
        cellIndex.GetContainsIndexList(cell_list);

        var neighborDetectionJob = new NeighborDetectionJob_Cell_Cell()
        {
            prodThresh = math.cos(math.radians(Bootstrap.Param.neighborFov)),
            distThresh = Bootstrap.Param.neighborDistance,

            positionFromGrovalEntity = GetComponentDataFromEntity<Translation>(true),
            velocityFromGrovalEntity = GetComponentDataFromEntity<Velocity>(true),

            cellIndex = cellIndex,
            cellList = cell_list,

            neighborsFromGrobalEntity = GetBufferFromEntity<NeighborsEntityBuffer>(),
        };
        Dependency = neighborDetectionJob.Schedule(cell_list.Length,
                                                   CellIndex_Bootstrap.Instance.CellBatchSize,
                                                   Dependency);

        cell_list.Dispose(Dependency);

        CellIndex_Bootstrap.Instance.SetJobHandle("NeighborDetectionSystem_CellIndex_Cell_Cell", Dependency);
    }
}

[UpdateInGroup(typeof(NeighborDetectionSystemGroup))]
public partial class NeighborDetectionSystem_CellIndex_Combined : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();

        RequireForUpdate(
            GetEntityQuery(typeof(BoidType),
                           typeof(Translation),
                           typeof(Velocity),
                           typeof(Acceleration),
                           typeof(Tag_ComputeNeighbors_CellIndex_Combined)));
    }

    [BurstCompile]
    private struct NeighborDetectionJob_Combined : IJobParallelFor
    {
        [ReadOnly] public float prodThresh;
        [ReadOnly] public float distThresh;

        [ReadOnly] public float alignmentWeight;
        [ReadOnly] public float cohesionWeight;
        [ReadOnly] public float separationWeight;

        [ReadOnly]
        public ComponentDataFromEntity<Translation> positionFromGrovalEntity;
        [ReadOnly]
        public ComponentDataFromEntity<Velocity> velocityFromGrovalEntity;

        [ReadOnly]
        public HashCellIndex<Entity> cellIndex;

        [ReadOnly]
        public NativeArray<PosIndex> cellList;

        [NativeDisableContainerSafetyRestriction]
        public ComponentDataFromEntity<Acceleration> accelerationFromGrobalEntity;

        private struct TmpForceValue
        {
            public float3 Alignment, Cohesion, Separation;
            public int Count;

            public float3 GetAccel(Translation pos0, Velocity vel0,
                                   float alignmentWeight,
                                   float cohesionWeight,
                                   float separationWeight)
            {
                if (Count <= 0) return float3.zero;

                float count_inv = 1f / Count;

                return (Alignment * count_inv - vel0.Value) * alignmentWeight
                       + (Cohesion * count_inv - pos0.Value) * cohesionWeight
                       + (Separation * count_inv) * separationWeight;
            }
        }

        public void Execute(int i_job)
        {
            var index = cellList[i_job];

            var entitiesInCell = new NativeList<Entity>(64, Allocator.Temp);
            cellIndex.GetValuesInCell(index, entitiesInCell);

            if(entitiesInCell.Length <= 0)
            {
                entitiesInCell.Dispose();
                return;
            }

            var searchIndexList = new NativeList<PosIndex>(64, Allocator.Temp);
            var entitiesForSearch = new NativeList<Entity>(64, Allocator.Temp);
            var positionsForSearch = new NativeList<Translation>(64, Allocator.Temp);
            var velocitiesForSearch = new NativeList<Velocity>(64, Allocator.Temp);

            //--- gather data in cell
            var positionsInCell = ComponentDataUtility.GatherComponentData(positionFromGrovalEntity, entitiesInCell, Allocator.Temp);
            var velocitiesInCell = ComponentDataUtility.GatherComponentData(velocityFromGrovalEntity, entitiesInCell, Allocator.Temp);

            var tmpForceInCell = new NativeArray<TmpForceValue>(entitiesInCell.Length, Allocator.Temp);

            //--- search neighbors by cell to cell
            cellIndex.GetSearchIndexList(index, distThresh, Boundary.Open, searchIndexList);
            for (int i_cell = 0; i_cell < searchIndexList.Length; i_cell++)
            {
                entitiesForSearch.Clear();
                cellIndex.GetValuesInCell(searchIndexList[i_cell], entitiesForSearch);

                if (entitiesForSearch.Length <= 0) continue;

                //--- gather data for search target cell
                positionsForSearch.ResizeUninitialized(entitiesForSearch.Length);
                velocitiesForSearch.ResizeUninitialized(entitiesForSearch.Length);
                for (int i_entity=0; i_entity < entitiesForSearch.Length; i_entity++)
                {
                    var entity = entitiesForSearch[i_entity];
                    positionsForSearch[i_entity] = positionFromGrovalEntity[entity];
                    velocitiesForSearch[i_entity] = velocityFromGrovalEntity[entity];
                }

                //--- compute interaction with neighbors
                for (int i_entity = 0; i_entity < entitiesInCell.Length; i_entity++)
                {
                    var force_tmp = tmpForceInCell[i_entity];
                    ComputeInteraction(entitiesInCell[i_entity],
                                       positionsInCell[i_entity].Value,
                                       math.normalize(velocitiesInCell[i_entity].Value),
                                       distThresh,
                                       prodThresh,
                                       entitiesForSearch,
                                       positionsForSearch,
                                       velocitiesForSearch,
                                       ref force_tmp);
                    tmpForceInCell[i_entity] = force_tmp;
                }
            }

            //--- writeback buffered acceleration
            for (int i=0; i<entitiesInCell.Length; i++)
            {
                var entity = entitiesInCell[i];
                var acc = accelerationFromGrobalEntity[entity].Value;
                acc += tmpForceInCell[i].GetAccel(positionsInCell[i], velocitiesInCell[i],
                                                  alignmentWeight, cohesionWeight, separationWeight);
                accelerationFromGrobalEntity[entity] = new Acceleration { Value = acc };
            }

            searchIndexList.Dispose();
            entitiesInCell.Dispose();
            entitiesForSearch.Dispose();
            positionsForSearch.Dispose();
            velocitiesForSearch.Dispose();

            positionsInCell.Dispose();
            velocitiesInCell.Dispose();

            tmpForceInCell.Dispose();
        }
        static void ComputeInteraction(Entity entity, float3 pos0, float3 fwd0,
                                       float distThresh, float prodThresh,
                                       NativeArray<Entity> neighborsFromCell,
                                       NativeArray<Translation> positionsForSearch,
                                       NativeArray<Velocity> velocitiesForSearch,
                                       ref TmpForceValue tmp_value)
        {
            float r2_search = distThresh * distThresh;
            for (int i = 0; i < neighborsFromCell.Length; i++)
            {
                var neighbor = neighborsFromCell[i];
                if (entity == neighbor) continue;

                float3 pos1 = positionsForSearch[i].Value;
                var to = pos1 - pos0;
                var dist2 = math.lengthsq(to);

                if (dist2 < r2_search)
                {
                    var dir = math.normalize(to);
                    var prod = math.dot(dir, fwd0);
                    if (prod > prodThresh)
                    {
                        //--- alignment
                        tmp_value.Alignment += velocitiesForSearch[i].Value;

                        //--- cohesion
                        tmp_value.Cohesion += positionsForSearch[i].Value;

                        //--- separation
                        tmp_value.Separation += (-dir);

                        //--- count
                        tmp_value.Count++;
                    }
                }
            }
        }
    }

    protected override void OnUpdate()
    {

        //--- search neighbors
        var cellIndex = CellIndex_Bootstrap.Instance.HashCellIndex;
        var cell_list = new NativeList<PosIndex>(Allocator.TempJob);
        cellIndex.GetContainsIndexList(cell_list);

        var computeInteractionJob = new NeighborDetectionJob_Combined()
        {
            prodThresh = math.cos(math.radians(Bootstrap.Param.neighborFov)),
            distThresh = Bootstrap.Param.neighborDistance,

            alignmentWeight = Bootstrap.Param.alignmentWeight,
            cohesionWeight = Bootstrap.Param.cohesionWeight,
            separationWeight = Bootstrap.Param.separationWeight,

            positionFromGrovalEntity = GetComponentDataFromEntity<Translation>(true),
            velocityFromGrovalEntity = GetComponentDataFromEntity<Velocity>(true),

            cellIndex = cellIndex,
            cellList = cell_list,

            accelerationFromGrobalEntity = GetComponentDataFromEntity<Acceleration>(),
        };

        Dependency = computeInteractionJob.Schedule(cell_list.Length,
                                                    CellIndex_Bootstrap.Instance.CellBatchSize,
                                                    Dependency);
        cell_list.Dispose(Dependency);

        CellIndex_Bootstrap.Instance.SetJobHandle("NeighborDetectionSystem_CellIndex_Combined", Dependency);
    }
}

static internal class NeighborDetectionFunc
{
    internal static void SearchNeighbors(Entity entity, float3 pos0, float3 fwd0,
                                         float distThresh, float prodThresh,
                                         NativeArray<Entity> neighborsFromCell,
                                         NativeArray<Translation> positionsForSearch,
                                         DynamicBuffer<NeighborsEntityBuffer> neighbors)
    {
        float r2_search = distThresh * distThresh;
        for (int i = 0; i < neighborsFromCell.Length; i++)
        {
            var target = neighborsFromCell[i];
            if (entity == target) continue;

            float3 pos1 = positionsForSearch[i].Value;
            var to = pos1 - pos0;
            var dist2 = math.lengthsq(to);

            if (dist2 < r2_search)
            {
                var dir = math.normalize(to);
                var prod = math.dot(dir, fwd0);
                if (prod > prodThresh)
                {
                    neighbors.Add(new NeighborsEntityBuffer { entity = target });
                }
            }
        }
    }
    internal static void SearchNeighbors(Entity entity, float3 pos0, float3 fwd0,
                                         float distThresh, float prodThresh,
                                         NativeArray<Entity> neighborsFromCell,
                                         NativeArray<Translation> positionsForSearch,
                                         int sort_key,
                                         EntityCommandBuffer.ParallelWriter ecb)
    {
        float r2_search = distThresh * distThresh;
        for (int i = 0; i < neighborsFromCell.Length; i++)
        {
            var neighbor = neighborsFromCell[i];
            if (entity == neighbor) continue;

            float3 pos1 = positionsForSearch[i].Value;
            var to = pos1 - pos0;
            var dist2 = math.lengthsq(to);

            if (dist2 < r2_search)
            {
                var dir = math.normalize(to);
                var prod = math.dot(dir, fwd0);
                if (prod > prodThresh)
                {
                    ecb.AppendToBuffer(sort_key, entity, new NeighborsEntityBuffer { entity = neighbor});
                }
            }
        }
    }
}