using System.Collections;
using System.Collections.Generic;

using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Mathematics;

[UpdateInGroup(typeof(ManagerSystemGroup))]
public partial class SpawnerAndRemoveSystem : SystemBase
{
    private EntityQuery _boids_query;
    private Random _random;

    protected override void OnCreate()
    {
        base.OnCreate();

        _boids_query = EntityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new[]
                {
                    ComponentType.ReadOnly<BoidType>()
                },
            None = new[]
                {
                    ComponentType.ReadOnly<Prefab>()
                }
        });

        _random = new Random(853);
    }

    protected override void OnUpdate()
    {
        Dependency.Complete();

        Entities.
            WithStructuralChanges().
            WithoutBurst().
            ForEach(
            (Entity trigger, in BoidsSpawner spawner) =>
            {
                Dependency.Complete();

                if(spawner.n > 0)
                {
                    UnityEngine.Debug.Log($"update boids num: add {spawner.n} boids.");

                    var spawnedEntities = new NativeArray<Entity>(spawner.n, Allocator.Temp);
                    EntityManager.Instantiate(spawner.Prefab, spawnedEntities);

                    float spawn_area = Bootstrap.WallScale * 0.4f;

                    for (int i = 0; i < spawner.n; i++)
                    {
                        var entity = spawnedEntities[i];

                        EntityManager.SetComponentData(entity, new Translation { Value = _random.NextFloat3(-spawn_area, spawn_area) });
                        EntityManager.SetComponentData(entity, new Rotation { Value = quaternion.identity });
                        EntityManager.SetComponentData(entity, new Scale { Value = spawner.scale });
                        EntityManager.SetComponentData(entity, new Velocity { Value = _random.NextFloat3Direction() * spawner.initSpeed });
                        EntityManager.SetComponentData(entity, new Acceleration { Value = float3.zero });
                    }

                    spawnedEntities.Dispose();
                }
                else if(spawner.n < 0)
                {
                    int n_delete = -spawner.n;
                    UnityEngine.Debug.Log($"update boids num: remove {n_delete} boids.");

                    var entities = _boids_query.ToEntityArray(Allocator.Temp);
                    EntityManager.DestroyEntity(new NativeSlice<Entity>(entities, 0, math.abs(n_delete)));
                    entities.Dispose();
                }

                //--- report spawner complete
                Bootstrap.UpdateBoidNumComplete(spawner);

                //--- delete trigger
                EntityManager.DestroyEntity(trigger);

            }).Run();
    }
}
