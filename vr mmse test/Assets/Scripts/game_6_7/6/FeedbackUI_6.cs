using UnityEngine;
using TMPro;
using System.Collections;

public class FeedbackUI_6 : MonoBehaviour
{
    [Header("Refs")]
    public TextMeshProUGUI text;   // 指到你的 TextMeshPro - Text (UI)

    [Header("Style")]
    public Color correctColor = new(0.2f, 0.8f, 0.3f);
    public Color wrongColor   = new(0.9f, 0.2f, 0.2f);
    [Range(0,5f)] public float hold = 1.0f;  // 顯示多久

    Coroutine playing;

    void Awake()
    {
        if (text) text.gameObject.SetActive(false);
    }

    public void ShowCorrect(string msg = "答對了！") => Play(msg, correctColor);
    public void ShowWrong(string msg = "再試一次…") => Play(msg, wrongColor);

    void Play(string msg, Color color)
    {
        if (!text) return;

        text.text = msg;
        text.color = color;

        if (playing != null) StopCoroutine(playing);
        playing = StartCoroutine(RunOnce());
    }

    IEnumerator RunOnce()
    {
        text.gameObject.SetActive(true);
        yield return new WaitForSeconds(hold);
        text.gameObject.SetActive(false);
    }
}
