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

    // 宣告所有需要持續運行的伺服器所使用的連接埠
    private readonly List<int> PersistentPorts = new List<int> { 5002, 5000, 5003 }; 

    private readonly List<string> sceneOrder = new List<string>
    {
        "Opening",
        // "Login Scene",
        // "SampleScene_rule",
        "GameIntroScene",
        "SampleScene_7",
        "Reward_Scene",
        "GameIntroScene",
        "SampleScene_14",
        "Reward_Scene",
        "GameIntroScene", 
        "SentenceGame_13",
        "Reward_Scene",
        "GameIntroScene",
        "SampleScene_3",
        "Reward_Scene",
        "GameIntroScene",
        "SampleScene_2",
        "Reward_Scene",
        "GameIntroScene",
        "SampleScene_5",
        "Reward_Scene",
        "GameIntroScene",
        "SampleScene_11_1",
        "SampleScene_11",
        "Reward_Scene",
        "GameIntroScene",
        "f1_8",
        "Reward_Scene",
        "GameIntroScene",
        "SampleScene_11",
        "Reward_Scene",
        "GameIntroScene",
        "SampleScene_6",
        "Reward_Scene",
        "Final_Scroe"         
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
        // 注意：這裡使用的埠號必須與上面的 PersistentPorts 列表一致
        yield return StartCoroutine(StartPythonIfFree("draw.py", 5002));
        yield return new WaitForSeconds(3f);
        yield return StartCoroutine(StartPythonIfFree("audio_5.py", 5000));
        yield return new WaitForSeconds(2f);
        yield return StartCoroutine(StartPythonIfFree("app_13.py", 5003));
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
    
    public void LoadNextScene()
    {
        currentIndex++;
        if (currentIndex >= sceneOrder.Count) currentIndex = 0;

        // 這邊有改
        // string nextScene = sceneOrder[currentIndex];

        // // ★ 關鍵：如果要進 Intro，就先把「下一個實際關卡」寫進 PlayerPrefs
        // if (nextScene == "GameIntroScene")
        // {
        //     int lookahead = Mathf.Min(currentIndex + 1, sceneOrder.Count - 1);
        //     PlayerPrefs.SetString("NextTargetScene", sceneOrder[lookahead]);
        //     // 可選：PlayerPrefs.Save();
        // }

            // ★ 若要進 Opening，就重設計數（並清除 NextTargetScene）
        // if (nextScene == "Opening")
        // {
        //     PlayerPrefs.SetInt("IntroStageCount", 0);
        //     PlayerPrefs.DeleteKey("NextTargetScene"); // 可選
        //     // PlayerPrefs.Save(); // 可選
        // }

        
        StartCoroutine(LoadSceneRoutine(sceneOrder[currentIndex]));
    }

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

    /// <summary>
    /// 【核心清理機制】使用 CMD 暴力查找並終止佔用指定 Port 的程序。
    /// </summary>
    private void KillProcessByPort(int port)
    {
        // 使用單一 CMD 指令來執行 netstat 查找 PID，並使用 taskkill 終止該 PID
        // 語法: FOR /F "tokens=5" %i IN ('netstat -ano ^| findstr :<port>') DO @taskkill /PID %i /F
        string cmdArguments = $"/C FOR /F \"tokens=5\" %i IN ('netstat -ano ^| findstr LISTEN ^| findstr :{port}') DO @taskkill /PID %i /F";

        try
        {
            var psi = new ProcessStartInfo("cmd.exe", cmdArguments)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            };

            Process p = new Process { StartInfo = psi };
            p.Start();
            p.WaitForExit(5000); // 最多等待 5 秒，確保指令執行
            
            // 讀取輸出，以便在 Unity Console 中查看結果 (選用)
            string output = p.StandardOutput.ReadToEnd();

            if (output.Contains("SUCCESS"))
            {
                UnityEngine.Debug.Log($"[Port Cleanup] 成功終止佔用 Port {port} 的程序。");
            }
            else if (output.Contains("no task"))
            {
                UnityEngine.Debug.Log($"[Port Cleanup] Port {port} 未被佔用或程序已終止。");
            }
            else
            {
                // 即使失敗，也可能是該 Port 未被佔用
                UnityEngine.Debug.LogWarning($"[Port Cleanup] Port {port} 清理指令執行完畢，結果: {output.Trim()}");
            }
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"[Port Cleanup] 無法執行 Port {port} 清理指令: {ex.Message}");
        }
    }

    private void StopAllPersistentServers()
    {
        UnityEngine.Debug.Log("[SceneFlow] 嘗試關閉所有伺服器...");
        
        // 1. (遺留步驟) 先嘗試用 Unity 記錄的 PID 終止（可能失敗，但仍應嘗試）
        foreach (var p in allServerProcesses)
        {
            try
            {
                if (p != null && !p.HasExited)
                {
                    // 終止主程序
                    p.Kill(); 
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[SceneFlow] 無法透過 PID 終止程序 (PID={p?.Id}): {ex.Message}");
            }
        }
        allServerProcesses.Clear();

        // 2. 【強制清理】對所有持續監聽的埠號執行 Port 級別終止
        foreach (int port in PersistentPorts)
        {
            KillProcessByPort(port);
        }

        UnityEngine.Debug.Log("[SceneFlow] 所有伺服器清理完成。");
    }

    private void OnApplicationQuit() => StopAllPersistentServers();
    private void OnDestroy() => StopAllPersistentServers();
}
