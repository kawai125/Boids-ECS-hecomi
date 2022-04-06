using UnityEngine;
using UnityEditor;

[CreateAssetMenu(menuName = "Boid/Param")]
public class Param : ScriptableObject
{
    public float initSpeed        = 0.5f;
    public float minSpeed         = 2f;
    public float maxSpeed         = 5;
    public float neighborDistance = 1.1f;
    public float neighborFov      = 80f;

    public float alignmentWeight  = 2f;
    public float separationWeight = 2f;
    public float cohesionWeight   = 3f;

    public float wallScale        = 5f;
    public float wallDistance     = 3f;
    public float wallWeight       = 1f;

    public float cellIndexRangeCoef = 2.5f;
}


