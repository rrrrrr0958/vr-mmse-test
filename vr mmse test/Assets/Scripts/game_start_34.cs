using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using UnityEngine.Networking;
using System.Text.RegularExpressions;
using System.Text;

public class GameManager : MonoBehaviour
{
    // =========================================================================
    // ���}�ܼ� (�b Unity Inspector ���]�w)
    // =========================================================================

    [Header("�C���}�l�]�w")]
    public float initialTextDelay = 3f;
    public float questionBroadcastDelay = 2f;
    public float timeBetweenQuestions = 2f;
    public float voiceQuestionBufferTime = 0.5f;

    [Header("�I���D�]�w")]
    public float clickResponseDuration = 3.0f;

    [Header("��v���ؼ��I")]
    public Transform cameraTarget_FishStall;

    [Header("��v�����ʳ]�w")]
    public float cameraMoveSpeed = 50.0f;

    [Header("UI �s��")]
    public TMPro.TextMeshPro questionBroadcastTextMeshPro;
    public Image highlightCircleImage;

    [Header("�y�����D�]�w")]
    public AudioSource voiceAudioSource;
    public AudioClip fishStallAudioClip;
    public AudioClip fruitStallAudioClip;
    public AudioClip weaponStallAudioClip;
    public AudioClip breadStallAudioClip;
    public AudioClip meatStallAudioClip;

    [Header("���u���D�y���]�w")]
    public AudioClip whatIsSellingAudioClip;
    public AudioClip fishColorAudioClip;
    public AudioClip whatIsThatAudioClip;

    [Header("�y�����ѳ]�w")]
    public string serverUrl = "http://localhost:5000/recognize_speech";
    public float recordingDuration = 5.0f;

    // =========================================================================
    // �p���ܼ� (�}�������ϥ�)
    // =========================================================================

    private GameObject[] stallRootObjects;
    private List<string> stallNames = new List<string>();
    private List<string> nonFishStallNames = new List<string>();
    private bool hasClickedStall = false;
    private Coroutine initialQuestionCoroutine;
    private string currentTargetStallName = "";
    private int correctAnswersCount = 0;
    private bool isWaitingForClickInput = false;

    private DatabaseReference dbReference;
    private FirebaseApp app;
    private AudioClip recordingClip;
    private List<string> currentQuestionAnswers = new List<string>();


    // =========================================================================
    // Unity �ͩR�P����k
    // =========================================================================

    void Awake()
    {
        stallRootObjects = GameObject.FindGameObjectsWithTag("StallNameText");
        Debug.Log($"Awake: Found {stallRootObjects.Length} stall clickable root objects by tag.");

        foreach (GameObject stallRoot in stallRootObjects)
        {
            stallRoot.SetActive(false);
            TMPro.TextMeshPro textMeshPro = stallRoot.GetComponentInChildren<TMPro.TextMeshPro>();
            if (textMeshPro != null)
            {
                string stallName = textMeshPro.text;
                stallNames.Add(stallName);
                if (stallName != "���u")
                {
                    nonFishStallNames.Add(stallName);
                }
            }
            else
            {
                Debug.LogWarning($"Awake: Stall root '{stallRoot.name}' has 'StallNameText' tag but no TextMeshPro component found in children. This stall name will not be used for initial question.");
            }
        }
        Debug.Log($"Awake: Total stall names collected for initial question: {stallNames.Count}");
        Debug.Log($"Awake: Non-fish stall names collected: {nonFishStallNames.Count}");

        if (cameraTarget_FishStall == null) Debug.LogError("Error: cameraTarget_FishStall is not assigned in the Inspector! Please assign it.");
        if (questionBroadcastTextMeshPro == null) Debug.LogWarning("Warning: questionBroadcastTextMeshPro is not assigned in the Inspector. Initial question will only appear in Console.");
        if (highlightCircleImage == null) Debug.LogError("Error: highlightCircleImage is not assigned in the Inspector! Please assign it for question 3.");
        if (voiceAudioSource == null) Debug.LogError("Error: voiceAudioSource is not assigned in the Inspector! Please assign the AudioSource from Main Camera.");
        if (fishStallAudioClip == null || fruitStallAudioClip == null || weaponStallAudioClip == null ||
            breadStallAudioClip == null || meatStallAudioClip == null ||
            whatIsSellingAudioClip == null || fishColorAudioClip == null || whatIsThatAudioClip == null)
        {
            Debug.LogWarning("Warning: Some AudioClips are not assigned. Voice questions may not play.");
        }

        if (highlightCircleImage != null) highlightCircleImage.gameObject.SetActive(false);

        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task => {
            var dependencyStatus = task.Result;
            if (dependencyStatus == DependencyStatus.Available)
            {
                app = FirebaseApp.DefaultInstance;
                dbReference = FirebaseDatabase.DefaultInstance.RootReference;
                Debug.Log("Firebase �w���\��l�ơI");
            }
            else
            {
                Debug.LogError($"�L�k�ѨM Firebase �̿���D: {dependencyStatus}");
            }
        });
    }

    void Start()
    {
        Debug.Log("GameManager Start() called.");
        if (questionBroadcastTextMeshPro != null)
        {
            questionBroadcastTextMeshPro.gameObject.SetActive(false);
        }
        StartCoroutine(ShowTextsAfterDelay());
        initialQuestionCoroutine = StartCoroutine(MainClickSequence());
    }

    void Update()
    {
        if (isWaitingForClickInput && !hasClickedStall && !string.IsNullOrEmpty(currentTargetStallName) && Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            int stallLayerMask = 1 << LayerMask.NameToLayer("StallLayer");

            if (Physics.Raycast(ray, out hit, Mathf.Infinity, stallLayerMask))
            {
                if (hit.collider.CompareTag("StallNameText"))
                {
                    TMPro.TextMeshPro clickedTextMeshPro = hit.collider.GetComponentInChildren<TMPro.TextMeshPro>();
                    if (clickedTextMeshPro != null && clickedTextMeshPro.text == currentTargetStallName)
                    {
                        Debug.Log($"���T�������I���ؼ��u��: {currentTargetStallName}�C���D���T�I");
                        correctAnswersCount++;
                    }
                    else
                    {
                        Debug.LogWarning($"�I���F���~���u��: {clickedTextMeshPro?.text ?? "�����u��"}�C���T���׬O {currentTargetStallName}�C���D���~�I");
                    }
                    currentTargetStallName = "";
                }
            }
        }
    }

    // =========================================================================
    // �C���y�{�����k
    // =========================================================================

    void HideAllStallNamesAndQuestion()
    {
        foreach (GameObject stallRoot in stallRootObjects)
        {
            stallRoot.SetActive(false);
        }
        if (questionBroadcastTextMeshPro != null)
        {
            questionBroadcastTextMeshPro.gameObject.SetActive(false);
        }
    }

    IEnumerator ShowTextsAfterDelay()
    {
        yield return new WaitForSeconds(initialTextDelay);
        foreach (GameObject stallRoot in stallRootObjects)
        {
            stallRoot.SetActive(true);
        }
        Debug.Log("�Ҧ��u��W�٤w��ܡC");
    }

    IEnumerator MainClickSequence()
    {
        yield return new WaitForSeconds(initialTextDelay + questionBroadcastDelay);

        List<string> tempNonFishStallNames = new List<string>(nonFishStallNames);

        correctAnswersCount = 0;

        for (int i = 0; i < 2; i++)
        {
            if (tempNonFishStallNames.Count == 0)
            {
                Debug.LogWarning("�S���������D���u�W�٥i���H����ܡI�нT�O�ܤ֦���ӫD���u�C");
                yield break;
            }

            int randomIndex = Random.Range(0, tempNonFishStallNames.Count);
            currentTargetStallName = tempNonFishStallNames[randomIndex];

            string initialQuestion = $"���I�� {currentTargetStallName} �u��I";
            Debug.Log($"Console ���D (�� {i + 1} ��): {initialQuestion}");
            PlayInitialVoiceQuestion(currentTargetStallName);

            tempNonFishStallNames.RemoveAt(randomIndex);

            isWaitingForClickInput = true;

            AudioClip currentClip = GetAudioClipForStall(currentTargetStallName);
            float totalWaitTime = (currentClip != null ? currentClip.length : 0f) + voiceQuestionBufferTime + clickResponseDuration;

            yield return new WaitForSeconds(totalWaitTime);

            currentTargetStallName = "";
            isWaitingForClickInput = false;

            yield return new WaitForSeconds(timeBetweenQuestions);
        }

        currentTargetStallName = "���u";
        string finalQuestion = $"���I�� {currentTargetStallName} �u��I";
        Debug.Log($"Console ���D (�� 3 ���A�T�w���u): {finalQuestion}");
        PlayInitialVoiceQuestion(currentTargetStallName);

        isWaitingForClickInput = true;

        AudioClip fishStallClip = GetAudioClipForStall("���u");
        float fishStallTotalWaitTime = (fishStallClip != null ? fishStallClip.length : 0f) + voiceQuestionBufferTime + clickResponseDuration;
        yield return new WaitForSeconds(fishStallTotalWaitTime);

        currentTargetStallName = "";
        isWaitingForClickInput = false;

        Debug.Log($"���T�D�ؼ�: {correctAnswersCount}/3");

        if (dbReference != null)
        {
            string userId = SystemInfo.deviceUniqueIdentifier;
            string timestamp = System.DateTime.Now.ToString("yyyyMMddHHmmss");
            string recordKey = $"{userId}_{timestamp}";

            Dictionary<string, object> scoreData = new Dictionary<string, object>();
            scoreData["correctAnswers"] = correctAnswersCount;
            scoreData["totalQuestions"] = 3;
            scoreData["timestamp"] = ServerValue.Timestamp;
            scoreData["userName"] = "PlayerName";

            dbReference.Child("scores").Child(recordKey).SetValueAsync(scoreData).ContinueWithOnMainThread(task =>
            {
                if (task.IsCompleted)
                {
                    Debug.Log($"���\�N���Ƽg�J Firebase: ���T {correctAnswersCount}/3");
                }
                else if (task.IsFaulted)
                {
                    Debug.LogError($"�g�J Firebase ����: {task.Exception}");
                }
            });
        }
        else
        {
            Debug.LogWarning("Firebase Database ����l�ơA�L�k�g�J���ơC");
        }

        Debug.Log("�Ҧ��I�����ȧ����A�ǳƶi�J���u�y�{�C");
        hasClickedStall = true;
        HideAllStallNamesAndQuestion();
        StartCoroutine(MoveCameraToFishStallAndStartFishStallQuestions());
    }

    IEnumerator MoveCameraToFishStallAndStartFishStallQuestions()
    {
        Debug.Log("�ǳƱN��v����V���u...");
        if (cameraTarget_FishStall == null)
        {
            Debug.LogError("cameraTarget_FishStall is not assigned! Cannot move camera.");
            yield break;
        }
        yield return StartCoroutine(SmoothCameraMove(cameraTarget_FishStall.position, cameraTarget_FishStall.rotation));
        Debug.Log("��v���w���\��V���u�C");
        StartCoroutine(FishStallQuestionSequence());
    }

    // �i�ק�j�N�y�������޿�q StartCoroutine(WaitForAnswer) ������
    IEnumerator FishStallQuestionSequence()
    {
        yield return new WaitForSeconds(timeBetweenQuestions);

        // �Ĥ@�ӻy�����D
        Debug.Log("Console ���D: �o���u��b�椰��H");
        yield return StartCoroutine(PlayAudioClipAndThenWait(whatIsSellingAudioClip));
        yield return StartCoroutine(WaitForAnswer(new List<string> { "��", "����", "���u", "��", "���A", "�����u" }));

        // �ĤG�ӻy�����D
        Debug.Log("Console ���D: �����C��O����H");
        yield return StartCoroutine(PlayAudioClipAndThenWait(fishColorAudioClip));
        yield return StartCoroutine(WaitForAnswer(new List<string> { "�Ŧ�", "��", "�ť�", "�ťզ�", "�L��", "�L�Ŧ�" }));

        // �ĤT�ӻy�����D (�a���)
        Debug.Log("Console ���D: ���ӬO����H");
        yield return StartCoroutine(PlayAudioClipAndThenWait(whatIsThatAudioClip));
        ShowHighlightCircle();
        yield return StartCoroutine(WaitForAnswer(new List<string> { "�O", "���O" }));
        HideHighlightCircle();

        Debug.Log("Console: �Ҧ����u���D�w�����I");
    }

    // �i�s�W�j�@�ӱM���Ω󼽩񭵰T�õ��ݨ伽�񧹲�����{
    IEnumerator PlayAudioClipAndThenWait(AudioClip clip)
    {
        if (voiceAudioSource == null || clip == null)
        {
            Debug.LogWarning("�L�k���񭵰T�AAudioSource �� AudioClip ���šC");
            yield break;
        }

        // �֤߭ק�G�N���T���w�� voiceAudioSource.clip
        voiceAudioSource.clip = clip;
        voiceAudioSource.Play();
        Debug.Log($"���b����y���A����: {clip.length} ��");

        // ���ݻy�����񧹲�
        yield return new WaitForSeconds(clip.length + voiceQuestionBufferTime);
    }


    void ShowHighlightCircle()
    {
        if (highlightCircleImage != null)
        {
            highlightCircleImage.gameObject.SetActive(true);
            Debug.Log("HighlightCircle �w�ҥΨ���ܡC���m�M�j�p�� Editor �]�w�C");
        }
        else
        {
            Debug.LogError("HighlightCircleImage ����ȡA�L�k��ܰ��I");
        }
    }

    void HideHighlightCircle()
    {
        if (highlightCircleImage != null)
        {
            highlightCircleImage.gameObject.SetActive(false);
            Debug.Log("HighlightCircle �w�T�ΡC");
        }
    }

    void PlayInitialVoiceQuestion(string stallName)
    {
        Debug.Log($"���ռ���y�����u��: '{stallName}' (����: {stallName.Length})");
        AudioClip clipToPlay = GetAudioClipForStall(stallName);
        PlayVoiceClip(clipToPlay, stallName);
    }

    private AudioClip GetAudioClipForStall(string stallName)
    {
        switch (stallName)
        {
            case "���u": return fishStallAudioClip;
            case "���G": return fruitStallAudioClip;
            case "�Z��": return weaponStallAudioClip;
            case "�ѥ]": return breadStallAudioClip;
            case "���u": return meatStallAudioClip;
            default: return null;
        }
    }

    // �i�ק�j���禡�אּ PlayOneShot�A�u�Ω��I���D�ت��y������
    private void PlayVoiceClip(AudioClip clip, string debugMessageContext)
    {
        if (voiceAudioSource != null)
        {
            if (clip != null)
            {
                voiceAudioSource.PlayOneShot(clip);
                Debug.Log($"Playing voice clip: '{debugMessageContext}'");
            }
            else
            {
                Debug.LogWarning($"�L�k����y���� '{debugMessageContext}'�C��]: AudioClip ���]�w�C");
            }
        }
        else
        {
            Debug.LogWarning($"�L�k����y���� '{debugMessageContext}'�C��]: AudioSource ���]�w�C");
        }
    }

    IEnumerator SmoothCameraMove(Vector3 targetPosition, Quaternion targetRotation)
    {
        Transform mainCameraTransform = Camera.main.transform;
        Vector3 startPosition = mainCameraTransform.position;
        Quaternion startRotation = mainCameraTransform.rotation;
        float elapsedTime = 0;
        float duration = Vector3.Distance(startPosition, targetPosition) / cameraMoveSpeed;
        if (duration < 0.05f) duration = 0.05f;
        while (elapsedTime < duration)
        {
            mainCameraTransform.position = Vector3.Lerp(startPosition, targetPosition, elapsedTime / duration);
            mainCameraTransform.rotation = Quaternion.Slerp(startRotation, targetRotation, elapsedTime / duration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        mainCameraTransform.position = targetPosition;
        mainCameraTransform.rotation = targetRotation;
        Debug.Log("��v�����Ʋ��ʧ����C");
    }

    // =========================================================================
    // �i�s�W�j�y�����Ѭ������֤ߨ禡
    // =========================================================================

    IEnumerator WaitForAnswer(List<string> correctAnswers)
    {
        // �֤߭ק�G�����즳�����ݭ��T����ɶ����{���X
        // �]���{�b�ڭ̦b�I�s�o�Ө�{�e�w�g�����ݭ��T���񧹲��F

        Debug.Log("�л��X�A������...");
        questionBroadcastTextMeshPro.text = "�л��X�A������...";

        if (Microphone.devices.Length > 0)
        {
            Debug.Log("�}�l����...");
            recordingClip = Microphone.Start(null, false, (int)recordingDuration, 44100);
            yield return new WaitForSeconds(recordingDuration);
            Microphone.End(null);
            Debug.Log("���������C");

            byte[] wavData = ConvertAudioClipToWav(recordingClip);
            yield return StartCoroutine(SendAudioToServer(wavData, correctAnswers));
        }
        else
        {
            Debug.LogError("�S�������J���]�ơI");
            questionBroadcastTextMeshPro.text = "�S�������J���]�ơI";
            yield return new WaitForSeconds(2.0f);
        }
    }

    IEnumerator SendAudioToServer(byte[] audioData, List<string> correctAnswers)
    {
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", audioData, "recording.wav", "audio/wav");

        UnityWebRequest request = UnityWebRequest.Post(serverUrl, form);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string jsonResponse = request.downloadHandler.text;
            Debug.Log("���A���^��: " + jsonResponse);
            try
            {
                RecognitionResponse response = JsonUtility.FromJson<RecognitionResponse>(jsonResponse);
                CheckAnswer(response.transcription, correctAnswers);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("�ѪR JSON ����: " + ex.Message);
                questionBroadcastTextMeshPro.text = "���ѥ��ѡA�ЦA�դ@���C";
            }
        }
        else
        {
            Debug.LogError("�y�����ѽШD����: " + request.error);
            questionBroadcastTextMeshPro.text = "�������~�Φ��A�����D�C";
        }
    }

    void CheckAnswer(string userResponse, List<string> correctAnswers)
    {
        if (string.IsNullOrEmpty(userResponse))
        {
            Debug.Log("�S��ť��^���C");
            questionBroadcastTextMeshPro.text = "�S��ť��^���C";
            return;
        }

        bool isCorrect = false;
        // �N�ϥΪ̦^�����W�Ƭ��p�g�A�åh���e��ť�
        string normalizedResponse = userResponse.Trim().ToLower();

        foreach (string correctAnswer in correctAnswers)
        {
            // �N���T���פ]���W��
            string normalizedCorrectAnswer = correctAnswer.Trim().ToLower();

            // �֤߭ק�G�P�_�覡�q Equals �אּ Contains�A�H�W�[�u��
            if (normalizedResponse.Contains(normalizedCorrectAnswer))
            {
                isCorrect = true;
                break;
            }
        }

        if (isCorrect)
        {
            Debug.Log($"���ץ��T�I�A���F: \"{userResponse}\"");
            questionBroadcastTextMeshPro.text = "���ץ��T�I";
        }
        else
        {
            Debug.Log($"���׿��~�C�A���F: \"{userResponse}\"�A���T���׬O: \"{string.Join("/", correctAnswers)}\"");
            questionBroadcastTextMeshPro.text = $"���׿��~�C";
        }

        StartCoroutine(ShowResultAndContinue(isCorrect));
    }

    IEnumerator ShowResultAndContinue(bool isCorrect)
    {
        if (isCorrect)
        {
            correctAnswersCount++;
        }
        yield return new WaitForSeconds(timeBetweenQuestions);
    }

    byte[] ConvertAudioClipToWav(AudioClip clip)
    {
        if (clip == null) return new byte[0];

        const int headerSize = 44;
        byte[] bytes = new byte[clip.samples * 2 * clip.channels + headerSize];
        int format = 1;
        int channels = clip.channels;
        int sampleRate = clip.frequency;
        int bitDepth = 16;
        int byteRate = sampleRate * channels * (bitDepth / 8);
        int blockAlign = channels * (bitDepth / 8);

        System.Text.Encoding.UTF8.GetBytes("RIFF").CopyTo(bytes, 0);
        System.BitConverter.GetBytes(bytes.Length - 8).CopyTo(bytes, 4);
        System.Text.Encoding.UTF8.GetBytes("WAVE").CopyTo(bytes, 8);
        System.Text.Encoding.UTF8.GetBytes("fmt ").CopyTo(bytes, 12);
        System.BitConverter.GetBytes(16).CopyTo(bytes, 16);
        System.BitConverter.GetBytes((short)format).CopyTo(bytes, 20);
        System.BitConverter.GetBytes((short)channels).CopyTo(bytes, 22);
        System.BitConverter.GetBytes(sampleRate).CopyTo(bytes, 24);
        System.BitConverter.GetBytes(byteRate).CopyTo(bytes, 28);
        System.BitConverter.GetBytes((short)blockAlign).CopyTo(bytes, 32);
        System.BitConverter.GetBytes((short)bitDepth).CopyTo(bytes, 34);
        System.Text.Encoding.UTF8.GetBytes("data").CopyTo(bytes, 36);
        System.BitConverter.GetBytes(clip.samples * blockAlign).CopyTo(bytes, 40);

        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        for (int i = 0; i < samples.Length; i++)
        {
            short pcmValue = (short)(samples[i] * short.MaxValue);
            System.BitConverter.GetBytes(pcmValue).CopyTo(bytes, headerSize + i * 2);
        }

        return bytes;
    }
}

// �T�O�����O�u�b�A���M�פ��s�b�@�Ӱƥ�
// �p�G���w�g�b�t�@�Ӹ}�����A�бN���R��
// [System.Serializable]
// public class RecognitionResponse
// {
//     public string transcription;
// }