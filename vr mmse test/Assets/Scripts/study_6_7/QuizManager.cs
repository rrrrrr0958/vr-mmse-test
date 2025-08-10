using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class QuizManager : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI questionText;

    [Header("完成本關後要做的事")]
    public UnityEvent onQuestionCleared;

    readonly List<(string prompt, string id)> pool = new()
    {
        ("請用控制器點選右邊櫃子上的相機Camera",       "camera"),
        ("請用控制器點選右邊櫃子上最大的盆栽Biggest Plant", "bigPlant"),
        ("請用控制器點選右邊櫃子上的檯燈Lamp",           "lamp"),
        ("請用控制器點選右邊櫃子上的黃色小球Yellow Ball",    "yellowBall"),
        ("請用控制器點選右邊櫃子上的白色寶特瓶White Bottle", "whiteBottle"),
    };

    int picked = -1;
    string currentAnswer = "";

    // 候選資料（最後一次選中的選項）
    string candidateId;
    string candidateLabel;

    int total, correct;

    void Start() => PickOneQuestion();

    void PickOneQuestion()
    {
        int last = PlayerPrefs.GetInt("last_quiz_idx", -1);
        if (pool.Count == 0) { if (questionText) questionText.text = "（題庫是空的）"; return; }

        int tries = 10;
        do { picked = Random.Range(0, pool.Count); }
        while (picked == last && pool.Count > 1 && --tries > 0);

        PlayerPrefs.SetInt("last_quiz_idx", picked);

        var q = pool[picked];
        currentAnswer = q.id;
        if (questionText) questionText.text = q.prompt;

        if (!TargetExistsInScene(currentAnswer))
        {
            for (int i = 0; i < pool.Count; i++)
            {
                int alt = (picked + 1 + i) % pool.Count;
                if (TargetExistsInScene(pool[alt].id))
                {
                    picked = alt;
                    currentAnswer = pool[alt].id;
                    if (questionText) questionText.text = pool[alt].prompt;
                    PlayerPrefs.SetInt("last_quiz_idx", picked);
                    break;
                }
            }
        }

        candidateId = candidateLabel = null;
    }

    bool TargetExistsInScene(string id)
    {
#if UNITY_2022_2_OR_NEWER
        var all = FindObjectsByType<SelectableTarget>(FindObjectsSortMode.None);
#else
        var all = FindObjectsOfType<SelectableTarget>(true);
#endif
        foreach (var t in all) if (t.targetId == id) return true;
        return false;
    }

    // 物件被選中時（即時候選）
    public void SubmitCandidate(string selectedId, string labelForLog = "")
    {
        candidateId    = selectedId;
        candidateLabel = string.IsNullOrEmpty(labelForLog) ? selectedId : labelForLog;

        bool ok = candidateId == currentAnswer;
        Debug.Log($"[候選] 目標物:{currentAnswer}  選擇:{candidateId} ({candidateLabel})  {(ok ? "✅ 正確" : "❌ 錯誤")}");
    }

    // UI 按鈕呼叫：最終判分
    public void ConfirmAnswer()
    {
        if (string.IsNullOrEmpty(candidateId))
        {
            Debug.Log("[最終] 尚未選擇任何物件。");
            return;
        }

        bool ok = candidateId == currentAnswer;
        total++; if (ok) correct++;

        Debug.Log($"[最終] 目標物:{currentAnswer}  選擇:{candidateId} ({candidateLabel})  {(ok ? "✅ 正確" : "❌ 錯誤")} ｜累積：{correct}/{total}");

        if (ok) onQuestionCleared?.Invoke();
        else StartCoroutine(BlinkWrong());
    }

    System.Collections.IEnumerator BlinkWrong()
    {
        if (!questionText) yield break;
        var old = questionText.text;
        questionText.text = "再找找看～";
        yield return new WaitForSeconds(0.6f);
        questionText.text = old;
    }

    // 若要外部手動切題可用
    public void SetQuestion(string prompt, string id)
    {
        if (questionText) questionText.text = prompt;
        currentAnswer = id;
        candidateId = candidateLabel = null;
    }
}
