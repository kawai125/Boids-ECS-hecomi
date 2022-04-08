using UnityEngine;
using UnityEditor;

[CreateAssetMenu(menuName = "Boid/Param")]
public class Param : ScriptableObject
{
    public float initSpeed        = 0.5f;
    public float minSpeed         = 2f;
    public float maxSpeed         = 5;

    public float alignmentWeight  = 2f;
    public float separationWeight = 2f;
    public float cohesionWeight   = 3f;

    public float wallDistance     = 3f;
    public float wallWeight       = 1f;
}

public readonly struct Define
{
    public const int InitialBoidsNum = 100;
    public const float InitialWallScale = 10f;

    public const float InitialNeighborSearchRange = 1.2f;
    public const float InitialNeighborSearchAngle = 90f;

    public const float InitialVortexIntensity = 1f;

    public const float InitialCellIndexRangeCoef = 1.5f;

    public const float InitialDensityForBoids = 0.6f;
    public const float InitialTargetFPS = 60f;
}


