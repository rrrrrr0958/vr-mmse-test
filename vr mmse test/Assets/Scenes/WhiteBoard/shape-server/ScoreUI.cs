using UnityEngine;
using UnityEngine.UI;   // uGUI Text
using TMPro;            // TextMeshPro

public class ScoreUI : MonoBehaviour
{
    [Header("Text Targets (擇一或兩者都接)")]
    public Text scoreText;        // uGUI 的 Text
    public TMP_Text scoreTMP;     // TextMeshProUGUI

    [Header("Display")]
    [Tooltip("顯示格式（{0}=分數）")]
    public string format = "總分：{0:0.0}";
    [Range(0.05f, 3f)] public float lerpTime = 0.4f; // 平滑時間

    [Header("Optional Color Gradient")]
    public bool useColorGradient = true;
    public Color lowColor  = new Color(0.85f, 0.2f, 0.2f); // 接近 0 分
    public Color highColor = new Color(0.2f, 0.8f, 0.3f);  // 接近 100 分

    float current = 0f;
    Coroutine animCo;

    /// <summary>外部呼叫：更新總分（0~100）</summary>
    public void UpdateScore(float score)
    {
        score = Mathf.Clamp(score, 0f, 100f);
        if (animCo != null) StopCoroutine(animCo);
        animCo = StartCoroutine(Animate(score));
    }
    
    public void ClearDisplay()
    {
        current = 0f;
        // 你想顯示空白或預設字樣都可，這裡示範顯示空白
        if (scoreTMP != null) scoreTMP.text = "";
        if (scoreText != null) scoreText.text = "";
        // 可選：把顏色也重置
        if (useColorGradient)
        {
            if (scoreTMP != null) scoreTMP.color = lowColor;
            if (scoreText != null) scoreText.color = lowColor;
        }
    }


    System.Collections.IEnumerator Animate(float target)
    {
        float start = current;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, lerpTime);
            current = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t));
            Render(current);
            yield return null;
        }
        current = target;
        Render(current);
    }

    void Render(float v)
    {
        string s = string.Format(format, v);
        if (scoreTMP != null) scoreTMP.text = s;
        if (scoreText != null) scoreText.text = s;

        if (useColorGradient)
        {
            Color c = Color.Lerp(lowColor, highColor, v / 100f);
            if (scoreTMP != null) scoreTMP.color = c;
            if (scoreText != null) scoreText.color = c;
        }
    }
}
