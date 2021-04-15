using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UI_controller : MonoBehaviour
{
    [SerializeField]
    CageView cageView;

    [SerializeField]
    Param param;

    public int boidCount = 100;

    public InputField inputBoidNumField;
    public Text inputBoidNumText;

    public InputField inputScaleField;
    public Text inputScaleText;

    // Start is called before the first frame update
    void Start()
    {
        cageView = cageView.GetComponent<CageView>();

        inputBoidNumField = inputBoidNumField.GetComponent<InputField>();
        inputBoidNumText = inputBoidNumText.GetComponent<Text>();

        inputScaleField = inputScaleField.GetComponent<InputField>();
        inputScaleText = inputScaleText.GetComponent<Text>();
    }

    public void OnClick()
    {
        UpdateCondition();
    }

    public void UpdateCondition()
    {
        //--- input # of Boids
        inputBoidNumText.text = inputBoidNumField.text;
        int n_input;
        Int32.TryParse(inputBoidNumText.text, out n_input);

        if (n_input >= 0)
        {
            this.boidCount = n_input;
        }

        //--- input scale
        inputScaleText.text = inputScaleField.text;
        float new_scale = float.Parse(inputScaleText.text);
        if (new_scale > 0.0f)
        {
            param.wallScale = new_scale;
            cageView.UpdateScale();
        }
    }
}
