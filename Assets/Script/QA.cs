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

    // ========== 問卷文字（畫面顯示與存檔共用，可在 Inspector 中修改）==========
    [Header("題目（依序 Q1~Q7）")]
    [TextArea]
    public string[] displayQuestionTexts = new string[7]
    {
        "請問您是否為糖尿病患者呢？",
        "過去一年是否做過眼睛檢查？",
        "是否有視力問題，\n例如看遠看近或閱讀有困難？",
        "是否有眼科病史，請勾選:",
        "過去是否有做過眼科疾病的手術？",
        "眼科病手術史，請勾選:",
        "您是否戴著矯正器具進行測試？"
    };

    [Header("是非題按鈕（依序 Q1~Q7，勾選題留空）")]
    public string[] displayYesLabels = new string[7] { "是", "是", "有", "", "是", "", "" };
    public string[] displayNoLabels = new string[7] { "否", "否", "無", "", "否", "", "" };

    [Header("勾選題")]
    public string displayNextLabel = "繼續";
    public string[] displayQ4Options = new string[7]
    {
        "無", "近視／遠視／散光", "弱視", "青光眼", "白內障", "黃斑部/視網膜疾病", "其他"
    };
    // 場景裡原本只有 5 個勾選框；多出的選項（無）會自動複製一列並顯示在最上方
    public string[] displayQ6Options = new string[6]
    {
        "白內障手術", "屈光手術(近視雷射等)", "青光眼手術", "黃斑部/視網膜手術", "其他", "無"
    };
    public string[] displayQ7Options = new string[4]
    {
        "是，眼鏡", "是，隱形眼鏡", "否", "角膜塑型片（昨夜佩戴）"
    };

    // ========== JSON 資料結構 ==========
    [Serializable]
    public class QuestionEntry
    {
        public int questionNumber;
        public string questionText;
        public string questionType;       // "是非題", "多選題", "單選題"
        public string boolAnswer;         // 是非題答案：該題「是／否」按鈕上的文字
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

    // 多選題答案 (Q4, Q6)，長度依選項數量
    private bool[] q4Selections = new bool[0];
    private bool[] q6Selections = new bool[0];

    // 單選題答案 (Q7)
    private int q7Selection = -1; // -1 表示尚未選擇

    // 勾選題的「繼續」按鈕與提示文字，由 QuestionnaireText 建立畫面時登記
    private readonly Button[] nextButtons = new Button[TOTAL_QUESTIONS];
    private readonly GameObject[] nextHints = new GameObject[TOTAL_QUESTIONS];

    void Awake()
    {
        directoryPath = Application.persistentDataPath;
        q4Selections = new bool[displayQ4Options.Length];
        q6Selections = new bool[displayQ6Options.Length];
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

    // ==================== 多選題（Q4）====================

    /// <summary>
    /// 切換第4題的選項（Toggle），optionIndex 對應 displayQ4Options
    /// </summary>
    public void ToggleQ4Option(int optionIndex)
    {
        if (optionIndex >= 0 && optionIndex < q4Selections.Length)
        {
            q4Selections[optionIndex] = !q4Selections[optionIndex];
        }
        Q4Toggles[optionIndex].DOFade(q4Selections[optionIndex] ? 1 : 0, 0);
        RefreshGate(3);
    }

    /// <summary>
    /// 第4題確認送出，按下後前進到下一題
    /// </summary>
    public void ConfirmQ4()
    {
        // 沒有勾選任何項目時按鈕是關閉的；這裡再擋一次，避免其他路徑繞過
        if (!HasSelection(3))
            return;
        NextQuestion(3); // Q4 的 index = 3
    }

    // ==================== 多選題（Q6）====================

    /// <summary>
    /// 切換第6題的選項（Toggle），optionIndex 對應 displayQ6Options
    /// </summary>
    public void ToggleQ6Option(int optionIndex)
    {
        if (optionIndex >= 0 && optionIndex < q6Selections.Length)
        {
            q6Selections[optionIndex] = !q6Selections[optionIndex];
        }
        Q6Toggles[optionIndex].DOFade(q6Selections[optionIndex] ? 1 : 0, 0);
        RefreshGate(5);
    }

    /// <summary>
    /// 第6題確認送出，按下後前進到下一題
    /// </summary>
    public void ConfirmQ6()
    {
        // 沒有勾選任何項目時按鈕是關閉的；這裡再擋一次，避免其他路徑繞過
        if (!HasSelection(5))
            return;
        NextQuestion(5); // Q6 的 index = 5
    }

    // ==================== 單選題（Q7）====================

    /// <summary>
    /// 選擇第7題的選項，optionIndex 對應 displayQ7Options
    /// </summary>
    public void SelectQ7Option(int optionIndex)
    {
        if(q7Selection >= 0)
        {
            Q7Toggles[q7Selection].DOFade(0, 0);
        }
        if (optionIndex >= 0 && optionIndex < displayQ7Options.Length)
        {
            q7Selection = optionIndex;
        }
        Q7Toggles[optionIndex].DOFade(1, 0);
        RefreshGate(6);
    }

    /// <summary>
    /// 第7題確認送出（最後一題，會儲存資料）
    /// </summary>
    public void ConfirmQ7()
    {
        // 沒有勾選任何項目時按鈕是關閉的；這裡再擋一次，避免其他路徑繞過
        if (!HasSelection(6))
            return;
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
            ShowQuestion(currentIndex + 1);
        }
    }

    // ==================== 儲存 JSON ====================

    public void SaveQuestionnaireData()
    {
        QuestionnaireData dataToSave = new QuestionnaireData();
        dataToSave.completionTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        for (int i = 0; i < TOTAL_QUESTIONS; i++)
        {
            QuestionEntry entry = new QuestionEntry();
            entry.questionNumber = i + 1;
            // 畫面上的換行只為排版，存檔時去掉
            entry.questionText = displayQuestionTexts[i].Replace("\n", "");

            switch (i)
            {
                case 0: // Q1 是非題
                case 1: // Q2 是非題
                case 2: // Q3 是非題
                case 4: // Q5 是非題
                    entry.questionType = "是非題";
                    entry.boolAnswer = boolAnswers[i].HasValue
                        ? (boolAnswers[i].Value ? displayYesLabels[i] : displayNoLabels[i])
                        : "未作答";
                    break;

                case 3: // Q4 多選題
                    entry.questionType = "多選題";
                    entry.multiAnswers = SelectedOptions(q4Selections, displayQ4Options);
                    break;

                case 5: // Q6 多選題
                    entry.questionType = "多選題";
                    entry.multiAnswers = SelectedOptions(q6Selections, displayQ6Options);
                    break;

                case 6: // Q7 單選題
                    entry.questionType = "單選題";
                    entry.singleAnswer = q7Selection >= 0
                        ? displayQ7Options[q7Selection]
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

    private static List<string> SelectedOptions(bool[] selections, string[] options)
    {
        var selected = new List<string>();
        for (int j = 0; j < selections.Length && j < options.Length; j++)
        {
            if (selections[j])
            {
                selected.Add(options[j]);
            }
        }
        return selected;
    }

    /// <summary>
    /// 清空答案並回到第 1 題（給下一位受測者）
    /// </summary>
    public void RestartQuestionnaire()
    {
        ResetAnswers();
        questionPage.SetActive(true);
        // Silent: the mode menu speaks next, and BeginQuestionnaire() reads Q1 once it is picked.
        ShowQuestion(0, false);
    }

    /// <summary>
    /// 重置所有答案狀態
    /// </summary>
    public void ResetAnswers()
    {
        boolAnswers = new bool?[TOTAL_QUESTIONS];
        q4Selections = new bool[displayQ4Options.Length];
        q6Selections = new bool[displayQ6Options.Length];
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
        RefreshGate(3);
        RefreshGate(5);
        RefreshGate(6);
    }

    // ==================== 顯示、語音與勾選檢查 ====================

    /// <summary>
    /// 選完測驗模式後由 EyeMetricFlow 呼叫：顯示第一題並唸出題目
    /// </summary>
    public void BeginQuestionnaire()
    {
        ShowQuestion(0, true);
    }

    /// <summary>
    /// 由 QuestionnaireText 登記勾選題的「繼續」按鈕與提示文字
    /// </summary>
    public void RegisterNextGate(int index, Button next, GameObject hint)
    {
        if (!IsValidIndex(index))
            return;
        nextButtons[index] = next;
        nextHints[index] = hint;
        RefreshGate(index);
    }

    private void ShowQuestion(int index, bool speak = true)
    {
        for (int i = 0; i < questions.Length; i++)
            questions[i].SetActive(i == index);
        RefreshGate(index);
        if (speak)
            SpeakQuestion(index);
    }

    // Q4 與 Q6 是多選、Q7 是單選，三題都不能空白前進。
    private bool HasSelection(int index)
    {
        switch (index)
        {
            case 3: return AnySelected(q4Selections);
            case 5: return AnySelected(q6Selections);
            case 6: return q7Selection >= 0;
            default: return true;
        }
    }

    private static bool AnySelected(bool[] selections)
    {
        foreach (var selected in selections)
        {
            if (selected)
                return true;
        }
        return false;
    }

    // ButtonTrigger 只對 interactable 的按鈕累積注視時間，所以關掉按鈕就等於擋住視線選取。
    private void RefreshGate(int index)
    {
        if (!IsValidIndex(index))
            return;
        bool ready = HasSelection(index);
        if (nextButtons[index] != null)
            nextButtons[index].interactable = ready;
        if (nextHints[index] != null)
            nextHints[index].SetActive(!ready);
    }

    private void SpeakQuestion(int index)
    {
        if (index < 0 || index >= displayQuestionTexts.Length)
            return;
        // 換行只是排版用的，句尾的冒號和逗號唸出來會斷得很奇怪，都先去掉。
        string text = displayQuestionTexts[index].Replace("\n", "").TrimEnd('：', ':', '，', ',', ' ');

        // 題目本身已經寫了「請勾選」就不用再補一次。
        if ((index == 3 || index == 5) && !text.Contains("勾選"))
            text = Join(text, "請勾選所有符合的項目");
        else if (index == 6)
            text = Join(text, "請選擇一項");

        PlayTTS(text);
    }

    // 問號或驚嘆號後面再加逗號會很怪，只有在句尾沒有標點時才補上。
    private static string Join(string sentence, string suffix)
    {
        if (sentence.Length == 0)
            return suffix;
        char last = sentence[sentence.Length - 1];
        bool ended = last == '？' || last == '?' || last == '！' || last == '!' || last == '。' || last == '.';
        return ended ? sentence + suffix : sentence + "，" + suffix;
    }

    private void PlayTTS(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;
        Nuwa.stopTTS();
        Nuwa.startTTS(text);
    }
}
