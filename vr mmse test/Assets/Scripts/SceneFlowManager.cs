using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;   // Process
using System.IO;

public class SceneFlowManager : MonoBehaviour
{
    public static SceneFlowManager instance;

    private readonly List<string> sceneOrder = new List<string>
    {
        "SampleScene_5",
        "SampleScene_3",
        "SampleScene_11_1",
        "SampleScene_11",
        "SampleScene_2",
        "SampleScene_11"
    };

    private int currentIndex = 0;

    [Header("Fade UI")]
    public Image fadeImage;
    public float fadeDuration = 3f;

    // === 追蹤目前啟動中的 Python 伺服器 ===
    private Process currentServerProcess = null;
    private string currentServerKey = null; // 可用來記錄是哪支腳本（debug用）

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void LoadNextScene()
    {
        currentIndex++;
        if (currentIndex >= sceneOrder.Count)
        {
            UnityEngine.Debug.Log("流程結束，回到第一個場景");
            currentIndex = 0;
        }

        string nextScene = sceneOrder[currentIndex];
        StartCoroutine(LoadSceneRoutine(nextScene));
    }

    public IEnumerator LoadSceneRoutine(string nextScene)
    {
        // 1) 黑幕淡入
        yield return StartCoroutine(Fade(0f, 1f));

        // 2) 在離開目前場景前，先關掉舊伺服器
        StopCurrentServer();

        // 3) 非阻塞載入新場景
        AsyncOperation op = SceneManager.LoadSceneAsync(nextScene, LoadSceneMode.Single);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f) yield return null;
        op.allowSceneActivation = true;

        // 4) 等一幀讓場景物件初始化
        yield return null;

        // 4.5) 額外等 XR Origin 初始化（避免視角卡住）
        yield return new WaitForSeconds(3f);

        // 5) 依新場景啟動對應 Python 伺服器
        StartServerForScene(nextScene);

        // 6) 黑幕淡出
        yield return StartCoroutine(Fade(1f, 0f));
    }

    // 依場景名稱啟動對應 Python 伺服器（會記錄到 currentServerProcess）
    public void StartServerForScene(string sceneName)
    {
        string pythonExe = "python";  // 若需要，可改成絕對路徑或 "py"、"python3"
        string workingDir = Path.Combine(Application.dataPath, "Scripts");

        string scriptToRun = "";

        switch (sceneName)
        {
            case "SampleScene_5":
                scriptToRun = "audio_5.py";  // 你的 whisper/Google Web Speech 腳本
                break;
            case "SampleScene_3":
                scriptToRun = "audio_5.py";  // 你的 whisper/Google Web Speech 腳本
                break;
            case "SampleScene_2":
                scriptToRun = "app_2.py";
                break;
            // 其他場景再依需求加 case
        }

        if (string.IsNullOrEmpty(scriptToRun))
        {
            UnityEngine.Debug.Log($"[SceneFlow] 場景 {sceneName} 不需啟動伺服器。");
            return;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = scriptToRun,
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var p = new Process();
            p.StartInfo = psi;

            p.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    UnityEngine.Debug.Log($"[Python:{scriptToRun}] {e.Data}");
            };

            p.ErrorDataReceived += (sender, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;

                string line = e.Data;

                // 常見無害訊息（依實際情況可再補關鍵字）
                if (
                    line.Contains("Running on http://") ||
                    line.Contains("Running on all addresses (0.0.0.0)") ||
                    line.Contains("Press CTRL+C to quit") ||
                    line.Contains("Debugger PIN:") ||
                    line.Contains("This is a development server") ||
                    line.Contains("Restarting with stat"))
                {
                    UnityEngine.Debug.Log($"[Python-Info:{scriptToRun}] {line}");
                }
                else
                {
                    UnityEngine.Debug.LogError($"[Python-Error:{scriptToRun}] {line}");
                }
            };

            bool started = p.Start();
            if (started)
            {
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();

                currentServerProcess = p;
                currentServerKey = scriptToRun;
                UnityEngine.Debug.Log($"[SceneFlow] 已啟動伺服器：{scriptToRun}（PID={p.Id}）");
            }
            else
            {
                UnityEngine.Debug.LogError($"[SceneFlow] 無法啟動伺服器：{scriptToRun}");
            }
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"[SceneFlow] 啟動伺服器失敗：{scriptToRun}，錯誤：{ex.Message}");
        }
    }

    // 關掉當前伺服器
    private void StopCurrentServer()
    {
        if (currentServerProcess == null) return;

        try
        {
            if (!currentServerProcess.HasExited)
            {
                currentServerProcess.Kill(); // 不要帶參數
                UnityEngine.Debug.Log($"[SceneFlow] 已關閉伺服器：{currentServerKey}（PID={currentServerProcess.Id}）");
            }
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogWarning($"[SceneFlow] 關閉伺服器發生例外：{ex.Message}");
        }
        finally
        {
            currentServerProcess.Dispose();
            currentServerProcess = null;
            currentServerKey = null;
        }
    }

    private IEnumerator Fade(float from, float to)
    {
        if (fadeImage == null) yield break;

        float t = 0f;
        Color c = fadeImage.color;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(from, to, t / fadeDuration);
            fadeImage.color = new Color(c.r, c.g, c.b, a);
            yield return null;
        }

        fadeImage.color = new Color(c.r, c.g, c.b, to);
    }

    // 遊戲退出/物件被銷毀時確保關閉伺服器
    private void OnApplicationQuit()
    {
        StopCurrentServer();
    }

    private void OnDestroy()
    {
        StopCurrentServer();
    }
}
