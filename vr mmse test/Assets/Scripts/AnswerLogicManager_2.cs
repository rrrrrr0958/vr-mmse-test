using UnityEngine;
using TMPro;
using System.Linq;

public class AnswerLogicManager : MonoBehaviour
{
    [Header("UI 顯示")]
    public TextMeshProUGUI statusText;

    private readonly string[] correctAnswers = { 
        "海鮮折扣快來買",      
        "雞豬牛羊都有賣",      
        "早起買菜精神好"       
    };

    private const float SimilarityThreshold = 0.50f;

    public void CheckAnswer(string userResponse, int questionIndex)
    {
        string userDisplay = string.IsNullOrEmpty(userResponse) ? "(無語音輸入)" : userResponse;

        string similarityPercent = "N/A";
        bool isCorrect = false;

        if (questionIndex >= 0 && questionIndex < correctAnswers.Length)
        {
            string correctAnswer = correctAnswers[questionIndex];

            // 清理輸入
            string cleanUserResponse = new string(userResponse.Where(c => !char.IsPunctuation(c) && !char.IsWhiteSpace(c)).ToArray()).ToLower();
            string cleanCorrectAnswer = new string(correctAnswer.Where(c => !char.IsPunctuation(c) && !char.IsWhiteSpace(c)).ToArray()).ToLower();

            if (string.IsNullOrEmpty(cleanUserResponse))
            {
                // 不再 return，改為視為答錯，但允許流程繼續
                // if (statusText != null)
                //     statusText.text = "⚠️ 辨識結果為空，已自動視為未回答。";
                Debug.Log($"[Answer] 您的回答: {userDisplay}. 判定：結果為空，視為未回答。");
            }
            else
            {
                // 計算相似度
                int distance = GetLevenshteinDistance(cleanUserResponse, cleanCorrectAnswer);
                int maxLength = Mathf.Max(cleanUserResponse.Length, cleanCorrectAnswer.Length);
                if (maxLength == 0) maxLength = 1;

                float similarity = 1.0f - ((float)distance / maxLength);
                isCorrect = similarity >= SimilarityThreshold;
                similarityPercent = similarity.ToString("P0"); // 例如 "67%"

                // --- 畫面輸出 (加上相似度) ---
                // if (statusText != null)
                // {
                //     if (isCorrect)
                //         statusText.text = $"✅ 答對了！您的回答: {userDisplay}\n相似度: {similarityPercent}";
                //     else
                //         statusText.text = $"❌ 答錯了。\n正確答案:「{correctAnswer}」\n相似度: {similarityPercent}";
                // }

                string consoleResult = isCorrect ? "✅ 答對" : "❌ 答錯";
                Debug.Log($"\n--- 答題結果 ---");
                Debug.Log($"題目索引: {questionIndex}");
                Debug.Log($"正確答案: {correctAnswer}");
                Debug.Log($"您的回答: {userDisplay}");
                Debug.Log($"相似度: {similarityPercent} ({consoleResult}，門檻為 {SimilarityThreshold:P0})");
                Debug.Log($"----------------\n");
            }
        }
        else
        {
            // if (statusText != null) statusText.text = "流程錯誤，無效的問題索引。";
            Debug.LogError($"[AnswerLogicManager] 無效的問題索引：{questionIndex}");
        }
    }


    public int GetLevenshteinDistance(string s, string t)
    {
        if (string.IsNullOrEmpty(s)) return string.IsNullOrEmpty(t) ? 0 : t.Length;
        if (string.IsNullOrEmpty(t)) return s.Length;

        int n = s.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1];

        for (int i = 0; i <= n; d[i, 0] = i++) ;
        for (int j = 1; j <= m; d[0, j] = j++) ;

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Mathf.Min(Mathf.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }
        return d[n, m];
    }
}
