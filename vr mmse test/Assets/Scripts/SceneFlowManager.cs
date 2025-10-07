using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;

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

    private readonly List<Process> allServerProcesses = new List<Process>();

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            StartCoroutine(StartPersistentServers());
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private IEnumerator StartPersistentServers()
    {
        yield return StartCoroutine(StartPythonIfFree("audio_5.py", 5000));
        yield return new WaitForSeconds(2f);
        yield return StartCoroutine(StartPythonIfFree("server_track.py", 5001));
    }

    private IEnumerator StartPythonIfFree(string script, int port)
    {
        if (!IsPortAvailable(port))
        {
            UnityEngine.Debug.LogWarning($"[SceneFlow] Port {port} 已被佔用，跳過啟動 {script}");
            yield break;
        }

        StartPythonScript(script);
        yield return null;
    }

    private bool IsPortAvailable(int port)
    {
        try
        {
            TcpListener listener = new TcpListener(System.Net.IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

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
            p.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    UnityEngine.Debug.Log($"[Python:{scriptToRun}] {e.Data}");
            };
            p.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    UnityEngine.Debug.LogWarning($"[PythonError:{scriptToRun}] {e.Data}");
            };

            bool started = p.Start();
            if (started)
            {
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                allServerProcesses.Add(p);
                UnityEngine.Debug.Log($"[SceneFlow] 啟動伺服器 {scriptToRun} (PID={p.Id})");
            }
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"[SceneFlow] 無法啟動 {scriptToRun}: {ex.Message}");
        }
    }
    //原本的loadnext
    public void LoadNextScene()
    {
        currentIndex++;
        if (currentIndex >= sceneOrder.Count) currentIndex = 0;
        StartCoroutine(LoadSceneRoutine(sceneOrder[currentIndex]));
    }

    //可以設定從某場景到下一個場景時要暫停
    // public void LoadNextScene()
    // {
    //     currentIndex++;
    //     if (currentIndex >= sceneOrder.Count) currentIndex = 0;

    //     // 🔹 在從 SampleScene_11 → SampleScene_2 時暫停 15 秒
    //     if (sceneOrder[currentIndex - 1] == "SampleScene_11" && sceneOrder[currentIndex] == "SampleScene_2")
    //     {
    //         StartCoroutine(PauseBeforeNextScene(15f, sceneOrder[currentIndex]));
    //         return;
    //     }

    //     StartCoroutine(LoadSceneRoutine(sceneOrder[currentIndex]));
    // }
    //和上方要一同存在或刪掉(寫如何暫停的)
    private IEnumerator PauseBeforeNextScene(float seconds, string nextScene)
    {
        UnityEngine.Debug.Log($"[SceneFlow] 即將切換至 {nextScene}，暫停 {seconds} 秒...");
        yield return new WaitForSeconds(seconds);
        yield return StartCoroutine(LoadSceneRoutine(nextScene));
    }

    private IEnumerator LoadSceneRoutine(string nextScene)
    {
        yield return StartCoroutine(Fade(0f, 1f));
        AsyncOperation op = SceneManager.LoadSceneAsync(nextScene, LoadSceneMode.Single);
        op.allowSceneActivation = false;
        while (op.progress < 0.9f) yield return null;
        op.allowSceneActivation = true;
        yield return new WaitForSeconds(3f);
        yield return StartCoroutine(Fade(1f, 0f));
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

    private void KillProcessTree(Process p)
    {
        try
        {
            if (p == null || p.HasExited) return;
            int pid = p.Id;
            ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", $"/c taskkill /PID {pid} /T /F");
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            Process.Start(psi);
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogWarning($"[SceneFlow] 無法關閉程序 PID={p?.Id}: {ex.Message}");
        }
    }

    private void StopAllPersistentServers()
    {
        foreach (var p in allServerProcesses)
        {
            KillProcessTree(p);
        }
        allServerProcesses.Clear();
        UnityEngine.Debug.Log("[SceneFlow] 已關閉所有伺服器");
    }

    private void OnApplicationQuit() => StopAllPersistentServers();
    private void OnDestroy() => StopAllPersistentServers();
}
