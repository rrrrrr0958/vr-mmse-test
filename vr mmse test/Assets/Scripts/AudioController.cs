using UnityEngine;
using System.Collections;

public class AudioController : MonoBehaviour
{
    [Header("Audio Source")]
    public AudioSource audioSource;

    [Header("Audio Clips")]
    public AudioClip starSpawnSound; // 星星出現音效
    public AudioClip buttonClickSound; // 按鈕點擊音效

    [Header("Volume Settings")]
    public float playSoundDelay = 0.5f;
    [Range(0f, 1f)] public float starSoundVolume = 1f;
    [Range(0f, 1f)] public float buttonSoundVolume = 1f;

    void Start()
    {
        // 如果沒有指定 AudioSource，自動獲取或創建
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        StartCoroutine(PlayStarSpawnSound());
    }

    /// <summary>
    /// 播放星星出現音效
    /// </summary>
    private IEnumerator PlayStarSpawnSound()
    {
        yield return new WaitForSeconds(playSoundDelay);
        if (starSpawnSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(starSpawnSound, starSoundVolume);
        }
        else
        {
            Debug.LogWarning("AudioController: 星星音效或 AudioSource 未設定!");
        }
    }

    /// <summary>
    /// 播放按鈕點擊音效
    /// </summary>
    public void PlayButtonClickSound()
    {
        if (buttonClickSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(buttonClickSound, buttonSoundVolume);
        }
        else
        {
            Debug.LogWarning("AudioController: 按鈕音效或 AudioSource 未設定!");
        }
    }
}