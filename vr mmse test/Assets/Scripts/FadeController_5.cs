using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class FadeController : MonoBehaviour
{
    public Image blackScreen;
    public float fadeDuration = 1f;

    void Start()
    {
        // 遊戲開始先淡出（畫面亮起）
        StartCoroutine(FadeOut());
    }

    public IEnumerator FadeIn()
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, 1f, t / fadeDuration);
            SetAlpha(alpha);
            yield return null;
        }
    }

    public IEnumerator FadeOut()
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, t / fadeDuration);
            SetAlpha(alpha);
            yield return null;
        }
    }

    void SetAlpha(float alpha)
    {
        var color = blackScreen.color;
        color.a = alpha;
        blackScreen.color = color;
    }
}
