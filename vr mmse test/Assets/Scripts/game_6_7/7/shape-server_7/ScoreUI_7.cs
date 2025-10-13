using UnityEngine;
using UnityEngine.UI;   // uGUI Text
using TMPro;            // TextMeshPro

public class ScoreUI_7 : MonoBehaviour
{
    [Header("Text Targets (擇一或兩者都接)")]
    public Text scoreText;        // uGUI 的 Text
    public TMP_Text scoreTMP;     // TextMeshProUGUI

    [Header("Display (數值模式用，不影響二值輸出)")]
    [Tooltip("顯示格式（{0}=分數）")]
    public string format = "總分：{0:0.0}";
    [Range(0.05f, 3f)] public float lerpTime = 0.4f; // 平滑時間

    [Header("Binary Output")]
    [Tooltip("勾選後：分數 >= passThreshold 顯示 passText，否則顯示 failText")]
    public bool showBinary = true;
    [Tooltip("通過門檻（含等於）")]
    public float passThreshold = 60f;
    public string passText = "1";
    public string failText = "0";

    [Header("Binary Colors")]
    [Tooltip("僅文字瞬間跳變，顏色可選擇是否做漸變")]
    public bool usePassFailColors = true;
    public Color passColor = new Color(0.2f, 0.8f, 0.3f);
    public Color failColor = new Color(0.85f, 0.2f, 0.2f);
    [Tooltip("二值模式下是否讓顏色做漸變（文字仍瞬間切換）")]
    public bool animateColorInBinary = false;

    [Header("Optional Color Gradient (僅在非二值輸出時生效)")]
    public bool useColorGradient = false; // 二值輸出預設關閉梯度
    public Color lowColor  = new Color(0.85f, 0.2f, 0.2f); // 接近 0 分
    public Color highColor = new Color(0.2f, 0.8f, 0.3f);  // 接近 100 分

    float current = 0f;
    Coroutine animCo;

    /// <summary>外部呼叫：更新總分（0~100）</summary>
    public void UpdateScore(float score)
    {
        score = Mathf.Clamp(score, 0f, 100f);

        if (showBinary)
        {
            // 二值模式：直接用目標分數判斷，不跑數字動畫
            bool pass = score >= passThreshold;
            current = score;
            RenderBinary(pass); // 文字立即顯示 1/0
            // 如需顏色漸變，可僅對顏色做動畫
            if (animateColorInBinary)
            {
                if (animCo != null) StopCoroutine(animCo);
                animCo = StartCoroutine(AnimateBinaryColor(pass));
            }
            return;
        }

        // 非二值：維持原本數值動畫
        if (animCo != null) StopCoroutine(animCo);
        animCo = StartCoroutine(Animate(score));
    }
    
    public void ClearDisplay()
    {
        current = 0f;
        if (scoreTMP != null) scoreTMP.text = "";
        if (scoreText != null) scoreText.text = "";
        // 重置顏色
        if (usePassFailColors)
        {
            if (scoreTMP != null) scoreTMP.color = failColor;
            if (scoreText != null) scoreText.color = failColor;
        }
        else if (useColorGradient)
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
            RenderNumeric(current);
            yield return null;
        }
        current = target;
        RenderNumeric(current);
    }

    System.Collections.IEnumerator AnimateBinaryColor(bool pass)
    {
        // 只在二值模式下對顏色做平滑（文字已立即切換）
        Color c0 = GetCurrentColor();
        Color c1 = pass ? passColor : failColor;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, lerpTime);
            Color c = Color.Lerp(c0, c1, Mathf.SmoothStep(0f, 1f, t));
            SetColor(c);
            yield return null;
        }
        SetColor(c1);
    }

    void RenderBinary(bool pass)
    {
        string s = pass ? passText : failText;
        if (scoreTMP != null) scoreTMP.text = s;
        if (scoreText != null) scoreText.text = s;

        if (usePassFailColors && !animateColorInBinary)
        {
            // 不做顏色動畫時，顏色也立即切換
            SetColor(pass ? passColor : failColor);
        }
    }

    void RenderNumeric(float v)
    {
        string s = string.Format(format, v);
        if (scoreTMP != null) scoreTMP.text = s;
        if (scoreText != null) scoreText.text = s;

        if (useColorGradient)
        {
            Color c = Color.Lerp(lowColor, highColor, v / 100f);
            SetColor(c);
        }
    }

    Color GetCurrentColor()
    {
        if (scoreTMP != null) return scoreTMP.color;
        if (scoreText != null) return scoreText.color;
        return Color.white;
    }

    void SetColor(Color c)
    {
        if (scoreTMP != null) scoreTMP.color = c;
        if (scoreText != null) scoreText.color = c;
    }
}
