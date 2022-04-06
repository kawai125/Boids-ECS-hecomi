using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Unity.Mathematics;

using TMPro;

public class ComputePlan_Controller : MonoBehaviour
{
    [SerializeField]
    private Dropdown _dropdownComputePlan;
    [SerializeField]
    private InputField _inputFieldRangeCoef;

    [SerializeField]
    private TMP_Text _textGridSize;
    [SerializeField]
    private TMP_Text _textBatchSize;
    [SerializeField]
    private TMP_Text _textNumberOfTargetCells;

    private List<ComputeNeighborsPlan> _planList;
    private ComputeNeighborsPlan _current_plan;

    private const float minRangeCoef = 0.2f;
    private const float maxRangeCoef = 10f;

    private void Start()
    {
        _planList = new List<ComputeNeighborsPlan>()
        {
            ComputeNeighborsPlan.Direct,

            ComputeNeighborsPlan.CellIndex_Entity_NeighborList,
            ComputeNeighborsPlan.CellIndex_Cell_NeighborList,
            ComputeNeighborsPlan.CellIndex_Cell_Cell,

            ComputeNeighborsPlan.CellIndex_Combined,
        };

        _dropdownComputePlan.ClearOptions();
        var drop_menu = new List<string>();
        foreach(var plan in _planList)
        {
            drop_menu.Add(plan.ToString());
        }
        _dropdownComputePlan.AddOptions(drop_menu);
        _dropdownComputePlan.value = 0;

        _inputFieldRangeCoef.text = Bootstrap.Param.cellIndexRangeCoef.ToString();


        _dropdownComputePlan.onValueChanged.AddListener(OnValueChangedDropdownComputePlan);
        _inputFieldRangeCoef.onEndEdit.AddListener(OnEndEditInputFieldRangeCoef);

        _current_plan = ComputeNeighborsPlan.Direct;
    }

    private void Update()
    {
        if(_current_plan == ComputeNeighborsPlan.Direct)
        {
            _textGridSize.text = "not used";
        }
        else
        {
            _textGridSize.text = CellIndex_Bootstrap.Instance.HashCellIndex.GridSize.ToString();
        }

        if (_current_plan == ComputeNeighborsPlan.Direct ||
            _current_plan == ComputeNeighborsPlan.CellIndex_Entity_NeighborList)
        {
            _textBatchSize.text = "none";
            _textNumberOfTargetCells.text = "none";
        }
        else
        {
            _textBatchSize.text = CellIndex_Bootstrap.Instance.CellBatchSize.ToString();
            _textNumberOfTargetCells.text = CellIndex_Bootstrap.Instance.NumberOfContainsCells.ToString();
        }
    }

    public void OnValueChangedDropdownComputePlan(int index)
    {
        var new_plan = _planList[index];
        Bootstrap.Instance.SwitchComputeNeighborsPlan(_current_plan, new_plan);
        _current_plan = new_plan;
    }
    public void OnEndEditInputFieldRangeCoef(string str)
    {
        if (!float.TryParse(str, out float range_coef))
        {
            //--- fail to parse
            _inputFieldRangeCoef.text = CellIndex_Bootstrap.Instance.RangeCoef.ToString();
            return;
        }

        range_coef = math.min(range_coef, maxRangeCoef);
        range_coef = math.max(range_coef, minRangeCoef);

        CellIndex_Bootstrap.Instance.RangeCoef = range_coef;
        _inputFieldRangeCoef.text = range_coef.ToString();
    }
}