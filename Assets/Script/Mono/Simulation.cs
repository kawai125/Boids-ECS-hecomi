using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Simulation : MonoBehaviour
{
    [SerializeField]
    UI_controller ui_input;
    public int boidCount;

    [SerializeField]
    GameObject boidPrefab;

    [SerializeField]
    Param param;


    List<Boid> boids_ = new List<Boid>();
    public IReadOnlyCollection<Boid> boids
    {
        get { return boids_.AsReadOnly(); }
    }

    void AddBoid()
    {
        var go = Instantiate(boidPrefab, UnityEngine.Random.insideUnitSphere, UnityEngine.Random.rotation);
        go.transform.SetParent(transform);
        var boid = go.GetComponent<Boid>();
        boid.simulation = this;
        boid.param = param;
        boids_.Add(boid);
    }

    void RemoveBoid()
    {
        if (boids_.Count == 0) return;

        var lastIndex = boids_.Count - 1;
        var boid = boids_[lastIndex];
        Destroy(boid.gameObject);
        boids_.RemoveAt(lastIndex);
    }

    void Update()
    {
        this.boidCount = ui_input.boidCount;

        while (boids_.Count < boidCount)
        {
            AddBoid();
        }
        while (boids_.Count > boidCount)
        {
            RemoveBoid();
        }
    }

    void OnDrawGizmos()
    {
        if (!param) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one * param.wallScale);
    }
}
