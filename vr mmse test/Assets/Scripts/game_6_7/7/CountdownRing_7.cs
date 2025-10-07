using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

public class CountdownBadge : MonoBehaviour
{
    [Header("UI")]
    public Image outerRing;        // Radial360（Image Type = Filled, Method = Radial360）
    public Image innerCircle;      // 實心圓（數字底）
    public TMP_Text timeLabel;     // 中央文字

    [Header("Timer")]
    public float duration = 90f;
    public bool autoStart = true;
    public bool showAsMMSS = true;
    public float warningThreshold = 10f;
    public bool warningPulse = true;

    [Header("Colors")]
    public Color ringNormal = new Color(0.2f, 0.8f, 1f);
    public Color ringWarning = new Color(1f, 0.35f, 0.25f);
    public Color circleBg = new Color(0.1f, 0.1f, 0.1f, 0.75f);
    public Color labelColor = Color.white;

    [Header("Drawing Lock")]
    [Tooltip("時間到或完成時關閉的元件（WhiteBoard/Brush 等）")]
    public List<Behaviour> componentsToDisable = new List<Behaviour>();
    [Tooltip("嘗試呼叫 SetDrawingEnabled(false) 的物件")]
    public List<GameObject> drawingTargets = new List<GameObject>();

    [Header("Events")]
    public UnityEvent onTimeUp;               // 若要分流做音效/特效可用
    public UnityEvent onFinishedByUser;       // ★ 手動完成或時間到，最後都會觸發（接跳關）
    public UnityEvent<float> onTick;          // 每幀回報剩餘秒數（秒）

    [Header("Behavior")]
    [Tooltip("時間到時自動等同按下「完成作畫」")]
    public bool autoFinishOnTimeUp = true;

    float _remain;
    bool _running;
    bool _finished;
    Vector3 _ringBaseScale = Vector3.one;

    void Awake()
    {
        if (outerRing) _ringBaseScale = outerRing.rectTransform.localScale;
        ApplyStaticUI();

    }

    void Start()
    {
        ResetTimer();
        if (autoStart) StartCountdown();
    }

    void ApplyStaticUI()
    {
        if (innerCircle) innerCircle.color = circleBg;
        if (timeLabel)   timeLabel.color = labelColor;
        if (outerRing)   outerRing.color = ringNormal;
    }

    public void ResetTimer()
    {
        _remain = Mathf.Max(0f, duration);
        _running = false;
        _finished = false;
        UpdateVisual(_remain);
    }

    public void StartCountdown()
    {
        if (duration <= 0f) duration = 1f;
        _remain = duration;
        _running = true;
        _finished = false;
        UpdateVisual(_remain);
    }

    public void Pause()  => _running = false;
    public void Resume() => _running = true;

    void Update()
    {
        if (!_running || _finished) return;

        _remain -= Time.deltaTime;
        if (_remain <= 0f)
        {
            _remain = 0f;
            _running = false;
            UpdateVisual(_remain);

            // 可獨立處理「時間到」效果
            onTimeUp?.Invoke();

            if (autoFinishOnTimeUp)
            {
                FinishInternal();  // 走與手動完成相同流程
            }
            return;
        }

        UpdateVisual(_remain);
        onTick?.Invoke(_remain);
    }

    void UpdateVisual(float remain)
    {
        // 外圈進度與警告效果
        if (outerRing)
        {
            float p = duration > 0f ? remain / duration : 0f;
            outerRing.fillAmount = Mathf.Clamp01(p);

            bool warn = remain <= warningThreshold;
            outerRing.color = warn ? ringWarning : ringNormal;

            if (warn && warningPulse)
            {
                float s = 1f + 0.03f * Mathf.Sin(Time.time * 8f);
                outerRing.rectTransform.localScale = _ringBaseScale * s;
            }
            else
            {
                outerRing.rectTransform.localScale = _ringBaseScale;
            }
        }

        // 文字（mm:ss 或整數秒）
        if (timeLabel)
        {
            if (showAsMMSS)
            {
                int sec = Mathf.CeilToInt(remain);
                int mm = sec / 60;
                int ss = sec % 60;
                timeLabel.text = $"{mm:00}:{ss:00}";
            }
            else
            {
                timeLabel.text = Mathf.CeilToInt(remain).ToString();
            }
        }
    }

    void LockDrawing()
    {
        foreach (var b in componentsToDisable)
            if (b) b.enabled = false;

        foreach (var go in drawingTargets)
            if (go) go.SendMessage("SetDrawingEnabled", false, SendMessageOptions.DontRequireReceiver);
    }

    // 給「完成作畫」按鈕
    public void FinishNow()
    {
        if (_finished) return;
        _running = false;
        _remain = 0f;
        UpdateVisual(_remain);
        FinishInternal();
    }

    // 時間到或手動完成都走這裡
    void FinishInternal()
    {
        if (_finished) return;
        _finished = true;
        LockDrawing();
        onFinishedByUser?.Invoke();  // ← 在 Inspector 綁你的跳關流程
    }
}
