using System.Collections;
using UnityEngine;
using TMPro;

public class VoiceTest2 : MonoBehaviour
{
    public AudioSource broadcastSource;
    public TextMeshProUGUI statusText;

    void Start()
    {
        StartCoroutine(StartGameSequence());
    }

    IEnumerator StartGameSequence()
    {
        broadcastSource.Play();
        yield return new WaitForSeconds(broadcastSource.clip.length);
        yield return new WaitForSeconds(3f);
        StartCoroutine(RecordPlayerVoice(7f));
    }

    IEnumerator RecordPlayerVoice(float duration)
    {
        statusText.text = "錄音中...";
        string micName = Microphone.devices[0];
        AudioClip recordedClip = Microphone.Start(micName, false, (int)duration, 44100);
        yield return new WaitForSeconds(duration);
        Microphone.End(micName);
        statusText.text = "錄音完成";
    }
}
