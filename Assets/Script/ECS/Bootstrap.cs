using System;

using UnityEngine;

using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;


public class Bootstrap : MonoBehaviour
{
    public static Bootstrap Instance { get; private set; }

    public static float WallScale { get; private set; }

    //[SerializeField] Vector3 boidScale = new Vector3(1.0f, 1.0f, 1.0f);
    [SerializeField] private float boidScale = 1.0f;

    [SerializeField] GameObject prefab_obj;
    private Entity prefab_entity;

    // UI interface
    [SerializeField]
    private UI_controller ui_input;

    private int n_boid;

    private EntityManager entity_manager;
    private EntityQuery boids_query;

    private Entity _triggerForBoidsSpawner;
    private Entity _triggerForComputePlanSwitcher;

    void Awake()
    {
        Instance = this;
    }

    public void Start()
    {
        //--- initialize dynamic parameter
        WallScale = Define.InitialWallScale;

        //--- setup managers
        var world = World.DefaultGameObjectInjectionWorld;
        entity_manager = world.EntityManager;

        boids_query = entity_manager.CreateEntityQuery(new EntityQueryDesc
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

        // convert prefab_obj -> prefab_entity
        prefab_entity = GameObjectConversionUtility.ConvertGameObjectHierarchy(
            prefab_obj,
            GameObjectConversionSettings.FromWorld(world, null)
        );

        // add user defined component
        entity_manager.AddComponent<Prefab>(prefab_entity);
        entity_manager.AddComponent<BoidType>(prefab_entity);
        entity_manager.AddComponent<Scale>(prefab_entity);
        entity_manager.AddComponent<Velocity>(prefab_entity);
        entity_manager.AddComponent<Acceleration>(prefab_entity);
        entity_manager.AddComponent<NeighborsEntityBuffer>(prefab_entity);

        entity_manager.AddComponent<Tag_UpdateInteraction>(prefab_entity);
        entity_manager.AddComponent<Tag_ComputeNeighbors_Direct>(prefab_entity);

        n_boid = 0;

        // initialize trigger prefab
        _triggerForBoidsSpawner = entity_manager.CreateEntity(typeof(Prefab), typeof(BoidsSpawner));
        _triggerForComputePlanSwitcher = entity_manager.CreateEntity(typeof(Prefab), typeof(ComputePlanSwicher));
    }
    public void OnDestroy()
    {
        
    }

    public static int BoidsCount { get { return Instance.n_boid; } }

    void UpdateBoidNum(int n_tgt)
    {
        if (n_tgt < 0) return;

        int n_diff = n_tgt - n_boid;

        if (n_diff == 0) return;

        var trigger = entity_manager.Instantiate(_triggerForBoidsSpawner);
        var spawner = new BoidsSpawner
        {
            Prefab = prefab_entity,
            n = n_diff,
            scale = boidScale,
            initSpeed = BoidParams_Bootstrap.Param.initSpeed
        };
        entity_manager.SetComponentData(trigger, spawner);
    }
    public static void UpdateBoidNumComplete(BoidsSpawner spawner)
    {
        Instance.n_boid += spawner.n;
    }

    void Update()
    {
        UpdateBoidNum(ui_input.boidCount);
        WallScale = ui_input.cageScale;
    }

    public void SwitchComputeNeighborsPlan(ComputeNeighborsPlan old_plan, ComputeNeighborsPlan new_plan)
    {
        var trigger = entity_manager.Instantiate(_triggerForComputePlanSwitcher);
        var swapper = new ComputePlanSwicher
        {
            Prefab = prefab_entity,
            Query = boids_query,
            RemoveTarget = old_plan,
            AddTarget = new_plan,
        };
        entity_manager.SetComponentData(trigger, swapper);
    }
}
