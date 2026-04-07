/*
  COPYRIGHT 2025 - PROPERTY OF TOBII AB
  -------------------------------------
  2025 TOBII AB - KARLSROVAGEN 2D, DANDERYD 182 53, SWEDEN - All Rights Reserved.

  NOTICE:  All information contained herein is, and remains, the property of Tobii AB and its suppliers, if any.
  The intellectual and technical concepts contained herein are proprietary to Tobii AB and its suppliers and may be
  covered by U.S.and Foreign Patents, patent applications, and are protected by trade secret or copyright law.
  Dissemination of this information or reproduction of this material is strictly forbidden unless prior written
  permission is obtained from Tobii AB.
*/

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TMPro;

/// <summary>
/// Captures all Unity log messages (Debug.Log / LogWarning / LogError / exceptions)
/// and appends them to a TMP_Text (TextMeshPro) component.
/// Attach this to any GameObject and assign a TMP_Text reference.
/// </summary>
[DisallowMultipleComponent]
public class DebugLogToTMP : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("TextMeshPro component to write logs into (TMP_Text, TextMeshProUGUI, etc.).")]
    public TMP_Text targetText;

    [Header("Behavior")]
    [Tooltip("Maximum number of lines to keep. Oldest lines are removed when exceeded (0 = unlimited).")]
    public int maxLines = 500;
    [Tooltip("Clear existing text on start.")]
    public bool clearOnStart = true;
    [Tooltip("Include stack traces for errors & exceptions.")]
    public bool includeStackTraceErrors = true;
    [Tooltip("Collapse consecutive duplicate messages.")]
    public bool collapseDuplicates = true;

    [Header("Formatting")]
    [Tooltip("Prefix timestamps (hh:mm:ss.fff).")]
    public bool includeTimestamp = true;
    [Tooltip("Colorize by log type using rich text tags.")]
    public bool colorize = true;

    private readonly List<string> _lines = new List<string>(1024);
    private readonly object _lock = new object();
    private bool _dirty;
    private string _lastLine;
    private int _lastLineRepeatCount;

    void Awake()
    {
        if (targetText == null)
        {
            // Try auto-find in children or scene (first enabled)
            targetText = GetComponentInChildren<TMP_Text>();
            if (targetText == null)
                targetText = FindObjectOfType<TMP_Text>();
        }

        if (clearOnStart && targetText != null)
            targetText.text = string.Empty;

        Application.logMessageReceived += HandleLog;
    }

    void OnDestroy()
    {
        Application.logMessageReceived -= HandleLog;
    }

    private void HandleLog(string condition, string stackTrace, LogType type)
    {
        var sb = new StringBuilder(256);

        if (includeTimestamp)
            sb.AppendFormat("{0:HH:mm:ss.fff} ", DateTime.Now);

        if (colorize)
            sb.Append(GetColorTagOpen(type));

        sb.Append(condition);

        if (collapseDuplicates)
        {
            lock (_lock)
            {
                if (_lastLine == condition)
                {
                    _lastLineRepeatCount++;
                    // Update last line in-place (suffix repeat count)
                    int idx = _lines.Count - 1;
                    if (idx >= 0)
                    {
                        _lines[idx] = AppendRepeatCount(_lines[idx], _lastLineRepeatCount);
                        _dirty = true;
                        return;
                    }
                }
                else
                {
                    _lastLine = condition;
                    _lastLineRepeatCount = 1;
                }
            }
        }

        if (includeStackTraceErrors && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
        {
            if (!string.IsNullOrEmpty(stackTrace))
            {
                sb.AppendLine();
                sb.Append(stackTrace.Trim());
            }
        }

        if (colorize)
            sb.Append(GetColorTagClose(type));

        lock (_lock)
        {
            _lines.Add(sb.ToString());
            TrimIfNeeded();
            _dirty = true;
        }
    }

    private string AppendRepeatCount(string original, int count)
    {
        // Replace any existing (xN) suffix; simple approach:
        int repeatIdx = original.LastIndexOf(" (x", StringComparison.Ordinal);
        if (repeatIdx >= 0)
        {
            // Remove old suffix
            original = original.Substring(0, repeatIdx);
        }
        return $"{original} (x{count})";
    }

    private void TrimIfNeeded()
    {
        if (maxLines > 0 && _lines.Count > maxLines)
        {
            int remove = _lines.Count - maxLines;
            _lines.RemoveRange(0, remove);
        }
    }

    void LateUpdate()
    {
        if (!_dirty || targetText == null) return;

        string combined;
        lock (_lock)
        {
            combined = string.Join("\n", _lines);
            _dirty = false;
        }
        targetText.text = combined;
    }

    private string GetColorTagOpen(LogType type)
    {
        switch (type)
        {
            case LogType.Warning:   return "<color=#E0A800>";
            case LogType.Error:
            case LogType.Assert:
            case LogType.Exception: return "<color=#FF4444>";
            default:                return "<color=#FFFFFF>";
        }
    }

    private string GetColorTagClose(LogType type) => "</color>";

    // Optional public API:

    /// <summary>Manually clear accumulated logs and target text.</summary>
    public void Clear()
    {
        lock (_lock)
        {
            _lines.Clear();
            _lastLine = null;
            _lastLineRepeatCount = 0;
            _dirty = true;
        }
    }

    /// <summary>Add a manual line bypassing Debug.Log (still formatted & timestamped).</summary>
    public void AddManualLine(string text)
    {
        HandleLog(text, string.Empty, LogType.Log);
    }
}