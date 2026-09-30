using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Tobii;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;


public class MetricTest : MonoBehaviour
{
    public enum TestMode
    {
        Single,
        Both
    }

    public RectTransform blackRT;
    public RectTransform[] sidesRT;
    public GameObject resultPage;
    public TMPro.TMP_Text leftScore;
    public TMPro.TMP_Text rightScore;
    public GazeCalibrationManager gazeCalibrationManager;
    public TMPro.TMP_Text resultText;
    public AudioSource audioSource;

    public TestMode Mode { get; set; } = TestMode.Single;
    // Raised on every answer; the next symbol follows immediately (HeadFollow uses the gap).
    public event Action Answered;
    public string FirstSide => Mode == TestMode.Both ? "both" : "right";

    private int curLevel = 5;
    private string answerSide = null;
    private readonly string[] allSides = { "up", "down", "right", "left" };
    private int correct = 0;
    private int wrong = 0;
    private int score = 5;
    private bool hadWrong = false;
    private List<int> scoreList = new List<int>();
    private GameObject bothEyesResult;
    private TMPro.TMP_Text bothEyesScore;

    // Colour of the label band in the result background art (Group 25.png).
    private static readonly Color ResultLabelBandColor = new Color32(0xFD, 0xEB, 0xD8, 0xFF);

    [Serializable]
    public class ScoreData
    {
        public List<int> scores = new List<int>();

        public string mode;

        public string completionTime;

        public ScoreData(List<int> collectedScores, TestMode testMode)
        {
            scores = collectedScores;
            mode = testMode == TestMode.Both ? "both" : "single";
            completionTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
    private string directoryPath;
    private const string FILE_PREFIX = "score_";

    void Awake()
    {
        directoryPath = Application.persistentDataPath;
        // Neutral selection sound: the test must not signal whether an answer was right.
        audioSource.clip = UiSounds.Tick;
    }

    public void StartMeticTest()
    {
        RandomSide();
        SetAnswerTransform();
        SetLevel(curLevel);
    }

    public void ResetSession()
    {
        scoreList.Clear();
        curLevel = 5;
        score = 5;
        correct = 0;
        wrong = 0;
        hadWrong = false;
        answerSide = null;
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
        Answered?.Invoke();
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

        // Single-eye mode: right eye, then left eye. Both-eyes mode: one round with both eyes.
        if (Mode == TestMode.Single && scoreList.Count == 1)
        {
            gazeCalibrationManager.SetTrialCountDown("left");
            return;
        }
        ShowFinalResult();
    }

    private void ShowFinalResult()
    {
        SaveScoreData();
        resultPage.SetActive(true);
        // The result page is drawn over this object; disable it so gaze can no longer select directions.
        gameObject.SetActive(false);

        float metric;
        bool largeEyeGap = false;
        var robotData = new Dictionary<string, string>();
        if (Mode == TestMode.Single)
        {
            ShowBothEyesLayout(false);
            rightScore.text = GetEyeMetric(scoreList[0]).ToString();
            leftScore.text = GetEyeMetric(scoreList[1]).ToString();

            // 右眼與左眼平均分數
            int avgScore = Mathf.Clamp(Mathf.RoundToInt((scoreList[0] + scoreList[1]) / 2f), 1, 11);
            metric = GetEyeMetric(avgScore);
            largeEyeGap = Mathf.Abs(scoreList[0] - scoreList[1]) > 2;
            robotData["vision_gap_flag"] = largeEyeGap ? "1" : "0";
        }
        else
        {
            ShowBothEyesLayout(true);
            metric = GetEyeMetric(scoreList[0]);
            bothEyesScore.text = metric.ToString();
        }

        robotData["vision_both_level"] = metric <= 0.3f ? "0" : (metric <= 0.5f ? "1" : "2");
        robotData["time"] = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeMilliseconds().ToString();
        foreach (var entry in robotData)
            Debug.Log(entry.Key + ": " + entry.Value);
        Debug.Log("metric: " + metric.ToString());
        RobotDataSender.SendData(robotData);

        // 取得結果文字並顯示 + TTS
        string message = GetResultMessage(metric, largeEyeGap);
        resultText.text = message;
        PlayTTS(message);
    }

    private void ShowBothEyesLayout(bool bothEyes)
    {
        leftScore.gameObject.SetActive(!bothEyes);
        rightScore.gameObject.SetActive(!bothEyes);
        if (bothEyes && bothEyesResult == null)
            BuildBothEyesResult();
        if (bothEyesResult != null)
            bothEyesResult.SetActive(bothEyes);
    }

    // The result background art has the 左眼/右眼 labels baked in; cover them and show a single both-eyes value.
    private void BuildBothEyesResult()
    {
        var background = (RectTransform)resultPage.transform.Find("BG");
        float artScale = background.rect.height / 600f;

        bothEyesResult = new GameObject("BothEyesResult", typeof(RectTransform));
        var group = (RectTransform)bothEyesResult.transform;
        group.SetParent(background, false);
        group.anchorMin = Vector2.zero;
        group.anchorMax = Vector2.one;
        group.offsetMin = Vector2.zero;
        group.offsetMax = Vector2.zero;

        foreach (float labelX in new[] { 300f, 728f })
        {
            var cover = new GameObject("LabelCover", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            cover.color = ResultLabelBandColor;
            cover.raycastTarget = false;
            PlaceOnArt(cover.rectTransform, group, new Vector2(labelX, 255f), new Vector2(124f, 64f), artScale);
        }

        var label = Instantiate(resultText, group);
        label.text = "雙眼視力";
        label.fontSize = 34;
        PlaceOnArt(label.rectTransform, group, new Vector2(400f, 342f), new Vector2(240f, 70f), artScale);

        bothEyesScore = Instantiate(rightScore, group);
        bothEyesScore.gameObject.SetActive(true);
        PlaceOnArt(bothEyesScore.rectTransform, group, new Vector2(610f, 342f), new Vector2(247f, 70f), artScale);
    }

    // artPixel is measured in the 1024x600 background image (origin top-left).
    private static void PlaceOnArt(RectTransform rect, Transform parent, Vector2 artPixel, Vector2 artSize, float artScale)
    {
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(artPixel.x - 512f, (600f - artPixel.y) * artScale);
        rect.sizeDelta = new Vector2(artSize.x, artSize.y * artScale);
    }

    private string GetResultMessage(float metric, bool largeEyeGap)
    {
        // 雙眼視力 score 差距 > 2 時優先使用差異提示
        if (largeEyeGap)
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
                "視力已影響日常活動，建議盡快檢查。",
                "看東西會模糊，需要靠近才清楚，可能與度數或眼睛健康有關。",
                "建議盡速就醫，越早檢查越能保護視力。"
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
                "哇！你看得超清楚！你的眼睛超厲害的！",
                "你的視力比一般標準還要好，看遠的清晰度非常優秀。",
                "超級厲害！你有像老鷹一樣的視力喔！"
            };
        }

        return msgs[UnityEngine.Random.Range(0, msgs.Length)];
    }

    public void SaveScoreData()
    {
        ScoreData dataToSave = new ScoreData(scoreList, Mode);

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
