using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;   // Process
using System.IO;
using System.Linq; // 為了使用 List<Process> 的 LINQ 方法 (例如 Add)

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

    // === 追蹤所有啟動中的 Python 伺服器 (常駐模式) ===
    private readonly List<Process> allServerProcesses = new List<Process>();

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);

            // **應用程式啟動時，一次性啟動所有需要的伺服器（分批啟動）**
            StartCoroutine(StartPersistentServers());
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // --- 修改：分批啟動常駐伺服器的方法 ---
    private IEnumerator StartPersistentServers()
    {
        // 先啟動 audio_5.py (Port 5000)
        StartPythonScript("audio_5.py");

        // 等待 2 秒
        yield return new WaitForSeconds(2f);

        // 再啟動 server_track.py (Port 5001)
        StartPythonScript("server_track.py");
    }


    // --- 將原有的 StartServerForScene 邏輯改為通用的啟動腳本方法 ---
    public void StartPythonScript(string scriptToRun)
    {
        string pythonExe = "python";
        string workingDir = Path.Combine(Application.dataPath, "Scripts");

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

            // 設置日誌輸出 (保持原樣)
            p.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    UnityEngine.Debug.Log($"[Python:{scriptToRun}] {e.Data}");
            };

            p.ErrorDataReceived += (sender, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;
                string line = e.Data;
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

                // 追蹤所有啟動的程序
                allServerProcesses.Add(p);
                UnityEngine.Debug.Log($"[SceneFlow] 已啟動常駐伺服器：{scriptToRun}（PID={p.Id}）");
            }
            else
            {
                UnityEngine.Debug.LogError($"[SceneFlow] 無法啟動常駐伺服器：{scriptToRun}");
            }
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"[SceneFlow] 啟動伺服器失敗：{scriptToRun}，錯誤：{ex.Message}");
        }
    }
    // ---

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
        yield return StartCoroutine(Fade(0f, 1f));

        // ⚠️ 移除 StopCurrentServer(); - 不再需要在場景切換時關閉伺服器

        AsyncOperation op = SceneManager.LoadSceneAsync(nextScene, LoadSceneMode.Single);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f) yield return null;
        op.allowSceneActivation = true;

        yield return null;
        yield return new WaitForSeconds(3f);

        // ⚠️ 移除 StartServerForScene(nextScene); - 不再需要在場景切換時啟動伺服器

        yield return StartCoroutine(Fade(1f, 0f));
    }

    // ⚠️ 移除 StartServerForScene 方法 (已替換為 StartPythonScript)

    // ⚠️ 移除所有關閉相關的方法：StopCurrentServer, GetShutdownUrlForCurrentServer, 
    // ShutdownAndFallback, TryKillCurrentProcess

    // --- 新增：應用程式退出時關閉所有伺服器的方法 ---
    private void StopAllPersistentServers()
    {
        foreach (var p in allServerProcesses)
        {
            try
            {
                if (p != null && !p.HasExited)
                {
                    p.Kill(); // 強制終止程序樹，這是最可靠的方式
                    UnityEngine.Debug.Log($"[SceneFlow] 應用程式退出時強制關閉程序 PID={p.Id}");
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[SceneFlow] 關閉程序 PID={p.Id} 失敗: {ex.Message}");
            }
        }
        allServerProcesses.Clear();
    }
    // ---

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

    private void OnApplicationQuit()
    {
        // 退出時，關閉所有常駐伺服器
        StopAllPersistentServers();
    }

    private void OnDestroy()
    {
        // 銷毀時，關閉所有常駐伺服器
        StopAllPersistentServers();
    }
}