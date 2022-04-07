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
        if (new_scale >= 5f)
        {
            cageScale = new_scale;
            cageView.UpdateScale(cageScale);
        }
    }
    public void UpdateSearchRange(string str)
    {
        if(float.TryParse(str, out float r_search))
        {
            if(0.2f <= r_search && r_search < 15f)
            {
                searchRange = r_search;
                return;
            }
        }

        inputSearchRange.text = searchRange.ToString();
    }
    public void UpdateSearchAngle(string str)
    {
        if (float.TryParse(str, out float angle))
        {
            if (1f <= angle && angle <= 180f)
            {
                searchAngle = angle;
                return;
            }
        }

        inputSearchAngle.text = searchAngle.ToString();
    }

    // for benchmark
    public void UpdateByScript(int n_boid, float cage_scale)
    {
        if (n_boid <= 0 || cage_scale <= 5f) return;

        boidCount = n_boid;
        inputBoidNumField.text = boidCount.ToString();

        cageScale = cage_scale;
        inputScaleField.text = cageScale.ToString();
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
