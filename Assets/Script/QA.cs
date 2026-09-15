using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class QA : MonoBehaviour
{
    public GameObject[] questions;
    public GameObject questionPage;
    public GameObject gazeCalibrationManagerGO;
    public GameObject canvasTrackBoxGO;
    public DetectDistance detectDistance;
    public event Action Completed;
    public Image[] Q4Toggles = new Image[7];
    public Image[] Q6Toggles = new Image[5]; 
    public Image[] Q7Toggles = new Image[4]; 

    // ========== 題目文字（可在 Inspector 中設定）==========
    [Header("題目文字")]
    public string question1Text = "第1題（是非題）";
    public string question2Text = "第2題（是非題）";
    public string question3Text = "第3題（是非題）";
    public string question4Text = "第4題（7選項多選題）";
    public string question5Text = "第5題（是非題）";
    public string question6Text = "第6題（5選項多選題）";
    public string question7Text = "第7題（4選項單選題）";

    // ========== 選項文字（可在 Inspector 中設定）==========
    [Header("第4題選項文字（7個）")]
    public string[] question4Options = new string[7]
    {
        "選項A", "選項B", "選項C", "選項D", "選項E", "選項F", "選項G"
    };

    [Header("第6題選項文字（5個）")]
    public string[] question6Options = new string[5]
    {
        "選項A", "選項B", "選項C", "選項D", "選項E"
    };

    [Header("第7題選項文字（4個）")]
    public string[] question7Options = new string[4]
    {
        "選項A", "選項B", "選項C", "選項D"
    };

    // ========== JSON 資料結構 ==========
    [Serializable]
    public class QuestionEntry
    {
        public int questionNumber;
        public string questionText;
        public string questionType;       // "是非題", "多選題", "單選題"
        public string boolAnswer;         // 是非題答案："是" / "否"
        public List<string> multiAnswers;  // 多選題答案
        public string singleAnswer;       // 單選題答案
    }

    [Serializable]
    public class QuestionnaireData
    {
        public List<QuestionEntry> questions = new List<QuestionEntry>();
        public string completionTime;
    }

    // ========== 內部狀態 ==========
    private const int TOTAL_QUESTIONS = 7;
    private string directoryPath;
    private const string FILE_PREFIX = "health_quiz_";

    // 是非題答案 (Q1, Q2, Q3, Q5)
    private bool?[] boolAnswers = new bool?[TOTAL_QUESTIONS]; // index 0~6 對應 Q1~Q7

    // 多選題答案 (Q4: 7個選項, Q6: 5個選項)
    private bool[] q4Selections = new bool[7];
    private bool[] q6Selections = new bool[5];

    // 單選題答案 (Q7: 4個選項)
    private int q7Selection = -1; // -1 表示尚未選擇

    void Awake()
    {
        directoryPath = Application.persistentDataPath;
    }

    // ==================== 是非題（Q1, Q2, Q3, Q5）====================

    /// <summary>
    /// 是非題按「是」, index 為題號 (0=Q1, 1=Q2, 2=Q3, 4=Q5)
    /// </summary>
    public void ButtonYes(int index)
    {
        if (IsValidIndex(index))
        {
            boolAnswers[index] = true;
            NextQuestion(index);
        }
    }

    /// <summary>
    /// 是非題按「否」, index 為題號 (0=Q1, 1=Q2, 2=Q3, 4=Q5)
    /// </summary>
    public void ButtonNo(int index)
    {
        if (IsValidIndex(index))
        {
            boolAnswers[index] = false;
            NextQuestion(index);
        }
    }

    // ==================== 多選題（Q4: 7個選項）====================

    /// <summary>
    /// 切換第4題的選項（Toggle），optionIndex: 0~6
    /// </summary>
    public void ToggleQ4Option(int optionIndex)
    {
        if (optionIndex >= 0 && optionIndex < q4Selections.Length)
        {
            q4Selections[optionIndex] = !q4Selections[optionIndex];
        }
        Q4Toggles[optionIndex].DOFade(q4Selections[optionIndex] ? 1 : 0, 0);
    }

    /// <summary>
    /// 第4題確認送出，按下後前進到下一題
    /// </summary>
    public void ConfirmQ4()
    {
        NextQuestion(3); // Q4 的 index = 3
    }

    // ==================== 多選題（Q6: 5個選項）====================

    /// <summary>
    /// 切換第6題的選項（Toggle），optionIndex: 0~4
    /// </summary>
    public void ToggleQ6Option(int optionIndex)
    {
        if (optionIndex >= 0 && optionIndex < q6Selections.Length)
        {
            q6Selections[optionIndex] = !q6Selections[optionIndex];
        }
        Q6Toggles[optionIndex].DOFade(q6Selections[optionIndex] ? 1 : 0, 0);
    }

    /// <summary>
    /// 第6題確認送出，按下後前進到下一題
    /// </summary>
    public void ConfirmQ6()
    {
        NextQuestion(5); // Q6 的 index = 5
    }

    // ==================== 單選題（Q7: 4個選項）====================

    /// <summary>
    /// 選擇第7題的選項，optionIndex: 0~3
    /// </summary>
    public void SelectQ7Option(int optionIndex)
    {
        if(q7Selection >= 0)
        {
            Q7Toggles[q7Selection].DOFade(0, 0);
        }
        if (optionIndex >= 0 && optionIndex < 4)
        {
            q7Selection = optionIndex;
        }
        Q7Toggles[optionIndex].DOFade(1, 0);
    }

    /// <summary>
    /// 第7題確認送出（最後一題，會儲存資料）
    /// </summary>
    public void ConfirmQ7()
    {
        NextQuestion(6); // Q7 的 index = 6，最後一題
    }

    // ==================== 共用邏輯 ====================

    private bool IsValidIndex(int index)
    {
        return index >= 0 && index < TOTAL_QUESTIONS;
    }

    private void NextQuestion(int currentIndex)
    {
        if (currentIndex == TOTAL_QUESTIONS - 1)
        {
            // 最後一題，儲存並結束
            SaveQuestionnaireData();
            questionPage.SetActive(false);
            gazeCalibrationManagerGO.SetActive(true);
            canvasTrackBoxGO.SetActive(true);
            detectDistance.OpenLock();
            Completed?.Invoke();
        }
        else
        {
            questions[currentIndex].SetActive(false);
            questions[currentIndex + 1].SetActive(true);
        }
    }

    // ==================== 儲存 JSON ====================

    public void SaveQuestionnaireData()
    {
        QuestionnaireData dataToSave = new QuestionnaireData();
        dataToSave.completionTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        string[] questionTexts = new string[]
        {
            question1Text, question2Text, question3Text,
            question4Text, question5Text, question6Text, question7Text
        };

        for (int i = 0; i < TOTAL_QUESTIONS; i++)
        {
            QuestionEntry entry = new QuestionEntry();
            entry.questionNumber = i + 1;
            entry.questionText = questionTexts[i];

            switch (i)
            {
                case 0: // Q1 是非題
                case 1: // Q2 是非題
                case 2: // Q3 是非題
                case 4: // Q5 是非題
                    entry.questionType = "是非題";
                    entry.boolAnswer = boolAnswers[i].HasValue
                        ? (boolAnswers[i].Value ? "是" : "否")
                        : "未作答";
                    break;

                case 3: // Q4 多選題（7個選項）
                    entry.questionType = "多選題";
                    entry.multiAnswers = new List<string>();
                    for (int j = 0; j < q4Selections.Length; j++)
                    {
                        if (q4Selections[j])
                        {
                            entry.multiAnswers.Add(question4Options[j]);
                        }
                    }
                    break;

                case 5: // Q6 多選題（5個選項）
                    entry.questionType = "多選題";
                    entry.multiAnswers = new List<string>();
                    for (int j = 0; j < q6Selections.Length; j++)
                    {
                        if (q6Selections[j])
                        {
                            entry.multiAnswers.Add(question6Options[j]);
                        }
                    }
                    break;

                case 6: // Q7 單選題（4個選項）
                    entry.questionType = "單選題";
                    entry.singleAnswer = q7Selection >= 0
                        ? question7Options[q7Selection]
                        : "未作答";
                    break;
            }

            dataToSave.questions.Add(entry);
        }

        string json = JsonUtility.ToJson(dataToSave, true);

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string filePath = Path.Combine(directoryPath, FILE_PREFIX + timestamp + ".json");

        try
        {
            File.WriteAllText(filePath, json);
            Debug.Log("問卷記錄儲存成功於: " + filePath);
        }
        catch (Exception e)
        {
            Debug.LogError("問卷記錄儲存失敗: " + e.Message);
        }
        ResetAnswers();
    }

    /// <summary>
    /// 清空答案並回到第 1 題（給下一位受測者）
    /// </summary>
    public void RestartQuestionnaire()
    {
        ResetAnswers();
        for (int i = 0; i < questions.Length; i++)
            questions[i].SetActive(i == 0);
        questionPage.SetActive(true);
    }

    /// <summary>
    /// 重置所有答案狀態
    /// </summary>
    public void ResetAnswers()
    {
        boolAnswers = new bool?[TOTAL_QUESTIONS];
        q4Selections = new bool[7];
        q6Selections = new bool[5];
        q7Selection = -1;
        foreach (var item in Q4Toggles)
        {
            item.DOFade(0, 0);
        }
        foreach (var item in Q6Toggles)
        {
            item.DOFade(0, 0);
        }
        foreach (var item in Q7Toggles)
        {
            item.DOFade(0, 0);
        }
    }
}
