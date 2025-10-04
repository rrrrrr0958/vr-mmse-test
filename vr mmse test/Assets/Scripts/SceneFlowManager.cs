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
        yield return StartCoroutine(Fade(0f, 1f));

        StopCurrentServer();

        AsyncOperation op = SceneManager.LoadSceneAsync(nextScene, LoadSceneMode.Single);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f) yield return null;
        op.allowSceneActivation = true;

        yield return null;
        yield return new WaitForSeconds(3f);

        StartServerForScene(nextScene);

        yield return StartCoroutine(Fade(1f, 0f));
    }

    public void StartServerForScene(string sceneName)
    {
        string pythonExe = "python";
        string workingDir = Path.Combine(Application.dataPath, "Scripts");

        string scriptToRun = "";

        switch (sceneName)
        {
            case "SampleScene_5":
            case "SampleScene_3":
            case "SampleScene_2":
                scriptToRun = "audio_5.py";
                break;
            case "SampleScene_11":
                scriptToRun = "server_track.py";
                break;
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

    // ========================= 新增關伺服器方法 =========================
    private void StopCurrentServer()
    {
        if (currentServerProcess == null) return;

        string shutdownUrl = GetShutdownUrlForCurrentServer();
        if (!string.IsNullOrEmpty(shutdownUrl))
            StartCoroutine(ShutdownAndFallback(shutdownUrl));
        else
            TryKillCurrentProcess();
    }

    private string GetShutdownUrlForCurrentServer()
    {
        if (currentServerKey == "server_track.py") return "http://127.0.0.1:5001/shutdown";
        if (currentServerKey == "audio_5.py") return "http://127.0.0.1:5000/shutdown";
        return null;
    }

    private IEnumerator ShutdownAndFallback(string url)
    {
        bool completed = false;
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes("");

        using (var req = new UnityEngine.Networking.UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            float timer = 0f;
            float timeout = 5f;

            var operation = req.SendWebRequest();
            while (!operation.isDone && timer < timeout)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.Log("[SceneFlow] Shutdown request success.");
                completed = true;
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[SceneFlow] Shutdown request failed: {req.error}");
            }
        }

        if (!completed)
            TryKillCurrentProcess();
    }

    private void TryKillCurrentProcess()
    {
        try
        {
            if (currentServerProcess != null && !currentServerProcess.HasExited)
            {
                currentServerProcess.Kill();
                UnityEngine.Debug.Log("[SceneFlow] Fallback: process killed.");
            }
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogWarning($"[SceneFlow] Kill process failed: {ex.Message}");
        }
        finally
        {
            currentServerProcess = null;
            currentServerKey = null;
        }
    }
    // ===============================================================

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
        StopCurrentServer();
    }

    private void OnDestroy()
    {
        StopCurrentServer();
    }
}
