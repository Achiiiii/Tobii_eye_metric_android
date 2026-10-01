using System.Collections.Generic;
using UnityEngine;

// Fixation-aware smoothing for noisy screen-space gaze samples:
// averages while the eye stays in one spot, jumps once a saccade is confirmed,
// and removes single-sample spikes with a 3-sample median. Both of those wait for samples, so
// they cost more at lower gaze rates; jumps too large to be noise skip them.
public class FixationSmoother
{
    public float FixationRadius = 80f;
    public float FixationWindowSeconds = 0.5f;
    public int SaccadeConfirmSamples = 2;
    // Samples this far from the output are taken at once (0 disables). At 1050x780 gaze noise at
    // the centre symbol was 35 px typical and 80 px at the 90th percentile, while looking from the
    // symbol to an option is 207-277 px; the median and the confirmation together held the dot
    // back two samples, about 200 ms at the ~10 Hz gaze rate.
    public float ImmediateJump = 0f;

    // After an immediate jump: where the output was, to go back if the next sample returns there.
    private bool _jumpPending;
    private Vector2 _beforeJump;

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
        _jumpPending = false;
        HasOutput = false;
        Spread = 0f;
    }

    public void AddSample(float time, Vector2 raw)
    {
        if (_jumpPending)
        {
            _jumpPending = false;
            // Straight back to where it was before the jump: that was a single-sample spike.
            if (Vector2.Distance(raw, _beforeJump) <= FixationRadius && Vector2.Distance(raw, Output) > FixationRadius)
            {
                StartFixationAt(time, raw);
                return;
            }
        }

        if (HasOutput && ImmediateJump > 0f && Vector2.Distance(raw, Output) > ImmediateJump)
        {
            _beforeJump = Output;
            _jumpPending = true;
            StartFixationAt(time, raw);
            return;
        }

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

    // Starts a new fixation at this sample, and refills the median with it so the samples that
    // follow are not pulled back toward the old position.
    private void StartFixationAt(float time, Vector2 position)
    {
        for (int i = 0; i < _recent.Length; i++)
            _recent[i] = position;
        _recentCount = _recent.Length;
        _recentIndex = 0;
        _saccadeCandidates.Clear();
        _fixation.Clear();
        _fixation.Add((time, position));
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
