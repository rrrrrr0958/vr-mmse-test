using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 抵達（瞬移/換場）後自動出題；接 QuizPanel；寫 RunLogger。
/// 相容你的 QuizPanel(Action<int>) 寫法。
/// </summary>
public class SessionController : MonoBehaviour {
    [Header("Data")]
    public LocationDB db;

    [Header("UI")]
    public QuizPanel quizPanel;

    [Header("Logging")]
    public RunLogger logger;

    [Header("Options")]
    [Range(2,6)] public int optionsPerQuestion = 4;

    PlayerRigMover _mover;
    System.Random _rng;
    List<LocationEntry> _currentOptions = new();
    int _correctIndex = -1;

    void Awake() {
        _rng = new System.Random();
        _mover = UnityEngine.Object.FindFirstObjectByType<PlayerRigMover>();
        if (_mover) _mover.OnTeleported.AddListener(OnArrived);
        SceneManager.sceneLoaded += OnSceneLoaded;
        logger?.StartRun();
    }

    void OnDestroy(){
        if (_mover) _mover.OnTeleported.RemoveListener(OnArrived);
        SceneManager.sceneLoaded -= OnSceneLoaded;
        logger?.EndRun();
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m) => OnArrived();

    void OnArrived() {
        var sceneName = SceneManager.GetActiveScene().name;
        var vpName = GuessVPNameByNearest(sceneName); // 近點偵測；找不到時回退到第一筆
        AskQuestion(sceneName, vpName);
    }

    // 依據玩家位置，找當前場景中「名稱符合 DB 的 Viewpoint」裡最近的一個
    string GuessVPNameByNearest(string sceneName) {
        var candidates = db.AllByScene(sceneName).ToList();
        if (candidates.Count == 0) return "";

        Transform rig = _mover ? _mover.transform : null;
        if (rig == null) return candidates[0].viewpointName;

        float best = float.MaxValue;
        string bestName = candidates[0].viewpointName;

        foreach (var e in candidates) {
            var go = GameObject.Find(e.viewpointName);
            if (!go) continue;
            float d = Vector3.SqrMagnitude(go.transform.position - rig.position);
            if (d < best) { best = d; bestName = e.viewpointName; }
        }
        return bestName;
    }

    void AskQuestion(string sceneName, string vpName) {
        var correct = db.FindBySceneAndVP(sceneName, vpName);
        if (correct == null) {
            Debug.LogWarning($"[Session] 找不到題目：scene={sceneName}, vp={vpName}");
            return;
        }

        _currentOptions = BuildOptions(correct, optionsPerQuestion);
        _correctIndex   = _currentOptions.IndexOf(correct);

        var labels = _currentOptions.Select(e => e.displayText).ToArray();

        quizPanel.Show("你現在在哪裡？", labels, OnPick);

        logger?.SetDisplayTextCache(correct.displayText);
        logger?.BeginQuestion(sceneName, correct.viewpointName, correct.displayText);
    }

    List<LocationEntry> BuildOptions(LocationEntry correct, int count) {
        var same   = db.entries.Where(e => e.sceneName == correct.sceneName && e != correct).OrderBy(_ => _rng.Next());
        var others = db.entries.Where(e => e.sceneName != correct.sceneName).OrderBy(_ => _rng.Next());

        var list = new List<LocationEntry>{ correct };
        foreach (var e in same.Concat(others)) {
            if (list.Count >= count) break;
            if (!list.Contains(e)) list.Add(e);
        }
        return list.OrderBy(_ => _rng.Next()).ToList();
    }

    void OnPick(int idx) {
        var sceneName = SceneManager.GetActiveScene().name;
        bool isCorrect = (idx == _correctIndex);
        string userKey = (idx >= 0 && idx < _currentOptions.Count) ? _currentOptions[idx].viewpointName : "";

        logger?.EndQuestion(userKey, isCorrect, 0);

        // （可選）顯示回饋後延遲關閉
        // quizPanel.ShowFeedback(isCorrect);
        // StartCoroutine(CloseLater(1f));
    }
}
