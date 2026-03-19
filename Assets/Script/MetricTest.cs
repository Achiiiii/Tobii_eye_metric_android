using System.Collections;
using System.Collections.Generic;
using Tobii;
using Unity.Mathematics;
using UnityEngine;


public class MetricTest : MonoBehaviour
{
    public RectTransform blackRT;
    public RectTransform[] sidesRT;
    public GameObject resultPage;
    public TMPro.TMP_Text leftScore;
    public TMPro.TMP_Text rightScore;
    public GazeCalibrationManager gazeCalibrationManager;
    public TMPro.TMP_Text resultText;
    public AudioSource audioSource;

    private int curLevel = 5;
    private string answerSide = null;
    private readonly string[] allSides = { "up", "down", "right", "left" };
    private int correct = 0;
    private int wrong = 0;
    private int score = 5;
    private bool hadWrong = false;
    private List<int> scoreList = new List<int>();
    private string lastSide;

    public class ScoreData
    {
        public List<int> scores = new List<int>();

        public string completionTime;

        public ScoreData(List<int> collectedScores)
        {
            scores = collectedScores;
            completionTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
    private string directoryPath;
    private const string FILE_PREFIX = "score_";

    void Awake()
    {
        directoryPath = Application.persistentDataPath;
    }

    public void StartMeticTest()
    {
        RandomSide();
        SetAnswerTransform();
        SetLevel(curLevel);
    }
    public void SetLevel(int level)
    {
        int sizeValue;
        switch (level)
        {
            case 1:
                sizeValue = 160;
                break;
            case 2:
                sizeValue = 96;
                break;
            case 3:
                sizeValue = 64;
                break;
            case 4:
                sizeValue = 48;
                break;
            case 5:
                sizeValue = 32;
                break;
            case 6:
                sizeValue = 24;
                break;
            case 7:
                sizeValue = 20;
                break;
            case 8:
                sizeValue = 16;
                break;
            case 9:
                sizeValue = 13;
                break;
            case 10:
                sizeValue = 10;
                break;
            case 11:
                sizeValue = 8;
                break;
            default:
                sizeValue = 200;
                break;
        }
        blackRT.sizeDelta = new Vector2(sizeValue, sizeValue);
        foreach (var item in sidesRT)
        {
            item.sizeDelta = new Vector2(sizeValue, sizeValue);
        }
    }
    public void SetAnswerTransform()
    {
        switch (answerSide)
        {
            case "up":
                blackRT.localRotation = Quaternion.Euler(0, 0, 90);
                break;
            case "down":
                blackRT.localRotation = Quaternion.Euler(0, 0, 270);
                break;
            case "right":
                blackRT.localRotation = Quaternion.Euler(0, 0, 0);
                break;
            case "left":
                blackRT.localRotation = Quaternion.Euler(0, 0, 180);
                break;
            default:
                break;
        }
    }

    public void RandomSide()
    {
        lastSide = answerSide;
        List<string> availableSides = new List<string>();

        foreach (string side in allSides)
        {
            if (side != answerSide)
            {
                availableSides.Add(side);
            }
        }
        answerSide = availableSides[UnityEngine.Random.Range(0, availableSides.Count)];
    }
    public void TriggerSide(string side)
    {
        if (side == lastSide) return;
        audioSource.Play();
        if (side == answerSide) correct++;
        else wrong++;

        if (correct == 3)
        {
            correct = 0;
            wrong = 0;
            if (hadWrong == true)
            {
                SetResult();
                return;
            }
            if (curLevel == 11)
            {
                SetResult();
                return;
            }
            else
            {
                curLevel++;
                score = curLevel;
            }
        }

        if (wrong == 3)
        {
            correct = 0;
            wrong = 0;
            hadWrong = true;
            if (curLevel == 1)
            {
                SetResult();
                return;
            }
            else
            {
                curLevel--;
                score = curLevel;
            }
        }

        StartMeticTest();
    }
    public void SetResult()
    {
        scoreList.Add(score);
        score = 5;
        curLevel = 5;
        hadWrong = false;
        if (scoreList.Count == 1) gazeCalibrationManager.SetTrialCountDown("left");
        else if (scoreList.Count == 2) gazeCalibrationManager.SetTrialCountDown("both");
        else if (scoreList.Count == 3)
        {
            SaveScoreData();
            resultPage.SetActive(true);
            rightScore.text = GetEyeMetric(scoreList[0]).ToString();
            leftScore.text = GetEyeMetric(scoreList[1]).ToString();

            // 計算三次測試平均分數
            float avg = (scoreList[0] + scoreList[1] + scoreList[2]) / 3f;
            int avgScore = Mathf.RoundToInt(avg);
            avgScore = Mathf.Clamp(avgScore, 1, 11);
            float metric = GetEyeMetric(avgScore);

            // 取得結果文字並顯示 + TTS
            string message = GetResultMessage(metric, scoreList[0], scoreList[1]);
            resultText.text = message;
            PlayTTS(message);
        }
    }

    private string GetResultMessage(float metric, int rightEyeScore, int leftEyeScore)
    {
        // 雙眼視力 score 差距 > 2 時優先使用差異提示
        if (Mathf.Abs(leftEyeScore - rightEyeScore) > 2)
        {
            string[] diffMsgs = new string[]
            {
                "兩眼視力差距較大，可能會影響日常看東西。建議安排眼科或驗光檢查，確認度數是否需要調整。",
                "雙眼有明顯視力差異，建議安排驗光或眼科檢查。",
                "雙眼差距較大，建議驗光或眼科檢查。"
            };
            return diffMsgs[UnityEngine.Random.Range(0, diffMsgs.Length)];
        }

        string[] msgs;

        if (metric <= 0.2f)
        {
            msgs = new string[]
            {
                "視力已影響日常活動，建議儘快檢查。",
                "看東西會模糊，需要靠近才清楚，可能與度數或眼睛健康有關。",
                "建議儘速就醫，越早檢查越能保護視力。"
            };
        }
        else if (metric <= 0.4f)
        {
            msgs = new string[]
            {
                "視力偏低，看遠的清晰度會明顯下降，建議安排眼科檢查。",
                "這個視力等級，看電視、開車、走路可能會不太穩。",
                "如果你有眼鏡，可能度數需要調整；若沒有，建議眼科看看。"
            };
        }
        else if (metric <= 0.7f)
        {
            msgs = new string[]
            {
                "視力比標準稍弱一點，可能是疲勞、近視或需要更新眼鏡度數。",
                "看東西可能偶爾會覺得不夠清楚，建議近期驗光或眼科檢查。",
                "視力稍低，若看遠會吃力，可考慮配眼鏡或檢查度數。"
            };
        }
        else if (metric <= 1.0f)
        {
            msgs = new string[]
            {
                "很棒！你的視力在正常範圍，看遠看近都沒問題。",
                "看起來眼睛狀況很好，日常生活完全足夠。",
                "目前視力正常，請保持充足休息與用眼習慣。"
            };
        }
        else
        {
            msgs = new string[]
            {
                "哇～你看得超清楚！你的眼睛超厲害的！",
                "你的視力比一般標準還要好，看遠的清晰度非常優秀。",
                "超級厲害！你有像老鷹一樣的視力喔～"
            };
        }

        return msgs[UnityEngine.Random.Range(0, msgs.Length)];
    }

    public void SaveScoreData()
    {
        ScoreData dataToSave = new ScoreData(scoreList);

        string json = JsonUtility.ToJson(dataToSave, true);

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string filePath = Path.Combine(directoryPath, FILE_PREFIX + timestamp + ".json");

        try
        {
            File.WriteAllText(filePath, json);
            Debug.Log("測驗分數儲存成功於: " + filePath);
        }
        catch (Exception e)
        {
            Debug.LogError("測驗分數儲存失敗: " + e.Message);
        }
    }
    private float GetEyeMetric(int eyeScore)
    {
        switch (eyeScore)
        {
            case 1:
                return 0.1f;
            case 2:
                return 0.2f;
            case 3:
                return 0.3f;
            case 4:
                return 0.4f;
            case 5:
                return 0.5f;
            case 6:
                return 0.7f;
            case 7:
                return 0.8f;
            case 8:
                return 1.0f;
            case 9:
                return 1.3f;
            case 10:
                return 1.5f;
            case 11:
                return 2.0f;
            default:
                return 1.0f;
        }
    }

    private void PlayTTS(string text)
    {
        if (text == "")
            return;
        Nuwa.stopTTS();
        Nuwa.startTTS(text);
    }
}
