using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UI_controller : MonoBehaviour
{
    [SerializeField]
    private CageView cageView;

    [SerializeField]
    private Param param;

    public int boidCount = Define.InitialBoidsNum;
    public float cageScale = Define.InitialWallScale;

    [SerializeField]
    private InputField inputBoidNumField;
    [SerializeField]
    private InputField inputScaleField;
    [SerializeField]
    private Button applyButton;

    public float searchRange, searchAngle;

    [SerializeField]
    private InputField inputSearchRange;
    [SerializeField]
    private InputField inputSearchAngle;

    public float vortexIntensity;

    [SerializeField]
    private InputField inputVortexIntensity;

    // Start is called before the first frame update
    void Start()
    {
        cageView.Init();
        cageView.UpdateScale(cageScale);

        searchRange = Define.InitialNeighborSearchRange;
        searchAngle = Define.InitialNeighborSearchAngle;

        if (inputSearchRange != null)
        {
            inputSearchRange.text = Define.InitialNeighborSearchRange.ToString();
            inputSearchRange.onEndEdit.AddListener(UpdateSearchRange);
        }
        if (inputSearchAngle != null)
        {
            inputSearchAngle.text = Define.InitialNeighborSearchAngle.ToString();
            inputSearchAngle.onEndEdit.AddListener(UpdateSearchAngle);
        }
        if (inputVortexIntensity != null)
        {
            inputVortexIntensity.text = Define.InitialVortexIntensity.ToString();
            inputVortexIntensity.onEndEdit.AddListener(UpdateVortexIntensity);
        }
    }

    public void OnClick()
    {
        UpdateCondition();
    }

    public void UpdateCondition()
    {
        //--- input # of Boids
        if(Int32.TryParse(inputBoidNumField.text, out int n_input))
        {
            n_input = Math.Max(n_input, 0);
            boidCount = n_input;
        }

        //--- input scale
        if(float.TryParse(inputScaleField.text, out float new_scale))
        {
            new_scale = Math.Max(new_scale, 4f);
            cageScale = new_scale;
            cageView.UpdateScale(cageScale);
        }
    }
    public void UpdateSearchRange(string str)
    {
        if(float.TryParse(str, out float r_search))
        {
            r_search = Mathf.Clamp(r_search, 0.2f, 15f);
            searchRange = r_search;
        }
        inputSearchRange.text = searchRange.ToString();
    }
    public void UpdateSearchAngle(string str)
    {
        if (float.TryParse(str, out float angle))
        {
            angle = Mathf.Clamp(angle, 1f, 180f);
            searchAngle = angle;
        }
        inputSearchAngle.text = searchAngle.ToString();
    }
    public void UpdateVortexIntensity(string str)
    {
        if(float.TryParse(str, out float vortex))
        {
            vortex = Mathf.Clamp(vortex, -5f, 5f);
            vortexIntensity = vortex;
        }
        inputVortexIntensity.text = vortexIntensity.ToString();
    }

    // for benchmark
    public void UpdateByScript(int n_boid, float cage_scale)
    {
        n_boid = Math.Max(n_boid, 0);
        cageScale = Math.Max(cageScale, 5f);

        boidCount = n_boid;
        inputBoidNumField.text = boidCount.ToString();

        cageScale = cage_scale;
        inputScaleField.text = cageScale.ToString();
        cageView.UpdateScale(cageScale);
    }
    public void EnableManualControl(bool enable)
    {
        if (enable)
        {
            inputBoidNumField.ActivateInputField();
            inputScaleField.ActivateInputField();
            applyButton.interactable = true;
        }
        else
        {
            inputBoidNumField.DeactivateInputField();
            inputScaleField.DeactivateInputField();
            applyButton.interactable = false;
        }
    }
}
