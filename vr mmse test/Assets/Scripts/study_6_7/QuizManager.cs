using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class QuizManager : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI questionText;

    [Header("完成本關後要做的事")]
    public UnityEvent onQuestionCleared; // 例如切到下一個遊戲、切場景、顯示結算等等

    // 題庫（題目文字 與 對應正解 targetId 必須一一對齊）
    readonly List<(string prompt, string id)> pool = new()
    {
        ("請用控制器點選右邊櫃子上的相機Camera",       "camera"),
        ("請用控制器點選右邊櫃子上最大的盆栽Biggest Plant", "bigPlant"),
        ("請用控制器點選右邊櫃子上的檯燈Lamp",     "lamp"),
        ("請用控制器點選右邊櫃子上的黃色小球Yellow Ball", "yellowBall"),
        ("請用控制器點選右邊櫃子上的白色寶特瓶White Bottle","whiteBottle"),
    };

    int picked = -1;            // 這次抽到第幾題
    string currentAnswer = "";  // 這題正解的 targetId

    void Start()
    {
        PickOneQuestion();
    }

    void PickOneQuestion()
    {
        // 避免和上一次遊玩抽到同一題（可選）
        int last = PlayerPrefs.GetInt("last_quiz_idx", -1);

        if (pool.Count == 0) { questionText.text = "（題庫是空的）"; return; }

        // 隨機抽
        int tries = 10;
        do { picked = Random.Range(0, pool.Count); }
        while (picked == last && pool.Count > 1 && --tries > 0);

        PlayerPrefs.SetInt("last_quiz_idx", picked);

        // 設定題目與答案
        var q = pool[picked];
        currentAnswer = q.id;
        questionText.text = q.prompt;

        // （可選）若場景缺少該 targetId，改抽另一題
        if (!TargetExistsInScene(currentAnswer))
        {
            // 如果抽到場景不存在的目標，嘗試換題一次
            for (int i = 0; i < pool.Count; i++)
            {
                int alt = (picked + 1 + i) % pool.Count;
                if (TargetExistsInScene(pool[alt].id))
                {
                    picked = alt;
                    currentAnswer = pool[alt].id;
                    questionText.text = pool[alt].prompt;
                    PlayerPrefs.SetInt("last_quiz_idx", picked);
                    break;
                }
            }
        }
    }

    bool TargetExistsInScene(string id)
    {
        var all = FindObjectsOfType<SelectableTarget>(true);
        foreach (var t in all) if (t.targetId == id) return true;
        return false;
    }

    // 給互動物件呼叫
    public void Submit(string targetId)
    {
        if (string.IsNullOrEmpty(currentAnswer)) return;

        if (targetId == currentAnswer)
        {
            // 正解：觸發完成事件（外部接「跳下一關」）
            onQuestionCleared?.Invoke();
        }
        else
        {
            StopAllCoroutines();
            StartCoroutine(BlinkWrong());
        }
    }

    System.Collections.IEnumerator BlinkWrong()
    {
        var old = questionText.text;
        questionText.text = "再找找看～";
        yield return new WaitForSeconds(0.6f);
        questionText.text = old;
    }
}

