using UnityEngine;
using System.IO;

public class LogToFile : MonoBehaviour
{
    private string logFilePath;

    void Awake()
    {
        // 設定日誌檔案路徑
        logFilePath = Path.Combine(Application.persistentDataPath, "crash_log.txt");

        // 每次運行時，清空舊的日誌檔案
        if (File.Exists(logFilePath))
        {
            File.Delete(logFilePath);
        }

        // 註冊日誌回調函數，所有日誌都會觸發這個函數
        Application.logMessageReceived += HandleLog;
    }

    void OnDestroy()
    {
        // 移除日誌回調，以避免重複監聽
        Application.logMessageReceived -= HandleLog;
    }

    void HandleLog(string logString, string stackTrace, LogType type)
    {
        // 格式化日誌訊息
        string logEntry = $"[{type}] {System.DateTime.Now}: {logString}\n{stackTrace}\n";

        // 將日誌寫入檔案
        File.AppendAllText(logFilePath, logEntry);
    }
}