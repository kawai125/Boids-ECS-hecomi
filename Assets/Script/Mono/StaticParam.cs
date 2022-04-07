using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StaticParam : MonoBehaviour
{
    private static StaticParam Instance { get; set; }
    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        WallScale = Define.InitialWallScale;
        NeighborSearchRange = Define.InitialNeighborSearchRange;
        NeighborSearchAngle = Define.InitialNeighborSearchAngle;
    }

    public static float WallScale { get; set; }
    public static float NeighborSearchRange { get; set; }
    public static float NeighborSearchAngle { get; set; }
}
