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

    // Start is called before the first frame update
    void Start()
    {
        cageView.Init();
        cageView.UpdateScale(cageScale);
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
