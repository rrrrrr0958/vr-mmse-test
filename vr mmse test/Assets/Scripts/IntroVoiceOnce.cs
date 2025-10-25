// Assets/Scripts/Audio/IntroVoiceOnce.cs
using UnityEngine;
using System.Collections;

/// <summary>
/// 讓場景中的「介紹音效」只在整個遊戲生命週期內播放一次。
/// 掛在含有 AudioSource 的物件上即可。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class IntroVoiceOnce : MonoBehaviour
{
    [Tooltip("若勾選，這個物件會跨場景不被銷毀。通常不需要勾。")]
    public bool dontDestroyOnLoad = false;

    [Tooltip("進入第一個場景時是否自動播放（取代 AudioSource.playOnAwake）。")]
    public bool playOnStart = true;

    [Tooltip("延遲多少秒再播（避免場景切換瞬間被裁切）。")]
    public float delay = 0f;

    // 本次 App 執行期間是否已經播過
    static bool s_alreadyPlayed = false;

    AudioSource _src;

    void Awake()
    {
        _src = GetComponent<AudioSource>();
        // 關閉原本的 Play On Awake，改由本腳本控制
        if (_src.playOnAwake) _src.playOnAwake = false;

        if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        // 若已經播過，確保之後不會再自動播
        if (s_alreadyPlayed && _src != null && _src.isPlaying)
            _src.Stop();
    }

    void Start()
    {
        if (s_alreadyPlayed) { enabled = false; return; }

        if (playOnStart && _src.clip != null)
        {
            if (delay > 0f) StartCoroutine(CoPlay());
            else _src.Play();

            s_alreadyPlayed = true;
        }
    }

    IEnumerator CoPlay()
    {
        yield return new WaitForSeconds(delay);
        if (_src && !_src.isPlaying) _src.Play();
    }
}
