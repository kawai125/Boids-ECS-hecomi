using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Unity.Mathematics;

public class Benchmark_Controller : MonoBehaviour
{
    [SerializeField]
    private InputField inputFieldTargetFPS;
    [SerializeField]
    private InputField inputFieldBoidsDensity;
    [SerializeField]
    private Button buttonStartBenchmark;
    [SerializeField]
    private Button buttonStopBenchmark;

    [SerializeField]
    private UI_controller ui_controller;

    private bool _running;
    private float _boids_density;

    private enum State
    {
        Spawn,
        Remove,
    }

    private State _state;
    private uint _state_count;

    private float _spawn_delay;
    private float _target_fps;

    private void Start()
    {
        _target_fps = Define.InitialTargetFPS;
        inputFieldTargetFPS.text = _target_fps.ToString();
        inputFieldTargetFPS.onEndEdit.AddListener(UpdateTargetFPS);

        _boids_density = Define.InitialDensityForBoids;
        inputFieldBoidsDensity.text = _boids_density.ToString();
        inputFieldBoidsDensity.onEndEdit.AddListener(UpdateBoidsDensity);

        buttonStartBenchmark.gameObject.SetActive(true);
        buttonStopBenchmark.gameObject.SetActive(false);
        _running = false;

        buttonStartBenchmark.onClick.AddListener(StartBenchmark);
        buttonStopBenchmark.onClick.AddListener(StopBenchmark);

        _state = State.Spawn;
        _state_count = 0;

        _spawn_delay = 0f;
    }

    private void Update()
    {
        const float spawn_interval = 0.5f;

        CheckFPS();
        if (!_running) return;

        _spawn_delay += Time.deltaTime;
        //Debug.Log($"spawn_Delay={_spawn_delay}, dt={Time.deltaTime}");
        if (_spawn_delay < spawn_interval) return;
        _spawn_delay = 0f;

        int n_next = NextBoidsNum();
        float scale_next = NextCageScale(n_next);

        ui_controller.UpdateByScript(n_next, scale_next);
    }

    private void CheckFPS()
    {
        float fps = 1f / Time.deltaTime;
        float margin = _target_fps * 0.02f;

        State now = fps >= (_target_fps - margin) ? State.Spawn : State.Remove;
        //Debug.Log($"fps = {fps}, jadge={_target_fps - margin}, state={now}");
        if (now == _state)
        {
            _state_count++;
        }
        else
        {
            _state = now;
            _state_count = 0;
        }
    }
    private int NextBoidsNum()
    {
        /*
        int n_next = Bootstrap.BoidsCount;
        switch (_state)
        {
            case State.Spawn:
                n_next += 100;
                break;
            case State.Remove:
                n_next -= 10;
                break;
        }

        return n_next;
        */


        const int spawn_coef = 240;
        const float spawn_max_ratio = 0.2f;
        const int n_diff_max = int.MaxValue / 4;

        int n_now = math.max(Bootstrap.BoidsCount, 10);

        float n_coef = (float)_state_count / spawn_coef;
        n_coef = math.min(n_coef, 1f);

        int n_diff = math.min((int)(n_coef * n_now * spawn_max_ratio), n_diff_max);

        int n_next = n_now;
        switch (_state)
        {
            case State.Spawn:
                n_next += n_diff;
                break;
            case State.Remove:
                n_next -= n_diff;
                break;
        }
        n_next = math.max(n_next, 0);

        return n_next;
    }
    private float NextCageScale(int n_boids)
    {
        const float margin = 0.05f;

        float scale_now = Bootstrap.WallScale;

        float tgt_volume = n_boids / _boids_density;
        float tgt_edge = math.pow(tgt_volume, 1f / 3f);

        if(math.abs(scale_now - tgt_edge) > scale_now * margin)
        {
            // 0.5 unit
            int edge = (int)(tgt_edge * 2f);
            return edge * 0.5f;
        }
        else
        {
            return scale_now;
        }
    }

    public void StartBenchmark()
    {
        SwitchMode(true);
    }
    public void StopBenchmark()
    {
        SwitchMode(false);
    }
    private void SwitchMode(bool is_run)
    {
        if (is_run)
        {
            _running = true;
            buttonStartBenchmark.gameObject.SetActive(false);
            buttonStopBenchmark.gameObject.SetActive(true);
            ui_controller.EnableManualControl(false);
        }
        else
        {
            _running = false;
            buttonStartBenchmark.gameObject.SetActive(true);
            buttonStopBenchmark.gameObject.SetActive(false);
            ui_controller.EnableManualControl(true);
        }
    }

    public void UpdateTargetFPS(string str)
    {
        if (float.TryParse(str, out float fps))
        {
            fps = math.max(fps, 10f);
            fps = math.min(fps, 1000f);
            _target_fps = fps;
        }
        inputFieldTargetFPS.text = _target_fps.ToString();
    }
    public void UpdateBoidsDensity(string str)
    {
        if(float.TryParse(str, out float density))
        {
            density = math.max(density, 0.1f);
            density = math.min(density, 10f);
            _boids_density = density;
        }
        inputFieldBoidsDensity.text = _boids_density.ToString();
    }
}
