using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public struct BoidParams
{
    public float initSpeed;
    public float minSpeed;
    public float maxSpeed;

    public float neighborSearchRange;
    public float neighborSearchAngle;

    public float alignmentWeight;
    public float cohesionWeight;
    public float separationWeight;

    public float wallDistance;
    public float wallWeight;

    public float vortexIntensity;
}

public class BoidParams_Bootstrap : MonoBehaviour
{
    public static BoidParams_Bootstrap Instance { get; private set; }
    void Awake()
    {
        Instance = this;
    }

    public static BoidParams Param;


    [SerializeField]
    private InputField inputInitSpeed;
    [SerializeField]
    private InputField inputMinSpeed;
    [SerializeField]
    private InputField inputMaxSpeed;

    [SerializeField]
    private InputField inputSearchRange;
    [SerializeField]
    private InputField inputSearchAngle;

    [SerializeField]
    private InputField inputAlignmentWeight;
    [SerializeField]
    private InputField inputCohesionWeight;
    [SerializeField]
    private InputField inputSeparationWeight;

    [SerializeField]
    private InputField inputWallDistance;
    [SerializeField]
    private InputField inputWallWeight;

    [SerializeField]
    private InputField inputVortexIntensity;

    private void Start()
    {
        Param.initSpeed = InitialBoidsParam.initSpeed;
        Param.minSpeed = InitialBoidsParam.minSpeed;
        Param.maxSpeed = InitialBoidsParam.maxSpeed;

        Param.neighborSearchRange = InitialBoidsParam.neighborSearchRange;
        Param.neighborSearchAngle = InitialBoidsParam.neighborSearchAngle;

        Param.alignmentWeight = InitialBoidsParam.alignmentWeight;
        Param.cohesionWeight = InitialBoidsParam.cohesionWeight;
        Param.separationWeight = InitialBoidsParam.separationWeight;

        Param.wallDistance = InitialBoidsParam.wallDistance;
        Param.wallWeight = InitialBoidsParam.wallWeight;

        Param.vortexIntensity = 1f;


        inputInitSpeed.text = Param.initSpeed.ToString();
        inputInitSpeed.onEndEdit.AddListener(UpdateInitSpeed);

        inputMinSpeed.text = Param.minSpeed.ToString();
        inputMinSpeed.onEndEdit.AddListener(UpdateMinSpeed);

        inputMaxSpeed.text = Param.maxSpeed.ToString();
        inputMaxSpeed.onEndEdit.AddListener(UpdateMaxSpeed);


        inputSearchRange.text = Param.neighborSearchRange.ToString();
        inputSearchRange.onEndEdit.AddListener(UpdateSearchRange);

        inputSearchAngle.text = Param.neighborSearchAngle.ToString();
        inputSearchAngle.onEndEdit.AddListener(UpdateSearchAngle);


        inputAlignmentWeight.text = Param.alignmentWeight.ToString();
        inputAlignmentWeight.onEndEdit.AddListener(UpdateAlignmentWeight);

        inputCohesionWeight.text = Param.cohesionWeight.ToString();
        inputCohesionWeight.onEndEdit.AddListener(UpdateCohesionWeight);

        inputSeparationWeight.text = Param.separationWeight.ToString();
        inputSeparationWeight.onEndEdit.AddListener(UpdateSeparationWeight);


        inputWallDistance.text = Param.wallDistance.ToString();
        inputWallDistance.onEndEdit.AddListener(UpdateWallDistance);

        inputWallWeight.text = Param.wallWeight.ToString();
        inputWallWeight.onEndEdit.AddListener(UpdateWallWeight);


        inputVortexIntensity.text = Param.vortexIntensity.ToString();
        inputVortexIntensity.onEndEdit.AddListener(UpdateVortexIntensity);
    }

    public void UpdateInitSpeed(string str)
    {
        UpdateValue(str, inputInitSpeed, ref Param.initSpeed, 0.2f, 20f);
    }
    public void UpdateMinSpeed(string str)
    {
        UpdateValue(str, inputMinSpeed, ref Param.minSpeed, 0.2f, Param.maxSpeed);
    }
    public void UpdateMaxSpeed(string str)
    {
        UpdateValue(str, inputMaxSpeed, ref Param.maxSpeed, Param.minSpeed, 20f);
    }

    public void UpdateSearchRange(string str)
    {
        UpdateValue(str, inputSearchRange, ref Param.neighborSearchRange, 0.2f, 15f);
    }
    public void UpdateSearchAngle(string str)
    {
        UpdateValue(str, inputSearchAngle, ref Param.neighborSearchAngle, 1f, 180f);
    }

    public void UpdateAlignmentWeight(string str)
    {
        UpdateValue(str, inputAlignmentWeight, ref Param.alignmentWeight, -10f, 100f);
    }
    public void UpdateCohesionWeight(string str)
    {
        UpdateValue(str, inputCohesionWeight, ref Param.cohesionWeight, -10f, 100f);
    }
    public void UpdateSeparationWeight(string str)
    {
        UpdateValue(str, inputSeparationWeight, ref Param.separationWeight, -10f, 100f);
    }

    public void UpdateWallDistance(string str)
    {
        UpdateValue(str, inputWallDistance, ref Param.wallDistance, 0.1f, Bootstrap.WallScale * 0.49f);
    }
    public void UpdateWallWeight(string str)
    {
        UpdateValue(str, inputWallWeight, ref Param.wallWeight, 0.1f, 10f);
    }

    public void UpdateVortexIntensity(string str)
    {
        UpdateValue(str, inputVortexIntensity, ref Param.vortexIntensity, -10f, 10f);
    }

    private static void UpdateValue(string str, InputField field, ref int value, int min, int max)
    {
        if(int.TryParse(str, out int tmp))
        {
            tmp = Mathf.Clamp(tmp, min, max);
            value = tmp;
        }
        field.text = value.ToString();
    }
    private static void UpdateValue(string str, InputField field, ref float value, float min, float max)
    {
        if (float.TryParse(str, out float tmp))
        {
            tmp = Mathf.Clamp(tmp, min, max);
            value = tmp;
        }
        field.text = value.ToString();
    }
}
