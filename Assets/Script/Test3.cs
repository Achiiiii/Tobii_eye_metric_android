using System.Collections;
using System.Collections.Generic;
using System.IO;
using DG.Tweening;
using UnityEngine;

public class Test3 : MonoBehaviour
{
    public Transform point;
    public TobiiController tobiiController;
    public Transform headGO;
    private bool _isRecording;
    private List<Test3Data> recordedData = new List<Test3Data>();
    private int times = 0;
    private Vector3[] path = { new(912.5f, 0, 0), new(-912.5f, 0, 0), new(-660f, 0, 0) };

    [System.Serializable]
    public class Test3Data
    {
        public float time;
        public Vector3 position;
    }
    [System.Serializable]
    private class Test3DataWrapper
    {
        public float startTime;
        public List<Test3Data> Test3DataList;
    }

    void Update()
    {
        if (_isRecording)
        {
            Vector3 newPos = new Vector3(point.localPosition.x, point.localPosition.y, headGO.position.z);
            recordedData.Add(new Test3Data
            {
                time = Time.time,
                position = newPos
            });
        }
    }
    public void Test3Start()
    {
        point.localPosition = new Vector3(-660f, 0, 0);
        _isRecording = true;
        point.DOLocalPath(path, 10f).SetEase(Ease.Linear).OnComplete(() =>
        {
            if (times == 0)
            {
                tobiiController.Test3End("first");
                StopRecord("first_", true);
                times++;
            }
            else
            {
                tobiiController.Test3End("end");
                StopRecord("second_", true);
                times = 0;
            }
        });
    }
    public void Reset()
    {
        recordedData.Clear();
        times = 0;
        point.DOKill();
        point.DOLocalMove(new Vector3(-912.5f, 0, 0), 0f);
        _isRecording = false;
        StopRecord("", false);
    }
    public void StopRecord(string filename, bool needSave = true)
    {
        _isRecording = false;
        if (needSave)
        {
            string json = JsonUtility.ToJson(new Test3DataWrapper { Test3DataList = recordedData }, true);

            string filePath = Path.Combine(Application.persistentDataPath, "track_data_" + filename + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json");
            File.WriteAllText(filePath, json);

            Debug.Log("Track recording saved to: " + filePath);
        }
    }
}
