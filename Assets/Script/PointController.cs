using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PointController : MonoBehaviour
{
    public int curIndex = 0;
    public UILineRenderer uILineRenderer;
    public TobiiController tobiiController;
    public PointButtonTrigger[] pointGOs;

    public void SetPoint(Vector2 position, int index)
    {
        curIndex = index;
        uILineRenderer.points.Add(position);
        uILineRenderer.SetVerticesDirty();
        if (index == 25)
        {
            StartCoroutine(DelayReset());
        }
    }
    private IEnumerator DelayReset()
    {
        yield return new WaitForSeconds(0.5f);
        Reset();
        tobiiController.EndTest();
    }
    public void Reset()
    {
        curIndex = 0;
        uILineRenderer.points.Clear();
        foreach (PointButtonTrigger item in pointGOs)
        {
            item.Reset();
        }
        uILineRenderer.SetVerticesDirty();
    }
}
