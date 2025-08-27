using System.Collections.Generic;
using UnityEngine;

public class SessionController : MonoBehaviour
{
    [Header("Refs")]
    public LocationDB db;
    public QuizPanel quizPanel;
    public float quizDelay = 0.3f;

    LocationEntry _correct;
    List<LocationEntry> _opts = new();
    float _tStart;

    void Start()
    {
        if (RunLogger.I == null)
            new GameObject("RunLogger").AddComponent<RunLogger>();

        AskWhereAmINow(); // 進場也先出一次題（例如站在 Hub 可不出題則直接 return）
    }

    // 給 PlayerRigMover 在瞬移後再次呼叫
    public void AskWhereAmINow()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        var candidates = db.GetByScene(scene);
        if (candidates == null || candidates.Count == 0)
            return; // 例如 Hub 不出題就別加到 DB

        // 依你需求：若一個場景有多攤（F2），我們以最近一次「到達的 Viewpoint」決定正解；
        // 先簡化：隨機抽本層的一個攤位作為正解
        _correct = candidates[Random.Range(0, candidates.Count)];

        _opts.Clear();
        _opts.Add(_correct);
        _opts.AddRange(db.GetDistractors(_correct, 3));

        // 打散
        for (int i = 0; i < _opts.Count; i++)
        {
            int j = Random.Range(i, _opts.Count);
            (_opts[i], _opts[j]) = (_opts[j], _opts[i]);
        }

        Invoke(nameof(ShowQuiz), quizDelay);
    }

    void ShowQuiz()
    {
        if (_correct == null) return;
        string[] labels = _opts.ConvertAll(o => o.displayText).ToArray();
        _tStart = Time.time;
        RunLogger.I.Log("quiz_shown", _correct.floorLabel, _correct.stallLabel, "", false, 0);
        quizPanel.Show("你現在在哪裡？", labels, OnPick);
    }

    void OnPick(int idx)
    {
        long rt = (long)((Time.time - _tStart) * 1000f);
        bool ok = _opts[idx] == _correct;
        RunLogger.I.Log("quiz_answer", _correct.floorLabel, _correct.stallLabel, _opts[idx].displayText, ok, rt);
        quizPanel.gameObject.SetActive(false);
    }
}
