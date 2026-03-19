using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class Test2 : MonoBehaviour
{
    public List<Vector2> position;
    public RectTransform point;
    public TobiiController tobiiController;
    public Transform headGO;

    private int lastIndex = -1; // 初始化為 -1 表示尚未抽過
    private List<Test2Data> recordedData = new List<Test2Data>();
    private Coroutine autoTriggerCoroutine;

    [System.Serializable]
    public class Test2Data
    {
        public float time;
        public Vector3 position;
        public bool isSuccess;
    }
    [System.Serializable]
    private class Test2DataWrapper
    {
        public float startTime;
        public List<Test2Data> Test2DataList;
    }

    private float _startTime;
    private int pointCounter = 0;

    Vector2 GetRandomPositionWithoutImmediateRepeat(List<Vector2> position)
    {
        if (position.Count <= 1)
        {
            Debug.LogWarning("List 太短，無法避免重複。");
            return position[0];
        }

        int randomIndex;

        do
        {
            randomIndex = UnityEngine.Random.Range(0, position.Count);
        }
        while (randomIndex == lastIndex);

        lastIndex = randomIndex;
        return position[randomIndex];
    }

    public void Test2Start()
    {
        pointCounter = 0;
        point.anchoredPosition = Vector2.zero;
        _startTime = Time.time;
        StartCoroutine(Delay5Sec());
        StartAutoTrigger();
    }
    public void SetPoint(bool _isSuccess)
    {
        if (autoTriggerCoroutine != null)
            StopCoroutine(autoTriggerCoroutine);

        Vector3 newPos = new Vector3(point.anchoredPosition.x, point.anchoredPosition.y, headGO.position.z);

        recordedData.Add(new Test2Data
        {
            time = Time.time,
            position = newPos,
            isSuccess = _isSuccess
        });

        pointCounter++;

        if (recordedData.Count == 40)
        {
            tobiiController.Test2End();
            SaveData("", true);
        }
        else
        {
            if (pointCounter % 2 == 0)
                point.anchoredPosition = Vector2.zero;
            else
                point.anchoredPosition = GetRandomPositionWithoutImmediateRepeat(position);

            StartAutoTrigger();
        }
    }
    private void StartAutoTrigger()
    {
        autoTriggerCoroutine = StartCoroutine(Delay5Sec());
    }
    private IEnumerator Delay5Sec()
    {
        yield return new WaitForSeconds(5f);
        SetPoint(false);
    }

    public void SaveData(string filename, bool needSave = true)
    {
        if (needSave)
        {
            string json = JsonUtility.ToJson(new Test2DataWrapper { startTime = _startTime, Test2DataList = recordedData }, true);

            string filePath = Path.Combine(Application.persistentDataPath, "jump_data_" + filename + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json");
            File.WriteAllText(filePath, json);

            Debug.Log("Test2 recording saved to: " + filePath);
        }
    }
    public void Reset()
    {
        recordedData.Clear();
        lastIndex = -1;
        StopAllCoroutines();
    }
}
