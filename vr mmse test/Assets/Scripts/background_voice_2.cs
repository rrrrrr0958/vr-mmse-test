using UnityEngine;
using System.Collections;

public class BackgroundVoice2 : MonoBehaviour
{
    public AudioSource broadcastSource;
    public RecordingState2 recorder;  // Reference to the recorder script

    void Start()
    {
        StartCoroutine(StartGameSequence());
    }

    IEnumerator StartGameSequence()
    {
        // Step 1: 播放音檔
        broadcastSource.Play();
        yield return new WaitForSeconds(broadcastSource.clip.length);

        // Step 2: 等待3秒
        yield return new WaitForSeconds(3f);

        // Step 3: 錄音
        recorder.StartRecording(7f);  // 改成呼叫別的 script 裡的錄音
    }
}
