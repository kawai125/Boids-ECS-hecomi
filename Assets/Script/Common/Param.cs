using UnityEngine;
using UnityEditor;

[CreateAssetMenu(menuName = "Boid/Param")]
public class Param : ScriptableObject
{
    public float initSpeed        = 0.5f;
    public float minSpeed         = 2f;
    public float maxSpeed         = 5;

    public float neighborSearchRange = 1.2f;
    public float neighborSearchAngle = 90f;

    public float alignmentWeight  = 2f;
    public float separationWeight = 2f;
    public float cohesionWeight   = 3f;

    public float wallDistance     = 3f;
    public float wallWeight       = 1f;
}

public readonly struct InitialBoidsParam
{
    public const float initSpeed = 3f;
    public const float minSpeed  = 2f;
    public const float maxSpeed  = 7;

    public const float neighborSearchRange = 1.2f;
    public const float neighborSearchAngle = 90f;

    public const float alignmentWeight  = 1.5f;
    public const float cohesionWeight   = 3f;
    public const float separationWeight = 9f;

    public const float wallDistance = 2f;
    public const float wallWeight   = 1f;
}

public readonly struct Define
{
    public const int InitialBoidsNum = 100;
    public const float InitialWallScale = 10f;

    public const float InitialVortexIntensity = 1f;

    public const float InitialCellIndexRangeCoef = 1.5f;
    public const int InitialCellMergeSize = 2;

    public const float InitialDensityForBoids = 0.4f;
    public const float InitialTargetFPS = 60f;
}


