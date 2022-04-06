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

    public int boidCount = 100;

    [SerializeField]
    public InputField inputBoidNumField;
    [SerializeField]
    public InputField inputScaleField;
    [SerializeField]
    private Button applyButton;

    // Start is called before the first frame update
    void Start()
    {

    }

    public void OnClick()
    {
        UpdateCondition();
    }

    public void UpdateCondition()
    {
        //--- input # of Boids
        Int32.TryParse(inputBoidNumField.text, out int n_input);
        if (n_input >= 0)
        {
            this.boidCount = n_input;
        }

        //--- input scale
        float.TryParse(inputScaleField.text, out float new_scale);
        if (new_scale > 0.0f)
        {
            param.wallScale = new_scale;
            cageView.UpdateScale();
        }
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
