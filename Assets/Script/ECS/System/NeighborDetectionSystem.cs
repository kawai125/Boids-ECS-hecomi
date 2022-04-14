//#define NEIGHBOR_DETECTION_VALIDATE_WITH_DIRECT

using System;
using UnityEngine;

using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;

using HashCellIndex;


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
        public ComponentDataFromEntity<Translation> positionFromGlobalEntity;

        [DeallocateOnJobCompletion]
        [ReadOnly]
        public NativeArray<Entity> entitiesGlobal;
    }

    protected override void OnUpdate()
    {
        var common_data = new NeighborDetectionDataContainer
        {
            prodThresh = math.cos(math.radians(BoidParams_Bootstrap.Param.neighborSearchAngle)),
            distThresh = BoidParams_Bootstrap.Param.neighborSearchRange,

            positionFromGlobalEntity = GetComponentDataFromEntity<Translation>(true),

            entitiesGlobal = query.ToEntityArray(Allocator.TempJob),
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
            for (int i = 0; i < common_data.entitiesGlobal.Length; i++)
            {
                var target = common_data.entitiesGlobal[i];
                if (entity == target) continue;

                float3 pos1 = common_data.positionFromGlobalEntity[target].Value;
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
        var cellIndex = CellIndex_Bootstrap.HashCellIndex;
        CellIndex_Bootstrap.InitDomain(Bootstrap.BoidsCount);

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

        CellIndex_Bootstrap.SetJobHandle(this.GetType().Name, Dependency);

        Dependency.Complete();
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
        public ComponentDataFromEntity<Translation> positionFromGlobalEntity;

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
            prodThresh = math.cos(math.radians(BoidParams_Bootstrap.Param.neighborSearchAngle)),
            distThresh = BoidParams_Bootstrap.Param.neighborSearchRange,

            positionFromGlobalEntity = GetComponentDataFromEntity<Translation>(true),

            cellIndex = CellIndex_Bootstrap.HashCellIndex,
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

            var positionsForSearch = ComponentDataUtility.GatherComponentData(common_data.positionFromGlobalEntity,
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

        CellIndex_Bootstrap.SetJobHandle(this.GetType().Name, Dependency);
    }
}

[UpdateInGroup(typeof(NeighborDetectionSystemGroup))]
public partial class NeighborDetectionSystem_CellIndex_Cell_NeighborList : SystemBase
{
    private NativeList<PosIndex> cell_list;

    protected override void OnCreate()
    {
        base.OnCreate();

        RequireForUpdate(
            GetEntityQuery(ComponentType.ReadOnly<BoidType>(),
                           ComponentType.ReadOnly<Translation>(),
                           ComponentType.ReadOnly<Velocity>(),
                           ComponentType.ReadWrite<NeighborsEntityBuffer>(),
                           ComponentType.ReadOnly<Tag_ComputeNeighbors_CellIndex_Cell_NeighborList>()));

        cell_list = new NativeList<PosIndex>(Allocator.Persistent);
    }
    protected override void OnDestroy()
    {
        base.OnDestroy();

        cell_list.Dispose();
    }

    [BurstCompile]
    private struct NeighborDetectionJob_Cell_NeighborList : IJobParallelFor
    {
        [ReadOnly] public float prodThresh;
        [ReadOnly] public float distThresh;

        [ReadOnly]
        public ComponentDataFromEntity<Translation> positionFromGlobalEntity;
        [ReadOnly]
        public ComponentDataFromEntity<Velocity> velocityFromGlobalEntity;

        [ReadOnly]
        public HashCellIndex<Entity> cellIndex;

        [ReadOnly]
        public NativeArray<PosIndex> cellList;

        [NativeDisableContainerSafetyRestriction]
        public BufferFromEntity<NeighborsEntityBuffer> neighborsFromGlobalEntity;

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
            var positionsInCell = ComponentDataUtility.GatherComponentData(positionFromGlobalEntity, entitiesInCell, Allocator.Temp);
            var velocitiesInCell = ComponentDataUtility.GatherComponentData(velocityFromGlobalEntity, entitiesInCell, Allocator.Temp);

            //--- gather data for search
            var positionsForSearch = ComponentDataUtility.GatherComponentData(positionFromGlobalEntity, entitiesForSearch, Allocator.Temp);

            //--- search neighbors by cell to neighbors from cellIndex
            for (int i_entity = 0; i_entity < entitiesInCell.Length; i_entity++)
            {
                var neighbors = neighborsFromGlobalEntity[entitiesInCell[i_entity]];
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
        var cellIndex = CellIndex_Bootstrap.HashCellIndex;
        cellIndex.GetContainsIndexList(cell_list);

        var neighborDetectionJob = new NeighborDetectionJob_Cell_NeighborList()
        {
            prodThresh = math.cos(math.radians(BoidParams_Bootstrap.Param.neighborSearchAngle)),
            distThresh = BoidParams_Bootstrap.Param.neighborSearchRange,

            positionFromGlobalEntity = GetComponentDataFromEntity<Translation>(true),
            velocityFromGlobalEntity = GetComponentDataFromEntity<Velocity>(true),

            cellIndex = cellIndex,
            cellList = cell_list,

            neighborsFromGlobalEntity = GetBufferFromEntity<NeighborsEntityBuffer>(),
        };
        Dependency = neighborDetectionJob.Schedule(cell_list.Length,
                                                   CellIndex_Bootstrap.UpdateBatchSize(cell_list.Length),
                                                   Dependency);

        CellIndex_Bootstrap.SetJobHandle(this.GetType().Name, Dependency);
    }
}

[UpdateInGroup(typeof(NeighborDetectionSystemGroup))]
public partial class NeighborDetectionSystem_CellIndex_Cell_Cell : SystemBase
{
    private NativeList<PosIndex> cell_list;

    protected override void OnCreate()
    {
        base.OnCreate();

        RequireForUpdate(
            GetEntityQuery(ComponentType.ReadOnly<BoidType>(),
                           ComponentType.ReadOnly<Translation>(),
                           ComponentType.ReadOnly<Velocity>(),
                           ComponentType.ReadWrite<NeighborsEntityBuffer>(),
                           ComponentType.ReadOnly<Tag_ComputeNeighbors_CellIndex_Cell_Cell>()));

        cell_list = new NativeList<PosIndex>(Allocator.Persistent);
    }
    protected override void OnDestroy()
    {
        base.OnDestroy();

        cell_list.Dispose();
    }

    [BurstCompile]
    private struct NeighborDetectionJob_Cell_Cell : IJobParallelFor
    {
        [ReadOnly] public float prodThresh;
        [ReadOnly] public float distThresh;

        [ReadOnly]
        public ComponentDataFromEntity<Translation> positionFromGlobalEntity;
        [ReadOnly]
        public ComponentDataFromEntity<Velocity> velocityFromGlobalEntity;

        [ReadOnly]
        public HashCellIndex<Entity> cellIndex;

        [ReadOnly]
        public NativeArray<PosIndex> cellList;

        [NativeDisableContainerSafetyRestriction]
        public BufferFromEntity<NeighborsEntityBuffer> neighborsFromGlobalEntity;

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

            for(int j = 0; j < entitiesInCell.Length; j++)
            {
                neighborsFromGlobalEntity[entitiesInCell[j]].Clear();
            }

            var searchIndexList = new NativeList<PosIndex>(64, Allocator.Temp);
            var entitiesForSearch = new NativeList<Entity>(64, Allocator.Temp);
            var positionsForSearch = new NativeList<Translation>(64, Allocator.Temp);

            //--- gather data in cell
            var positionsInCell = ComponentDataUtility.GatherComponentData(positionFromGlobalEntity, entitiesInCell, Allocator.Temp);
            var velocitiesInCell = ComponentDataUtility.GatherComponentData(velocityFromGlobalEntity, entitiesInCell, Allocator.Temp);

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
                    positionsForSearch[i] = positionFromGlobalEntity[entitiesForSearch[i]];
                }

                //--- detect neighbors
                for (int i_entity = 0; i_entity < entitiesInCell.Length; i_entity++)
                {
                    NeighborDetectionFunc.SearchNeighbors(entitiesInCell[i_entity],
                                                          positionsInCell[i_entity].Value,
                                                          math.normalize(velocitiesInCell[i_entity].Value),
                                                          distThresh,
                                                          prodThresh,
                                                          entitiesForSearch,
                                                          positionsForSearch,
                                                          neighborsFromGlobalEntity[entitiesInCell[i_entity]]);
                }
            }

            searchIndexList.Dispose();
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
        var cellIndex = CellIndex_Bootstrap.HashCellIndex;
        cellIndex.GetContainsIndexList(cell_list);

        var neighborDetectionJob = new NeighborDetectionJob_Cell_Cell()
        {
            prodThresh = math.cos(math.radians(BoidParams_Bootstrap.Param.neighborSearchAngle)),
            distThresh = BoidParams_Bootstrap.Param.neighborSearchRange,

            positionFromGlobalEntity = GetComponentDataFromEntity<Translation>(true),
            velocityFromGlobalEntity = GetComponentDataFromEntity<Velocity>(true),

            cellIndex = cellIndex,
            cellList = cell_list,

            neighborsFromGlobalEntity = GetBufferFromEntity<NeighborsEntityBuffer>(),
        };
        Dependency = neighborDetectionJob.Schedule(cell_list.Length,
                                                   CellIndex_Bootstrap.UpdateBatchSize(cell_list.Length),
                                                   Dependency);

        CellIndex_Bootstrap.SetJobHandle(this.GetType().Name, Dependency);
    }
}

[UpdateInGroup(typeof(NeighborDetectionSystemGroup))]
public partial class NeighborDetectionSystem_CellIndex_Combined_CNL : SystemBase
{
    private NativeList<PosIndex> cell_list;

    protected override void OnCreate()
    {
        base.OnCreate();

        RequireForUpdate(
            GetEntityQuery(ComponentType.ReadOnly<BoidType>(),
                           ComponentType.ReadOnly<Translation>(),
                           ComponentType.ReadOnly<Velocity>(),
                           ComponentType.ReadWrite<Acceleration>(),
                           ComponentType.ReadOnly<Tag_ComputeNeighbors_CellIndex_Combined_CNL>()));

        cell_list = new NativeList<PosIndex>(Allocator.Persistent);
    }
    protected override void OnDestroy()
    {
        base.OnDestroy();

        cell_list.Dispose();
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
        public ComponentDataFromEntity<Translation> positionFromGlobalEntity;
        [ReadOnly]
        public ComponentDataFromEntity<Velocity> velocityFromGlobalEntity;

        [ReadOnly]
        public HashCellIndex<Entity> cellIndex;

        [ReadOnly]
        public NativeArray<PosIndex> cellList;

        [NativeDisableContainerSafetyRestriction]
        public ComponentDataFromEntity<Acceleration> accelerationFromGlobalEntity;

        private struct TmpForceValue
        {
            public float3 Alignment, Cohesion, Separation;
            public int Count;

            public void Clear()
            {
                Alignment = 0f;
                Cohesion = 0f;
                Separation = 0f;
                Count = 0;
            }

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

            if (entitiesInCell.Length <= 0)
            {
                entitiesInCell.Dispose();
                return;
            }

            //--- gather data in cell
            var positionsInCell = ComponentDataUtility.GatherComponentData(positionFromGlobalEntity, entitiesInCell, Allocator.Temp);
            var velocitiesInCell = ComponentDataUtility.GatherComponentData(velocityFromGlobalEntity, entitiesInCell, Allocator.Temp);

            var entitiesForSearch = new NativeList<Entity>(64, Allocator.Temp);
            cellIndex.GetNeighborList(index, distThresh, Boundary.Open, entitiesForSearch);

            //--- gather data for search
            var positionsForSearch = ComponentDataUtility.GatherComponentData(positionFromGlobalEntity, entitiesForSearch, Allocator.Temp);
            var velocitiesForSearch = ComponentDataUtility.GatherComponentData(velocityFromGlobalEntity, entitiesForSearch, Allocator.Temp);

            //--- result buffer
            var tmpForceInCell = new NativeArray<TmpForceValue>(entitiesInCell.Length, Allocator.Temp);

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

            //--- writeback buffered acceleration
            for (int i = 0; i < entitiesInCell.Length; i++)
            {
                var entity = entitiesInCell[i];
                var acc = accelerationFromGlobalEntity[entity].Value;
                acc += tmpForceInCell[i].GetAccel(positionsInCell[i], velocitiesInCell[i],
                                                  alignmentWeight, cohesionWeight, separationWeight);
                accelerationFromGlobalEntity[entity] = new Acceleration { Value = acc };
            }

            entitiesInCell.Dispose();
            positionsInCell.Dispose();
            velocitiesInCell.Dispose();

            entitiesForSearch.Dispose();
            positionsForSearch.Dispose();
            velocitiesForSearch.Dispose();

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
        var cellIndex = CellIndex_Bootstrap.HashCellIndex;
        cellIndex.GetContainsIndexList(cell_list);

        var computeInteractionJob = new NeighborDetectionJob_Combined()
        {
            prodThresh = math.cos(math.radians(BoidParams_Bootstrap.Param.neighborSearchAngle)),
            distThresh = BoidParams_Bootstrap.Param.neighborSearchRange,

            alignmentWeight = BoidParams_Bootstrap.Param.alignmentWeight,
            cohesionWeight = BoidParams_Bootstrap.Param.cohesionWeight,
            separationWeight = BoidParams_Bootstrap.Param.separationWeight,

            positionFromGlobalEntity = GetComponentDataFromEntity<Translation>(true),
            velocityFromGlobalEntity = GetComponentDataFromEntity<Velocity>(true),

            cellIndex = cellIndex,
            cellList = cell_list,

            accelerationFromGlobalEntity = GetComponentDataFromEntity<Acceleration>(),
        };

        Dependency = computeInteractionJob.Schedule(cell_list.Length,
                                                    CellIndex_Bootstrap.UpdateBatchSize(cell_list.Length),
                                                    Dependency);

        CellIndex_Bootstrap.SetJobHandle(this.GetType().Name, Dependency);
    }
}

[UpdateInGroup(typeof(NeighborDetectionSystemGroup))]
public partial class NeighborDetectionSystem_CellIndex_Combined_CC : SystemBase
{
    private NativeList<PosIndex> cell_list;

    protected override void OnCreate()
    {
        base.OnCreate();

        RequireForUpdate(
            GetEntityQuery(ComponentType.ReadOnly<BoidType>(),
                           ComponentType.ReadOnly<Translation>(),
                           ComponentType.ReadOnly<Velocity>(),
                           ComponentType.ReadWrite<Acceleration>(),
                           ComponentType.ReadOnly<Tag_ComputeNeighbors_CellIndex_Combined_CC>()));

        cell_list = new NativeList<PosIndex>(Allocator.Persistent);
    }
    protected override void OnDestroy()
    {
        base.OnDestroy();

        cell_list.Dispose();
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
        public ComponentDataFromEntity<Translation> positionFromGlobalEntity;
        [ReadOnly]
        public ComponentDataFromEntity<Velocity> velocityFromGlobalEntity;

        [ReadOnly]
        public HashCellIndex<Entity> cellIndex;

        [ReadOnly]
        public NativeArray<PosIndex> cellList;

        [NativeDisableContainerSafetyRestriction]
        public ComponentDataFromEntity<Acceleration> accelerationFromGlobalEntity;

        private struct TmpForceValue
        {
            public float3 Alignment, Cohesion, Separation;
            public int Count;

            public void Clear()
            {
                Alignment = 0f;
                Cohesion = 0f;
                Separation = 0f;
                Count = 0;
            }

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
            var positionsInCell = ComponentDataUtility.GatherComponentData(positionFromGlobalEntity, entitiesInCell, Allocator.Temp);
            var velocitiesInCell = ComponentDataUtility.GatherComponentData(velocityFromGlobalEntity, entitiesInCell, Allocator.Temp);

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
                    positionsForSearch[i_entity] = positionFromGlobalEntity[entity];
                    velocitiesForSearch[i_entity] = velocityFromGlobalEntity[entity];
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
                var acc = accelerationFromGlobalEntity[entity].Value;
                acc += tmpForceInCell[i].GetAccel(positionsInCell[i], velocitiesInCell[i],
                                                  alignmentWeight, cohesionWeight, separationWeight);
                accelerationFromGlobalEntity[entity] = new Acceleration { Value = acc };
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
        var cellIndex = CellIndex_Bootstrap.HashCellIndex;
        cellIndex.GetContainsIndexList(cell_list);

        var computeInteractionJob = new NeighborDetectionJob_Combined()
        {
            prodThresh = math.cos(math.radians(BoidParams_Bootstrap.Param.neighborSearchAngle)),
            distThresh = BoidParams_Bootstrap.Param.neighborSearchRange,

            alignmentWeight = BoidParams_Bootstrap.Param.alignmentWeight,
            cohesionWeight = BoidParams_Bootstrap.Param.cohesionWeight,
            separationWeight = BoidParams_Bootstrap.Param.separationWeight,

            positionFromGlobalEntity = GetComponentDataFromEntity<Translation>(true),
            velocityFromGlobalEntity = GetComponentDataFromEntity<Velocity>(true),

            cellIndex = cellIndex,
            cellList = cell_list,

            accelerationFromGlobalEntity = GetComponentDataFromEntity<Acceleration>(),
        };

        Dependency = computeInteractionJob.Schedule(cell_list.Length,
                                                    CellIndex_Bootstrap.UpdateBatchSize(cell_list.Length),
                                                    Dependency);

        CellIndex_Bootstrap.SetJobHandle(this.GetType().Name, Dependency);
    }
}

[UpdateInGroup(typeof(NeighborDetectionSystemGroup))]
public partial class NeighborDetectionSystem_CellIndex_MergedCell_NeighborList : SystemBase
{
    private NativeList<MergedPosIndex> cell_list;

    protected override void OnCreate()
    {
        base.OnCreate();

        RequireForUpdate(
            GetEntityQuery(ComponentType.ReadOnly<BoidType>(),
                           ComponentType.ReadOnly<Translation>(),
                           ComponentType.ReadOnly<Velocity>(),
                           ComponentType.ReadWrite<NeighborsEntityBuffer>(),
                           ComponentType.ReadOnly<Tag_ComputeNeighbors_CellIndex_MergedCell_NL>()));

        cell_list = new NativeList<MergedPosIndex>(Allocator.Persistent);
    }
    protected override void OnDestroy()
    {
        base.OnDestroy();

        cell_list.Dispose();
    }

    [BurstCompile]
    private struct NeighborDetectionJob_Cell_NeighborList : IJobParallelFor
    {
        [ReadOnly] public float prodThresh;
        [ReadOnly] public float distThresh;

        [ReadOnly]
        public ComponentDataFromEntity<Translation> positionFromGlobalEntity;
        [ReadOnly]
        public ComponentDataFromEntity<Velocity> velocityFromGlobalEntity;

        [ReadOnly]
        public HashCellIndex<Entity> cellIndex;

        [ReadOnly]
        public NativeArray<MergedPosIndex> cellList;

        [NativeDisableContainerSafetyRestriction]
        public BufferFromEntity<NeighborsEntityBuffer> neighborsFromGlobalEntity;

        public void Execute() { for (int i = 0; i < cellList.Length; i++) Execute(i); }
        public void Execute(int i_job)
        {
            var merged_cell_index = cellList[i_job];

            //--- gather data
            var entitiesForMergedSearch = cellIndex.GetMergedNeighborList(merged_cell_index,
                                                                          distThresh,
                                                                          Boundary.Open,
                                                                          64, Allocator.Temp);

            var positionsForMergedSearch = MergedCellUtility.GatherComponentData(positionFromGlobalEntity,
                                                                                 entitiesForMergedSearch,
                                                                                 Allocator.Temp);

            var entitiesInMergedCell = entitiesForMergedSearch.ExtractMergedCell(Allocator.Temp);
            var velocitiesInMergedCell = MergedCellUtility.GatherComponentData(velocityFromGlobalEntity,
                                                                               entitiesInMergedCell,
                                                                               Allocator.Temp);

            //--- cell iteration
            for(int i_cell = 0; i_cell < entitiesInMergedCell.Length; i_cell++)
            {
                var entitiesInCell = entitiesInMergedCell.GetCell(i_cell);
                var positionsInCell = positionsForMergedSearch.GetCell(i_cell);
                var velocitiesInCell = velocitiesInMergedCell.GetCell(i_cell);

                var entitiesForSearch = entitiesForMergedSearch.GetNeighbors(i_cell);
                var positionsForSearch = positionsForMergedSearch.GetNeighbors(i_cell);

                //--- search neighbors by cell to neighbors from cellIndex
                for (int i_entity = 0; i_entity < entitiesInCell.Length; i_entity++)
                {
                    var neighbors = neighborsFromGlobalEntity[entitiesInCell[i_entity]];
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
            }

            entitiesForMergedSearch.Dispose();
            positionsForMergedSearch.Dispose();

            entitiesInMergedCell.Dispose();
            velocitiesInMergedCell.Dispose();
        }
    }

    protected override void OnUpdate()
    {
        //--- search neighbors
        var cellIndex = CellIndex_Bootstrap.HashCellIndex;
        cellIndex.GetContainsMergedCellList(CellIndex_Bootstrap.CellMergeSize, cell_list);

        var neighborDetectionJob = new NeighborDetectionJob_Cell_NeighborList()
        {
            prodThresh = math.cos(math.radians(BoidParams_Bootstrap.Param.neighborSearchAngle)),
            distThresh = BoidParams_Bootstrap.Param.neighborSearchRange,

            positionFromGlobalEntity = GetComponentDataFromEntity<Translation>(true),
            velocityFromGlobalEntity = GetComponentDataFromEntity<Velocity>(true),

            cellIndex = cellIndex,
            cellList = cell_list,

            neighborsFromGlobalEntity = GetBufferFromEntity<NeighborsEntityBuffer>(),
        };
        Dependency = neighborDetectionJob.Schedule(cell_list.Length,
                                                   CellIndex_Bootstrap.UpdateBatchSize(cell_list.Length),
                                                   Dependency);

        CellIndex_Bootstrap.SetJobHandle(this.GetType().Name, Dependency);
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
}



#if NEIGHBOR_DETECTION_VALIDATE_WITH_DIRECT
[UpdateAfter(typeof(NeighborDetectionSystemGroup))]
public partial class NeighborValidateSystem : SystemBase
{
    private EntityQuery query;

    protected override void OnCreate()
    {
        base.OnCreate();

        query = GetEntityQuery(ComponentType.ReadOnly<BoidType>(),
                               ComponentType.ReadOnly<Translation>(),
                               ComponentType.ReadOnly<Velocity>(),
                               ComponentType.ReadOnly<NeighborsEntityBuffer>());
    }

    private struct NeighborDetectionDataContainer
    {
        [ReadOnly] public float prodThresh;
        [ReadOnly] public float distThresh;

        [ReadOnly]
        public ComponentDataFromEntity<Translation> positionFromGlobalEntity;

        [ReadOnly]
        public HashCellIndex<Entity> cell_index;

        [DeallocateOnJobCompletion]
        [ReadOnly]
        public NativeArray<Entity> entitiesGlobal;
    }

    protected override void OnUpdate()
    {
        var common_data = new NeighborDetectionDataContainer
        {
            prodThresh = math.cos(math.radians(BoidParams_Bootstrap.Param.neighborSearchAngle)),
            distThresh = BoidParams_Bootstrap.Param.neighborSearchRange,

            positionFromGlobalEntity = GetComponentDataFromEntity<Translation>(true),

            cell_index = CellIndex_Bootstrap.HashCellIndex,

            entitiesGlobal = query.ToEntityArray(Allocator.TempJob),
        };

        // args for Entities.ForEach() must be ordered as (Entity), (ref args), and (in args).
        Dependency = Entities.
            WithName("ValidateNeighborsJob").
            WithAll<BoidType>().
            WithNone<Tag_ComputeNeighbors_Direct>().
            WithBurst().
            ForEach(
        (Entity entity, in DynamicBuffer<NeighborsEntityBuffer> tgt_neighbors, in Translation position, in Velocity velocity) =>
        {
            if (!common_data.cell_index.Box.IsInside(position.Value))
                return; // HashCellIndex cannot treat outside of box.

            ValidateNeighborList(entity, tgt_neighbors, position, velocity,
                                 common_data.distThresh, common_data.prodThresh,
                                 common_data.entitiesGlobal,
                                 common_data.positionFromGlobalEntity,
                                 common_data.cell_index);
        }).ScheduleParallel(Dependency);

        CellIndex_Bootstrap.SetJobHandle(this.GetType().Name, Dependency);
    }

    public static void ValidateNeighborList(Entity entity,
                                            in DynamicBuffer<NeighborsEntityBuffer> tgt_neighbors,
                                            in Translation position,
                                            in Velocity velocity,
                                            float distThresh, float prodThresh,
                                            in NativeArray<Entity> entitiesGlobal,
                                            in ComponentDataFromEntity<Translation> positionFromGlobalEntity,
                                            in HashCellIndex<Entity> cell_index)
    {
        var ref_neighbors = new NativeList<NeighborsEntityBuffer>(tgt_neighbors.Length, Allocator.Temp);

        float3 pos0 = position.Value;
        float3 fwd0 = math.normalize(velocity.Value);

        float r2_search = distThresh * distThresh;
        for (int i = 0; i < entitiesGlobal.Length; i++)
        {
            var target = entitiesGlobal[i];
            if (entity == target) continue;

            float3 pos1 = positionFromGlobalEntity[target].Value;
            var to = pos1 - pos0;
            var dist2 = math.lengthsq(to);

            if (dist2 < r2_search)
            {
                var dir = math.normalize(to);
                var prod = math.dot(dir, fwd0);
                if (prod > prodThresh)
                {
                    ref_neighbors.Add(new NeighborsEntityBuffer { entity = target });
                }
            }
        }

        //--- check result
        bool flag = true;
        var sb = new System.Text.StringBuilder();
        if (tgt_neighbors.Length != ref_neighbors.Length)
        {
            flag = false;
            sb.Append($"a bug found. tgt.Length={tgt_neighbors.Length}, ref.Length={ref_neighbors.Length}\n");
        }

        for (int i = 0; i < ref_neighbors.Length; i++)
        {
            if (!tgt_neighbors.AsNativeArray().Contains(ref_neighbors[i]))
            {
                flag = false;
                var i_entity = ref_neighbors[i].entity;
                var pos = positionFromGlobalEntity[i_entity].Value;
                sb.Append($"ref[{i}]={i_entity} was not found in tgt. pos={pos}, index={cell_index.GetIndex(pos)}\n");
            }
        }
        {
            var pos = positionFromGlobalEntity[entity].Value;
            sb.Append($"\nfor Entity[{entity}], pos={pos}, index={cell_index.GetIndex(pos)}\n");
        }

        sb.Append("\n");
        for (int i = 0; i < ref_neighbors.Length; i++)
        {
            var pos = positionFromGlobalEntity[ref_neighbors[i].entity].Value;
            sb.Append($" ref[{i}], pos={pos}, index={cell_index.GetIndex(pos)}\n");
        }

        sb.Append("\n");
        for (int i = 0; i < tgt_neighbors.Length; i++)
        {
            var pos = positionFromGlobalEntity[tgt_neighbors[i].entity].Value;
            sb.Append($" tgt[{i}], pos={pos}, index={cell_index.GetIndex(pos)}\n");
        }

        ref_neighbors.Dispose();

        if (!flag) throw new InvalidProgramException(sb.ToString());
    }
}
#endif
