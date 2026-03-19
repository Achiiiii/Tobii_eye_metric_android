using System;
using System.Collections;
using System.Collections.Generic;
using Tobii.Research.Unity;
using UnityEngine;
using UnityEngine.UI;

public class TobiiController : MonoBehaviour
{
    public RectTransform eyeTrackRect;
    public TMPro.TMP_Text content;
    public TMPro.TMP_Text contentUnder;
    public GameObject trackBoxGuide;
    public GameObject testChoice;
    public GameObject basicTest;
    public GameObject advancedTest;
    public GameObject test1;
    public GameObject test2;
    public GameObject test3;
    public GameObject blackTestBtn;
    public GameObject colorTestBtn;
    public GameObject startBtn;
    public GameObject blackTest;
    public GameObject colorTest;
    public GameObject backBtn;
    public Test2 test2Script;
    public Test3 test3Script;
    public GameObject pointer;
    public GameObject[] stars;
    public Sprite starFull;
    public Sprite starEmpty;
    public GazePointVisualizer gazePointVisualizer;
    public FollowGazePoint2D followGazePoint2D;
    public AudioSource audioSource;
    public AudioClip questionAudio;
    public AudioClip test1Audio;
    public AudioClip test2Audio;
    public AudioClip test3Audio;
    public AudioClip againAudio;
    public AudioClip blackTestAudio;
    public AudioClip colorTestAudio;
    public AudioClip[] resultAudios;
    public PointController blackPointController;
    public PointController colorPointController;
    public GameObject questionPage;
    public GameObject testPage;
    public DetectDistance detectDistance;
    public GameObject gazeCalibrationManagerGO;
    public GameObject canvasTrackBoxGO;

    [SerializeField]
    [Tooltip("This key will show or hide the track box guide.")]
    private KeyCode _toggleKey = KeyCode.None;

    private float testDuration;
    private float startTime;
    private float endTime;
    private bool isScale = true;
    private string test1String = "";
    private Page curPage = Page.home;

    private enum Page
    {
        home,
        basic,
        advanced,
        t1,
        t2,
        t3,
        black,
        color
    }

    // void Start()
    // {
    //     followGazePoint2D.SetRatio(eyeTrackRect.localScale.x);
    // }

    public void QuestionEnd()
    {
        questionPage.SetActive(false);
        gazeCalibrationManagerGO.SetActive(true);
        canvasTrackBoxGO.SetActive(true);
        detectDistance.OpenLock();
    }

    void Update()
    {
        if (Input.GetKeyDown(_toggleKey))
        {
            if (isScale)
            {
                eyeTrackRect.localScale = new Vector3(1, 1, 1);
            }
            else
            {
                eyeTrackRect.localScale = new Vector3(0.42f, 0.42f, 1);
            }
            // followGazePoint2D.SetRatio(eyeTrackRect.localScale.x);
            isScale = !isScale;
        }
    }
    public void BasicTestClick()
    {
        testChoice.SetActive(false);
        basicTest.SetActive(true);
        PlayAudio(questionAudio);
        curPage = Page.basic;
    }
    public void AdvancedTestClick()
    {
        testChoice.SetActive(false);
        advancedTest.SetActive(true);
        PlayAudio(questionAudio);
        curPage = Page.advanced;
    }

    public void Test1Click()
    {
        basicTest.SetActive(false);
        startBtn.SetActive(true);
        ContentControl(
            "請保持頭部不動，專心看著螢幕中央的紅十字10秒，總共做2次，每次結束會休息。\n\n準備好後，請凝視選項3秒。",
            "測驗時間：約 20 秒鐘"
        );
        curPage = Page.t1;
        PlayAudio(test1Audio);
    }
    public void Test2Click()
    {
        basicTest.SetActive(false);
        startBtn.SetActive(true);
        ContentControl(
            "請先注視畫面中央的原點, 當周圍出現另一個圓點時請快速且準確地將視線移動到原點上。\n\n準備好後，請凝視選項3秒。",
            "測驗時間：約 30 秒至 5 分鐘"
        );
        curPage = Page.t2;
        PlayAudio(test2Audio);
    }
    public void Test3Click()
    {
        basicTest.SetActive(false);
        startBtn.SetActive(true);
        ContentControl(
            "請您將視線跟著畫面中的圓點移動！\n\n準備好後，請凝視選項3秒。",
            "測驗時間：約 20 秒鐘"
        );
        curPage = Page.t3;
        PlayAudio(test3Audio);
    }

    public void BlackTestClick()
    {
        advancedTest.SetActive(false);
        startBtn.SetActive(true);
        ContentControl(
            "請依照數字順序（1 到 25），將視線依序注視螢幕上隨機分布的圓圈。每注視正確的數字，系統會自動連線。\n\n。準備好後，請凝視開始3秒。",
            "測驗時間：約 50 秒至 20 分鐘"
        );
        curPage = Page.black;
        PlayAudio(blackTestAudio);
    }
    public void ColorTestClick()
    {
        advancedTest.SetActive(false);
        startBtn.SetActive(true);
        ContentControl(
            "請依照圓圈內的數字順序，從 1 到 25 進行注視，並交替使用紅色與黃色數字（紅1 > 黃2 > 紅3 > 黃4...）。圓圈隨機分布於畫面中，注視正確會自動連線。\n\n。準備好後，請凝視開始3秒。",
            "測驗時間：約 3 至 8 分鐘。"
        );
        curPage = Page.color;
        PlayAudio(colorTestAudio);
    }

    public void StartClick()
    {
        followGazePoint2D.ToggleGazeDot(false);
        startBtn.SetActive(false);
        TestPageControl(true);
        startTime = Time.time;
        audioSource.Stop();
    }

    private void TestPageControl(bool value)
    {
        switch (curPage)
        {
            case Page.t1:
                test1.SetActive(value);
                if (value)
                {
                    followGazePoint2D.StartRecord();
                    followGazePoint2D.ToggleLineActive(true);
                    StartCoroutine(Test1Start("first"));
                }
                break;
            case Page.t2:
                test2.SetActive(value);
                if (value)
                {
                    followGazePoint2D.StartRecord();
                    followGazePoint2D.ToggleLineActive(true);
                    test2Script.Test2Start();
                }
                break;
            case Page.t3:
                test3.SetActive(value);
                if (value)
                {
                    followGazePoint2D.StartRecord();
                    followGazePoint2D.ToggleLineActive(true);
                    test3Script.Test3Start();
                }
                break;
            case Page.black:
                blackTest.SetActive(value);
                break;
            case Page.color:
                colorTest.SetActive(value);
                break;
            default:
                break;
        }
    }
    private IEnumerator Test1Start(string times)
    {
        yield return new WaitForSeconds(10f);
        if (test1String == "first")
        {
            test1String = "";
            followGazePoint2D.StopRecord("stability_2_", true);
            contentUnder.text = "";
            ShowResult(2);
        }
        else if (times == "first")
        {
            test1String = "first";
            followGazePoint2D.StopRecord("stability_1_", true);
            followGazePoint2D.ToggleGazeDot(true);
            ContentControl("休息一下，準備第二次測試。\n\n準備好後，請凝視選項3秒。", "");
            PlayAudio(againAudio);
            startBtn.SetActive(true);
        }
        test1.SetActive(false);
        followGazePoint2D.ToggleLineActive(false);

    }
    private void ContentControl(string _content, string _contentUnder)
    {
        content.text = _content;
        contentUnder.text = _contentUnder;
    }
    private void ShowResult(int score)
    {
        startBtn.SetActive(false);
        for (int i = 0; i < stars.Length; i++)
        {
            stars[i].SetActive(true);
            if (i <= score) stars[i].GetComponent<Image>().sprite = starFull;
            else stars[i].GetComponent<Image>().sprite = starEmpty;
        }
        switch (score)
        {
            case 0:
                content.text = "測驗結束！\n您的測驗結果需要再加油，持續練習會讓您越來越進步。";
                break;
            case 1:
                content.text = "測驗結束！\n您已達到不錯的水準！相信您能越來越好！";
                break;
            case 2:
                content.text = "測驗結束！\n您的測驗結果非常優秀！請繼續保持！";
                break;
            default:
                break;
        }
        PlayAudio(resultAudios[score]);
        StartCoroutine(DelayHideResult());
    }

    private IEnumerator DelayHideResult()
    {
        yield return new WaitForSeconds(10f);
        followGazePoint2D.ToggleGazeDot(true);
        foreach (GameObject star in stars)
        {
            star.SetActive(false);
        }
        startBtn.SetActive(false);
        switch (curPage)
        {
            case Page.t1:
            case Page.t2:
            case Page.t3:
                curPage = Page.basic;
                basicTest.SetActive(true);
                break;
            case Page.black:
            case Page.color:
                curPage = Page.advanced;
                advancedTest.SetActive(true);
                break;

        }
        ContentControl(
            "請問您今天想進行哪一項眼動測試呢？\n（凝視選項3秒）",
            ""
        );
        PlayAudio(questionAudio);
    }

    private void PlayAudio(AudioClip clip)
    {
        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.Play();
    }

    public void EndTest()
    {
        endTime = Time.time;
        testDuration = endTime - startTime;
        contentUnder.text = "測驗結果： " + Mathf.Round(testDuration).ToString() + " 秒";
        int score;
        if (curPage == Page.black)
        {
            if (testDuration <= 60) score = 2;
            else if (testDuration <= 73) score = 1;
            else score = 0;

        }
        else if (curPage == Page.color)
        {
            if (testDuration <= 180) score = 2;
            else if (testDuration <= 236) score = 1;
            else score = 0;
        }
        else if (curPage == Page.t2)
        {
            if (testDuration <= 30) score = 2;
            else if (testDuration <= 60) score = 1;
            else score = 0;
        }
        else
        {
            score = 2;
        }

        ShowResult(score);
        TestPageControl(false);
    }
    public void Test2End()
    {
        followGazePoint2D.StopRecord("pavisic_", true);
        followGazePoint2D.ToggleLineActive(false);
        contentUnder.text = "";
        EndTest();
    }
    public void Test3End(string times)
    {
        if (times == "first")
        {
            followGazePoint2D.StopRecord("track_1_", true);
            followGazePoint2D.ToggleGazeDot(true);
            ContentControl("休息一下，準備第二次測試。\n\n準備好後，請凝視選項3秒。", "");
            PlayAudio(againAudio);
            startBtn.SetActive(true);
        }
        else
        {
            followGazePoint2D.StopRecord("track_2_", true);
            contentUnder.text = "";
            ShowResult(2);
        }
        test3.SetActive(false);
        followGazePoint2D.ToggleLineActive(false);

    }
    public void Reset()
    {
        StopAllCoroutines();
        followGazePoint2D.ToggleGazeDot(true);
        followGazePoint2D.ToggleLineActive(false);
        followGazePoint2D.StopRecord("", false);
        pointer.SetActive(false);
        if (curPage == Page.black)
            blackPointController.Reset();
        if (curPage == Page.color)
            colorPointController.Reset();
        foreach (GameObject star in stars)
        {
            star.SetActive(false);
        }

        trackBoxGuide.gameObject.SetActive(true);
        // trackBoxGuide.TrackBoxGuideActive = true;
        testChoice.SetActive(true);
        basicTest.SetActive(false);
        advancedTest.SetActive(false);
        test1.SetActive(false);
        test2.SetActive(false);
        test3.SetActive(false);
        blackTest.SetActive(false);
        colorTest.SetActive(false);
        startBtn.SetActive(false);
        content.text = "請問您今天想進行哪一項眼動測試呢？\n（凝視選項3秒）";
        contentUnder.text = "";
        // trackBoxGuide.Reset();
        audioSource.Stop();
        test1String = "";
        test2Script.Reset();
        test3Script.Reset();
        curPage = Page.home;
    }
    public void BackBtn()
    {
        audioSource.Stop();
        switch (curPage)
        {
            case Page.home:
                followGazePoint2D.ToggleGazeDot(true);
                pointer.SetActive(false);
                trackBoxGuide.gameObject.SetActive(true);
                // trackBoxGuide.TrackBoxGuideActive = true;
                // trackBoxGuide.Reset();
                break;
            case Page.basic:
                curPage = Page.home;
                basicTest.SetActive(false);
                testChoice.SetActive(true);
                PlayAudio(questionAudio);
                break;
            case Page.advanced:
                curPage = Page.home;
                advancedTest.SetActive(false);
                testChoice.SetActive(true);
                PlayAudio(questionAudio);
                break;
            case Page.t1:
                followGazePoint2D.ToggleGazeDot(true);
                startBtn.SetActive(false);
                basicTest.SetActive(true);
                ContentControl(
                    "請問您今天想進行哪一項眼動測試呢？\n（凝視選項3秒）",
                    ""
                );
                PlayAudio(questionAudio);
                StopAllCoroutines();
                followGazePoint2D.ToggleLineActive(false);
                followGazePoint2D.StopRecord("", false);
                test1String = "";
                TestPageControl(false);
                curPage = Page.basic;
                break;
            case Page.t2:
                followGazePoint2D.ToggleGazeDot(true);
                startBtn.SetActive(false);
                basicTest.SetActive(true);
                ContentControl(
                    "請問您今天想進行哪一項眼動測試呢？\n（凝視選項3秒）",
                    ""
                );
                PlayAudio(questionAudio);
                StopAllCoroutines();
                followGazePoint2D.ToggleLineActive(false);
                followGazePoint2D.StopRecord("", false);
                test2Script.Reset();
                TestPageControl(false);
                curPage = Page.basic;
                break;
            case Page.t3:
                followGazePoint2D.ToggleGazeDot(true);
                startBtn.SetActive(false);
                basicTest.SetActive(true);
                ContentControl(
                    "請問您今天想進行哪一項眼動測試呢？\n（凝視選項3秒）",
                    ""
                );
                PlayAudio(questionAudio);
                followGazePoint2D.ToggleLineActive(false);
                followGazePoint2D.StopRecord("", false);
                test3Script.Reset();
                TestPageControl(false);
                curPage = Page.basic;
                break;
            case Page.black:
                followGazePoint2D.ToggleGazeDot(true);
                startBtn.SetActive(false);
                advancedTest.SetActive(true);
                ContentControl(
                    "請問您今天想進行哪一項眼動測試呢？\n（凝視選項3秒）",
                    ""
                );
                PlayAudio(questionAudio);
                blackPointController.Reset();
                TestPageControl(false);
                curPage = Page.advanced;
                break;
            case Page.color:
                followGazePoint2D.ToggleGazeDot(true);
                startBtn.SetActive(false);
                advancedTest.SetActive(true);
                ContentControl(
                    "請問您今天想進行哪一項眼動測試呢？\n（凝視選項3秒）",
                    ""
                );
                PlayAudio(questionAudio);
                colorPointController.Reset();
                TestPageControl(false);
                curPage = Page.advanced;
                break;
        }
    }
}
