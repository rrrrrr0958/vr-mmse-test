using UnityEngine;

public class LevelIntroTTS : MonoBehaviour
{
    [Header("把每關的語音拖進來（依題目順序）")]
    public AudioClip[] questionVOs;
    public int questionIndex = 0; // 本關是哪一題

    [Header("選用")]
    public float delayBeforePlay = 0.2f; // 進場0.2秒後播，避免載入卡頓
    public bool spatialize = false;      // VR 若要定位聲音可開

    AudioSource _src;

    void Awake()
    {
        _src = gameObject.AddComponent<AudioSource>();
        _src.playOnAwake = false;
        _src.loop = false;
        _src.spatialize = spatialize;
        _src.spatialBlend = spatialize ? 1f : 0f; // 3D or 2D
    }

    void Start() => Invoke(nameof(PlayNow), delayBeforePlay);

    public void PlayNow()
    {
        if (questionVOs == null || questionVOs.Length == 0) return;
        if (questionIndex < 0 || questionIndex >= questionVOs.Length) return;
        _src.clip = questionVOs[questionIndex];
        _src.Play();
    }

    // 可給 UI 的「重播」按鈕用
    public void Replay() { if (_src.clip) _src.Play(); }
    // 可給 UI 的「靜音」按鈕用
    public void ToggleMute() { _src.mute = !_src.mute; }
}
