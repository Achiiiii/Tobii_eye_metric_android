# Eye_Metric_V2 交接文件

凱比機器人（Nuwa Kebbi AIR_H300）上的眼動視力測驗。使用 Tobii Stream Engine 的 webcam processor，以機器人頭部的前鏡頭追蹤眼動，受測者用「注視」作答。本文件整理 2026-09 ～ 2026-10 這一輪的修改、設計理由、可調參數與已知限制。

---

## 1. 建置與安裝

| 項目 | 設定 |
|---|---|
| Unity | 2022.3.62f1 |
| 平台 | Android，IL2CPP，ARM64 |
| Graphics API | **只用 OpenGLES3**。Vulkan 在凱比的 Mali GPU 上會出現記憶體洩漏和當機，不要開啟 |
| 套件名稱 | `com.nuwarobotics.app.mibounity` |
| 場景 | `Assets/Scenes/TobiiSample_new.unity` |
| 版本 | `bundleVersion 0.1`、`AndroidBundleVersionCode 1`（發布前視需要調整） |

**命令列建置**（不用開 Unity 編輯器）：

```
Unity.exe -batchmode -quit -projectPath <專案路徑> -buildTarget Android -executeMethod AndroidBuild.BuildApk -apkPath <輸出.apk> -logFile <log 檔>
```

建置腳本在 `Assets/Editor/AndroidBuild.cs`，編輯器選單 `Build > Build Android APK` 也可以。log 中唯一預期會出現的錯誤，是未使用的 GrayscaleURP shader，可以忽略。

**安裝與重開**：

```
adb install -r Eye_Metric_V2.apk
adb shell am force-stop com.nuwarobotics.app.mibounity
adb shell am start -n com.nuwarobotics.app.mibounity/com.u2a.sdk.NuwaUnityPlayerActivity
```

> batchmode 建置完成後 adb server 會被關掉。下一次 adb 指令會自動重啟，但凱比會先顯示 `unauthorized` 幾秒到一分鐘。先執行 `adb start-server`，等狀態變成 `device` 再安裝；如果一直是 unauthorized，就在凱比上按允許，或重開機器人。**不要用 kill adb.exe 的方式停止 logcat**，會讓凱比失去授權，只能重開機器人。

---

## 2. 測驗流程（使用者看到的）

1. **頭部位置確認**：距離機器人 30～50 cm，並且眼睛高度在機器人頭部能俯仰追到的範圍內，維持 3 秒即通過。畫面上有距離刻度尺和高度刻度尺，並有語音引導。
2. **眼動校準**：依序注視 5 個點，順序以琥珀色編號標示。
3. **測驗**：每題先只顯示中央的 E。受測者看過中央後，四個選項才以彈出動畫出現，接著注視選項 1.5 秒作答，進度圈會跟著填滿。
4. 單眼測驗在遮眼之後**重新校準**，再進行測驗。
5. **問卷**：題目有語音朗讀；多選題至少要勾一項才能繼續；「無」和其他選項互斥。

測驗中機器人頭部會轉動，讓受測者保持在校準時的位置。如果受測者明顯偏離，會暫停作答，並引導他回到原位。

---

## 3. 這一輪的修改

### 3.1 效能

| 修改 | 結果 |
|---|---|
| 相機從 13 MP 改成 2100×1560，再降採樣 ×2，變成 **1050×780** 交給 Tobii | 畫面約 7 fps → 29 fps |
| Tobii 推論移到背景執行緒（`GazeFrameWorker`），只處理最新一幀，處理不完的舊幀直接丟棄 | 主執行緒不再卡住 |
| 相機固定 15 fps | 降低發熱；Tobii 一幀約 75～90 ms，實際眼動更新率約 11～13 Hz |

- 降採樣 ×3（700×520）實測：推論時間一樣（約 84 ms），延遲沒有改善，準確度反而變差（78 px 對 64 px）。**Tobii 的推論時間跟輸入解析度無關，請維持 ×2。**
- 紅點延遲的下限就是 Tobii 的 CPU 推論時間。凱比有 NPU，但 Tobii 的 `libtobii_processor_nexus.so` 內嵌的 ONNX Runtime 只有 CPU provider，模型也有加密，我們這邊沒辦法改用 NPU。

### 3.2 準確度

| 機制 | 檔案 | 說明 |
|---|---|---|
| 注視平滑 | `FixationSmoother.cs` | 取 3 點中位數去除尖峰，眼跳要連續 2 點才確認；跳動大於 2 倍半徑時直接跳過去 |
| 飄移校正 | `DriftCorrector.cs` | 5 個參考點（中央、上、下、左、右）各自記錄 2D 誤差，取最近 5 筆的中位數，第一筆只用一半權重。用中央、最近的水平選項、最近的垂直選項三點做重心插值，修正上限 150 px |
| 先看中央再出選項 | `OptionReveal.cs` | 偵測到受測者看過中央 E 後，選項才出現；最多等到頭停止轉動後 1.2 s，或總共 2 s |
| 選擇閘門 | `GazeSelectionGate.cs` | 因看中央而出現選項時，紅點必須先移動超過 60 px，才能開始選擇。避免單眼誤差大時，看中央就直接落在選項上而誤選 |
| 頭部追蹤 | `HeadFollow.cs` | 轉動凱比的 neck_z 和 neck_y，讓受測者保持在校準時的位置 |
| 回位引導 | `PositionGuide.cs` | 左右偏離超過 10° 或距離偏離超過 5 cm，且持續 1.5 s，就暫停作答，顯示刻度尺並以語音引導受測者回到原位 |

**飄移校正的樣本來源**：中央取看中央時的穩定注視；選項取作答前最後 1 秒、注視在**受測者實際選的選項**上的資料。不假設受測者看的是正確答案。

實測：雙眼約 46 → 29 px，單眼誤差約減少 58%；選擇閘門讓「選項出現 1.7 s 內就完成作答」的誤選從最多 38% 降到 0%。

### 3.3 頭部追蹤（`HeadFollow.cs`）

- 用 Tobii head pose 算出受測者偏離校準位置的角度，以 8 Hz、增益 0.8 轉動頭部。每步最多 10°、最少 3°（凱比馬達常常忽略 1～3° 的小動作）。
- 模式（`FollowMode`）預設是 **BetweenTrials**：只在題目之間或偏差很大時才轉，作答時不動。
- 實測的馬達極限：yaw ±30°，pitch −14.5°（往上）～ +12°（往下）。pitch 往上的停止點會浮動，曾經停在 −13.6°。
- 下指令後如果馬達沒動，就當作卡住：退讓 3 秒；如果是頂在範圍盡頭，退讓 10 秒。**不要把馬達沒動的位置記成永久極限**，以前這樣做會讓頭卡在一側。
- 方向：neck_z 正值是機器人轉向它的右邊；neck_y 正值是往下看。程式裡 yaw 的 sign 是 +1、pitch 是 −1，都在機器人上確認過。

### 3.4 頭部位置確認（`DetectDistance.cs`、`HeadDistanceGuide.cs`）

- 距離 30～50 cm 並維持 3 秒。
- **高度檢查**：受測者眼睛相對於機器人的仰角，必須在 −15°～+17.5° 之間（頭部俯仰範圍再加 3° 餘裕）。超出範圍持續 1 秒就不給通過，畫面右側的高度刻度尺會顯示位置，提示句也會附上「約差 N 公分」。
- 如果現場**無法調整高度**（例如機器人不能墊高、椅子不能調），受測者會無法進入測驗。目前沒有略過機制，有需要可以另外加一個工作人員用的略過方式。

### 3.5 介面與問卷

- 問卷題目語音朗讀；多選題沒勾不能繼續；「無」和其他選項互斥（`QA.cs` 的 `ClearConflictingOptions`）。
- 校準點的順序編號改成琥珀色，避免跟藍色的校準點混淆。
- 選項以 DOTween OutBack 彈出，從 0.2 倍放大到原尺寸，耗時 0.3 s。
- 注視進度圈、作答音效、各階段的轉場畫面。

---

## 4. 主要檔案

| 檔案 | 用途 |
|---|---|
| `Assets/Script/EyeMetricFlow.cs` | App 流程總控，負責建立下面多數元件 |
| `Assets/Script/HeadFollow.cs` | 機器人頭部追蹤，也提供高度判斷 `EyeLevelMismatch` |
| `Assets/Script/DetectDistance.cs`、`HeadDistanceGuide.cs` | 頭部位置確認的邏輯與畫面 |
| `Assets/Script/PositionGuide.cs` | 測驗中的回位引導 |
| `Assets/Script/DriftCorrector.cs` | 飄移校正 |
| `Assets/Script/OptionReveal.cs`、`GazeSelectionGate.cs` | 先看中央、選擇閘門 |
| `Assets/Script/MetricTest.cs` | 測驗題目、選項顯示與動畫 |
| `Assets/Script/ButtonTrigger.cs`、`GazeDwellIndicator.cs` | 注視選擇（1.5 s）、選擇後冷卻 1 s、進度圈 |
| `Assets/Script/FixationSmoother.cs` | 注視平滑 |
| `Assets/Script/QA.cs`、`QuestionnaireText.cs` | 問卷 |
| `Assets/Script/GazeDebugOverlay.cs`、`GazeLatencyStats.cs` | 隱藏的診斷面板 |
| `Assets/Tobii/Scripts/GazeFrameWorker.cs` | Tobii 推論背景執行緒 |
| `Assets/Tobii/Scripts/AndroidWebcamCaptureClient.cs` | 從相機外掛取影像、送去推論 |
| `Assets/Tobii/Scripts/StreamEngineDevice.cs`、`StreamEngineCalibration.cs` | Tobii 連線與校準 |
| `Assets/Tobii/Scripts/GazeCalibrationManager.cs` | 校準流程 |
| `Assets/Tobii/Plugins/Android/com/tobii/AndroidCameraPlugin.java` | Camera2 外掛：選解析度、降採樣、15 fps |

**診斷面板**：在右上角網路圖示連點 5 下打開，只顯示數據，包括相機 fps、推論時間、眼動更新率、延遲、頭部狀態、飄移校正狀態，不能調整任何設定。

---

## 5. Log

**抓 log**：

```
adb logcat -d -s Unity TobiiRT > session.log
```

Tobii 自己的訊息在 **`TobiiRT`** 標籤下，例如校準狀態、`calib id`、影格錯誤。只抓 `Unity` 標籤會看不到。

| 標籤 | 內容 |
|---|---|
| `[PERF]` | 每 5 秒一次：推論時間、相機 fps、眼動 Hz、畫面 fps、紅點追上延遲 |
| `[HEAD]` / `[MOTOR]` | 頭部追蹤狀態（每 5 秒）／每次馬達指令的結果 |
| `[HEIGHT]` | 高度不符而擋住頭部確認 |
| `[GUIDE]` | 回位引導的開始與結束 |
| `[REVEAL]` / `[GATE]` | 選項出現的時間與原因／選擇閘門擋下的選擇 |
| `[DRIFT]` / `[FIX]` / `[OPTION]` | 中央樣本／目前的校正量／每次作答時的原始誤差（評估準確度最好用的數據） |
| `Calibration finished: ...` | 校準結果 |

一般 log 已關閉堆疊追蹤，錯誤和例外仍會保留。

---

## 6. 已知限制與調查結論

**Tobii 校準**
- `tobii_calibration_compute_and_apply` 在這套 webcam processor 上**每次都回傳 `TOBII_ERROR_INTERNAL`**，但 `TobiiRT` log 顯示校準編號有更新，實際上已經套用。程式現在改用「校準編號有沒有改變」來判斷成功（`StreamEngineCalibration.cs`）。
- 在同一輪測驗中，讓「有校準」和「清除校準」逐題交替，兩組選項的原始誤差都是 44 px：**在這套模型上，5 點校準對準確度幾乎沒有幫助**，目前的準確度主要來自 Tobii 基礎模型加上飄移校正。這只是一輪、約 20 題的數據；如果要縮短校準（例如改成 1 點，或單眼不重新校準），建議先多做幾輪，包含單眼測驗，確認結果一致再改。
- 清除校準要先 `tobii_calibration_start`，否則會回傳 `CALIBRATION_NOT_STARTED`。
- Tobii 附的 `ConfigInterop.tobii_calibration_retrieve` 用 lambda 當原生回呼，**在 IL2CPP 下會拋出例外**。需要時請自己寫 DllImport，並使用加上 `[MonoPInvokeCallback]` 的靜態回呼。

**單眼**
- webcam processor 不支援只追蹤一眼（`tobii_set_enabled_eye` 回傳 `NOT_SUPPORTED`）。單眼校準和「睜雙眼只校準一次」都試過，都沒有比較好。單眼的原始誤差約 72～83 px，是 Tobii 本身的限制。
- **自動判斷是否遮眼**：試過 Tobii 的 per-eye 資料。閉眼時，eye openness 從約 7.5 降到 0.2，非常明顯；但手掌遮著張開的眼睛時，數值幾乎不變，兩眼也都回報有效。所以無法用 Tobii 的資料判斷遮眼。如果要做，需要自己分析鏡頭畫面中眼睛區域的對比。

**其他**
- 凱比的 pitch 往上停止點會浮動，見 3.3。
- 受測者坐得太高時，頭部會頂在 pitch 上限，追蹤能力受限。測驗中的回位引導只檢查左右和距離，不檢查高度。

---

## 7. 開發注意事項

- **字型**：TMP 中文字型 `msjh SDF` 只有 3793 個字，**沒有英數字**，也沒有「儘」「～」。新增任何畫面文字都要先確認字型裡有這些字。英數字請用 `UiFactory.WithLatinFallback` 改用預設字型。
- **新增 C# 檔案**時，要一併提交 Unity 產生的 `.meta` 檔。
- **執行緒**：Tobii 的回呼在 `GazeFrameWorker` 的背景執行緒觸發。校準相關的呼叫透過 `Task.Run` 執行，不要跟 frame 處理共用同一把鎖，否則可能死結。
- 凱比只有一顆前鏡頭，同一時間只能有一個 app 使用。
