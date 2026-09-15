using System.Collections.Generic;
using UnityEngine;

// Fixation-aware smoothing for noisy screen-space gaze samples:
// averages while the eye stays in one spot, jumps once a saccade is confirmed,
// and removes single-sample spikes with a 3-sample median.
public class FixationSmoother
{
    public float FixationRadius = 80f;
    public float FixationWindowSeconds = 0.5f;
    public int SaccadeConfirmSamples = 2;

    private readonly List<(float time, Vector2 position)> _fixation = new List<(float time, Vector2 position)>();
    private readonly List<Vector2> _saccadeCandidates = new List<Vector2>();
    private readonly Vector2[] _recent = new Vector2[3];
    private int _recentCount;
    private int _recentIndex;

    public bool HasOutput { get; private set; }
    public Vector2 Output { get; private set; }
    /// <summary>RMS distance of the current fixation's samples from their mean, in pixels.</summary>
    public float Spread { get; private set; }

    public void Reset()
    {
        _fixation.Clear();
        _saccadeCandidates.Clear();
        _recentCount = 0;
        _recentIndex = 0;
        HasOutput = false;
        Spread = 0f;
    }

    public void AddSample(float time, Vector2 raw)
    {
        Vector2 sample = Despike(raw);

        if (HasOutput && Vector2.Distance(sample, Output) <= FixationRadius)
        {
            _saccadeCandidates.Clear();
            _fixation.Add((time, sample));
            while (_fixation.Count > 1 && _fixation[0].time < time - FixationWindowSeconds)
                _fixation.RemoveAt(0);
        }
        else
        {
            _saccadeCandidates.Add(sample);
            if (HasOutput && _saccadeCandidates.Count < SaccadeConfirmSamples)
                return;

            _fixation.Clear();
            foreach (var candidate in _saccadeCandidates)
                _fixation.Add((time, candidate));
            _saccadeCandidates.Clear();
        }

        UpdateOutput();
    }

    private Vector2 Despike(Vector2 raw)
    {
        _recent[_recentIndex] = raw;
        _recentIndex = (_recentIndex + 1) % _recent.Length;
        if (_recentCount < _recent.Length)
            _recentCount++;
        if (_recentCount < _recent.Length)
            return raw;

        return new Vector2(
            Median(_recent[0].x, _recent[1].x, _recent[2].x),
            Median(_recent[0].y, _recent[1].y, _recent[2].y));
    }

    private static float Median(float a, float b, float c)
    {
        return Mathf.Max(Mathf.Min(a, b), Mathf.Min(Mathf.Max(a, b), c));
    }

    private void UpdateOutput()
    {
        Vector2 sum = Vector2.zero;
        foreach (var sample in _fixation)
            sum += sample.position;
        Vector2 mean = sum / _fixation.Count;

        float squared = 0f;
        foreach (var sample in _fixation)
            squared += (sample.position - mean).sqrMagnitude;

        Spread = Mathf.Sqrt(squared / _fixation.Count);
        Output = mean;
        HasOutput = true;
    }
}
