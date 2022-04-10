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
    }

    public static float WallScale { get; set; }
}
