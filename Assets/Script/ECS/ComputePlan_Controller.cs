using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Unity.Mathematics;
using Unity.Jobs.LowLevel.Unsafe;

using TMPro;

public class ComputePlan_Controller : MonoBehaviour
{
    [SerializeField]
    private InputField _inputFieldNumWorkerThreads;

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

    [SerializeField]
    private GameObject _gameObjectMergedCelSize;
    [SerializeField]
    private InputField _inputFieldMergedCellSize;

    private List<ComputeNeighborsPlan> _planList;
    private ComputeNeighborsPlan _current_plan;

    private void Start()
    {
        _inputFieldNumWorkerThreads.text = JobsUtility.JobWorkerCount.ToString();
        _inputFieldNumWorkerThreads.onEndEdit.AddListener(UpdateNumWorkerThreads);

        _planList = new List<ComputeNeighborsPlan>()
        {
            ComputeNeighborsPlan.Direct,

            ComputeNeighborsPlan.CellIndex_Entity_NeighborList,
            ComputeNeighborsPlan.CellIndex_Cell_NeighborList,
            ComputeNeighborsPlan.CellIndex_Cell_Cell,

            ComputeNeighborsPlan.CellIndex_Combined_CNL,
            ComputeNeighborsPlan.CellIndex_Combined_CC,

            ComputeNeighborsPlan.CellIndex_MergedCell_NL,
        };

        _dropdownComputePlan.ClearOptions();
        var drop_menu = new List<string>();
        foreach(var plan in _planList)
        {
            drop_menu.Add(plan.ToString());
        }
        _dropdownComputePlan.AddOptions(drop_menu);
        _dropdownComputePlan.value = 0;
        _dropdownComputePlan.onValueChanged.AddListener(OnValueChangedDropdownComputePlan);

        _inputFieldRangeCoef.text = Define.InitialCellIndexRangeCoef.ToString();
        _inputFieldRangeCoef.onEndEdit.AddListener(OnEndEditInputFieldRangeCoef);

        _inputFieldMergedCellSize.text = Define.InitialCellMergeSize.ToString();
        _inputFieldMergedCellSize.onEndEdit.AddListener(UpdateMergedCellSize);

        _current_plan = ComputeNeighborsPlan.Direct;

        _gameObjectMergedCelSize.SetActive(false);
    }

    private void Update()
    {
        if(_current_plan == ComputeNeighborsPlan.Direct)
        {
            _textGridSize.text = "not used";
        }
        else
        {
            _textGridSize.text = CellIndex_Bootstrap.HashCellIndex.GridSize.ToString();
        }

        if (_current_plan == ComputeNeighborsPlan.Direct ||
            _current_plan == ComputeNeighborsPlan.CellIndex_Entity_NeighborList)
        {
            _textBatchSize.text = "by entity";
            _textNumberOfTargetCells.text = "by entity";
        }
        else
        {
            _textBatchSize.text = CellIndex_Bootstrap.CellBatchSize.ToString();
            _textNumberOfTargetCells.text = CellIndex_Bootstrap.NumberOfContainsCells.ToString();
        }

        if(_current_plan == ComputeNeighborsPlan.CellIndex_MergedCell_NL)
        {
            int nm = CellIndex_Bootstrap.CellMergeSize;
            int batch = nm * nm * nm;
            _textBatchSize.text = $"{batch} x {CellIndex_Bootstrap.CellBatchSize}";
        }
    }

    public void OnValueChangedDropdownComputePlan(int index)
    {
        var new_plan = _planList[index];
        Bootstrap.Instance.SwitchComputeNeighborsPlan(_current_plan, new_plan);
        _current_plan = new_plan;

        _gameObjectMergedCelSize.SetActive(new_plan == ComputeNeighborsPlan.CellIndex_MergedCell_NL);
    }
    public void OnEndEditInputFieldRangeCoef(string str)
    {
        if (float.TryParse(str, out float range_coef))
        {
            range_coef = math.clamp(range_coef, 0.2f, 10f);
            CellIndex_Bootstrap.RangeCoef = range_coef;
        }
        _inputFieldRangeCoef.text = CellIndex_Bootstrap.RangeCoef.ToString();
    }
    public void UpdateNumWorkerThreads(string str)
    {
        if(int.TryParse(str, out int n_Threads))
        {
            n_Threads = math.clamp(n_Threads, 1, JobsUtility.JobWorkerMaximumCount);
            JobsUtility.JobWorkerCount = n_Threads;
        }
        _inputFieldNumWorkerThreads.text = JobsUtility.JobWorkerCount.ToString();
    }
    public void UpdateMergedCellSize(string str)
    {
        if(int.TryParse(str, out int n_merge))
        {
            n_merge = math.clamp(n_merge, 1, 10);
            CellIndex_Bootstrap.CellMergeSize = n_merge;
        }
        _inputFieldMergedCellSize.text = CellIndex_Bootstrap.CellMergeSize.ToString();
    }
}
