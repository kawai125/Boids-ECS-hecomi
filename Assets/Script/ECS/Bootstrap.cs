using UnityEngine;

using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

public class Bootstrap : MonoBehaviour
{
    public static Bootstrap Instance { get; private set; }
    public static Param Param {  get { return Instance.param; } }

    //[SerializeField] Vector3 boidScale = new Vector3(1.0f, 1.0f, 1.0f);
    [SerializeField] public float boidScale = 1.0f;
    [SerializeField] public Param param;

    [SerializeField] GameObject prefab_obj;
    private Entity prefab_entity;

    // UI interface
    [SerializeField] UI_controller ui_input;

    private int n_boid;
    private Unity.Mathematics.Random random;

    void Awake()
    {
        Instance = this;
    }

    public void Start()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        var manager = world.EntityManager;

        // convert prefab_obj -> prefab_entity
        prefab_entity = GameObjectConversionUtility.ConvertGameObjectHierarchy(
            prefab_obj,
            GameObjectConversionSettings.FromWorld(world, null)
        );

        // add user defined component
        manager.AddComponent<BoidPrefabType>(prefab_entity);
        manager.AddComponent<Scale>(prefab_entity);
        manager.AddComponent<Velocity>(prefab_entity);
        manager.AddComponent<Acceleration>(prefab_entity);
        manager.AddComponent<NeighborsEntityBuffer>(prefab_entity);

        n_boid = 0;

        // initialize random
        this.random = new Unity.Mathematics.Random(853);
    }
    public void OnDestroy()
    {
        
    }

    void UpdateBoidNum(int n_tgt)
    {
        if (n_tgt < 0) return;

        var manager = World.DefaultGameObjectInjectionWorld.EntityManager;

        int n_diff = n_tgt - n_boid;

        if (n_diff > 0)
        {
            Debug.Log($"update boids num: add {n_diff} boids.");

            var scale = this.boidScale;
            var initSpeed = this.param.initSpeed;

            for (int i = 0; i < n_diff; i++)
            {
                var entity = manager.Instantiate(prefab_entity);

                manager.RemoveComponent<BoidPrefabType>(entity);
                manager.AddComponent<BoidType>(entity);

                manager.SetComponentData(entity, new Translation { Value = this.random.NextFloat3(1f) });
                manager.SetComponentData(entity, new Rotation { Value = quaternion.identity });
                manager.SetComponentData(entity, new Scale { Value = scale });
                manager.SetComponentData(entity, new Velocity { Value = this.random.NextFloat3Direction() * initSpeed });
                manager.SetComponentData(entity, new Acceleration { Value = float3.zero });
            }
        }
        if (n_diff < 0)
        {
            int n_delete = -n_diff;

            Debug.Log($"update boids num: remove {n_delete} boids.");

            var entity_query = manager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<BoidType>()
                }
            });
            var entities = entity_query.ToEntityArray(Allocator.Temp);
            manager.DestroyEntity(new NativeSlice<Entity>(entities, n_tgt));

            entities.Dispose();
        }

        n_boid = n_tgt;
    }

    void Update()
    {
        UpdateBoidNum(ui_input.boidCount);
    }

    void OnDrawGizmos()
    {
        if (!param) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one * param.wallScale);
    }
}
