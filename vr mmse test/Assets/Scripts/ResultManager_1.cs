using UnityEngine;
using System.Collections.Generic;

public class ResultManager : MonoBehaviour
{
    public static ResultManager instance;

    void Awake()
    {
        if (instance == null) instance = this;
    }

    public void ShowResult(List<string> clicked, List<string> correct, float startTime, float endTime)
    {
        float timeUsed = endTime - startTime;

        int correctCount = 0;
        for (int i = 0; i < correct.Count; i++)
        {
            if (i < clicked.Count && clicked[i] == correct[i])
            {
                correctCount++;
            }
        }
        float accuracy = (float)correctCount / correct.Count * 100f;

        Debug.Log($"✅ 答對數量: {correctCount}/{correct.Count}, 準確率: {accuracy:F2}%, 用時: {timeUsed:F2}秒");

        // 🔥 每次結束自動產生 JSON
        GameManager.instance.ConvertGameDataToJson("Player001", accuracy, timeUsed);
    }
}
