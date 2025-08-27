using System.IO;
using UnityEngine;

public class RunLogger : MonoBehaviour
{
    public static RunLogger I;
    StreamWriter _w;

    void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        Object.DontDestroyOnLoad(gameObject);

        var dir = System.IO.Path.Combine(Application.persistentDataPath, "logs");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"session_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");

        _w = new StreamWriter(path, true);
        _w.WriteLine("ts,event,scene,floor,stall,choice,correct,rt_ms,extra");
        _w.Flush();
    }

    void OnDestroy() { _w?.Flush(); _w?.Dispose(); }

    public void Log(string evt, string floor, string stall, string choice, bool correct, long rtMs, string extra="")
    {
        string ts = System.DateTime.Now.ToString("o");
        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        _w.WriteLine($"{ts},{evt},{scene},{Q(floor)},{Q(stall)},{Q(choice)},{correct},{rtMs},{Q(extra)}");
        _w.Flush();
    }

    string Q(string s) => "\"" + (s ?? "").Replace("\"","\"\"") + "\"";
}
