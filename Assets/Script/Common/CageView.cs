using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CageView : MonoBehaviour
{

    [SerializeField]
    public GameObject cubePrefab;

    [SerializeField]
    public Param param;

    [SerializeField]
    public float cageWidthCoef;

    List<GameObject> cubeList_ = new List<GameObject>();

    // Start is called before the first frame update
    void Start()
    {
        for(int i=0; i<12; i++)
        {
            cubeList_.Add(Instantiate(cubePrefab));
        }

        UpdateScale();
    }

    public void UpdateScale()
    {
        float wallScale = param.wallScale;
        float cageWidth = wallScale * cageWidthCoef;
        Vector3 xCageSize = new Vector3(wallScale, cageWidth, cageWidth);
        Vector3 yCageSize = new Vector3(cageWidth, wallScale, cageWidth);
        Vector3 zCageSize = new Vector3(cageWidth, cageWidth, wallScale);

        //--- make floor
        cubeList_[0].transform.localScale = xCageSize;
        cubeList_[1].transform.localScale = xCageSize;

        cubeList_[2].transform.localScale = zCageSize;
        cubeList_[3].transform.localScale = zCageSize;

        cubeList_[0].transform.position = new Vector3(0.0f, -0.5f * wallScale, -0.5f * wallScale);
        cubeList_[1].transform.position = new Vector3(0.0f, -0.5f * wallScale, 0.5f * wallScale);

        cubeList_[2].transform.position = new Vector3(-0.5f * wallScale, -0.5f * wallScale, 0.0f);
        cubeList_[3].transform.position = new Vector3(0.5f * wallScale, -0.5f * wallScale, 0.0f);

        //--- make ceil
        cubeList_[4].transform.localScale = xCageSize;
        cubeList_[5].transform.localScale = xCageSize;

        cubeList_[6].transform.localScale = zCageSize;
        cubeList_[7].transform.localScale = zCageSize;

        cubeList_[4].transform.position = new Vector3(0.0f, 0.5f * wallScale, -0.5f * wallScale);
        cubeList_[5].transform.position = new Vector3(0.0f, 0.5f * wallScale, 0.5f * wallScale);

        cubeList_[6].transform.position = new Vector3(-0.5f * wallScale, 0.5f * wallScale, 0.0f);
        cubeList_[7].transform.position = new Vector3(0.5f * wallScale, 0.5f * wallScale, 0.0f);

        //--- make pole
        cubeList_[8].transform.localScale = yCageSize;
        cubeList_[9].transform.localScale = yCageSize;
        cubeList_[10].transform.localScale = yCageSize;
        cubeList_[11].transform.localScale = yCageSize;

        cubeList_[8].transform.position = new Vector3(-0.5f * wallScale, 0.0f, -0.5f * wallScale);
        cubeList_[9].transform.position = new Vector3(-0.5f * wallScale, 0.0f, 0.5f * wallScale);
        cubeList_[10].transform.position = new Vector3(0.5f * wallScale, 0.0f, -0.5f * wallScale);
        cubeList_[11].transform.position = new Vector3(0.5f * wallScale, 0.0f, 0.5f * wallScale);
    }
}
