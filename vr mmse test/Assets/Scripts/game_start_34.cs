using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    // =========================================================================
    // ���}�ܼ� (�b Unity Inspector ���]�w)
    // =========================================================================

    [Header("�C���}�l�]�w")]
    public float initialTextDelay = 3f;      // �C���}�l��A�u���r��ܫe������ɶ�
    public float questionBroadcastDelay = 2f; // �u���r��ܫ�A���D�s���e������ɶ�
    public float timeBetweenQuestions = 2f;  // ���D����������ɶ� (�Ω󵥫ݻy���^��)
    public float voiceQuestionBufferTime = 0.5f; // �y�����񧹦��᪺�B�~�w�Įɶ�

    // �i�s�W�j�I���D�ت����ݮɶ�
    [Header("�I���D�]�w")]
    public float clickResponseDuration = 3.0f; // �C���I���D�ص��ݨϥΪ��I������� (���ɶ��q���|�ʴ��I��)


    [Header("��v���ؼ��I")]
    public Transform cameraTarget_FishStall; // ���u��v���ؼ�

    [Header("��v�����ʳ]�w")]
    public float cameraMoveSpeed = 50.0f;    // ��v�����ʪ��t�סA�ƭȶV�j�V��

    [Header("UI �s��")]
    public TMPro.TextMeshPro questionBroadcastTextMeshPro; // �Ω���� "���I�� XX �u��" �� TextMeshPro �ե� (�i��A�p�G���b�ù���ܫh����)
    public Image highlightCircleImage; // �s�W�G�Ω󰪫G��ܪ���� UI Image

    // �y�����D�����ܼ�
    [Header("�y�����D�]�w")]
    public AudioSource voiceAudioSource; // �Ω󼽩�y���� AudioSource�A�j�w Main Camera �W��
    public AudioClip fishStallAudioClip;    // "���u" ���y���ɮ�
    public AudioClip fruitStallAudioClip;   // "���G�u" ���y���ɮ�
    public AudioClip weaponStallAudioClip;  // "�Z���u" ���y���ɮ�
    public AudioClip breadStallAudioClip;   // "�ѥ]�u" ���y���ɮ�
    public AudioClip meatStallAudioClip;    // "���u" ���y���ɮ�

    // ���u�M�ݰ��D�y���ɮ�
    [Header("���u���D�y���]�w")]
    public AudioClip whatIsSellingAudioClip; // "�o���u��b�椰��H" ���y���ɮ�
    public AudioClip fishColorAudioClip;     // "�����C��O����H" ���y���ɮ�
    public AudioClip whatIsThatAudioClip;    // "���ӬO����H" ���y���ɮ�


    // =========================================================================
    // �p���ܼ� (�}�������ϥ�)
    // =========================================================================

    private GameObject[] stallRootObjects;
    private List<string> stallNames = new List<string>();
    private List<string> nonFishStallNames = new List<string>(); // ���]�t���u���W�٦C��

    private bool hasClickedStall = false; // ���ܼƱN�Ω󱱨�O�_�w�i�J���u�y�{
    private Coroutine initialQuestionCoroutine;

    private string currentTargetStallName = ""; // �x�s��e�Q�n�D���ؼ��u��W��

    // �i�s�W�j�l�ܥ��T�I��������
    private int correctAnswersCount = 0;

    // �i�s�W�j�лx�A��ܷ�e�O�_�B�󵥫ݪ��a�I�������q�]�w�����T�D�^
    private bool isWaitingForClickInput = false;

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
                stallNames.Add(stallName); // �[�J�Ҧ��u��W��

                // �N�D���u���W�٥[�J��t�@�ӦC��
                if (stallName != "���u") // �T�O "���u" �o�Ӧr��M�A�� TextMeshPro �����@�P
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


        if (cameraTarget_FishStall == null)
        {
            Debug.LogError("Error: cameraTarget_FishStall is not assigned in the Inspector! Please assign it.");
        }
        if (questionBroadcastTextMeshPro == null)
        {
            Debug.LogWarning("Warning: questionBroadcastTextMeshPro is not assigned in the Inspector. Initial question will only appear in Console.");
        }
        if (highlightCircleImage == null)
        {
            Debug.LogError("Error: highlightCircleImage is not assigned in the Inspector! Please assign it for question 3.");
        }

        // �ˬd�y�������ܼƬO�_�]�w
        if (voiceAudioSource == null)
        {
            Debug.LogError("Error: voiceAudioSource is not assigned in the Inspector! Please assign the AudioSource from Main Camera.");
        }
        if (fishStallAudioClip == null || fruitStallAudioClip == null || weaponStallAudioClip == null ||
            breadStallAudioClip == null || meatStallAudioClip == null ||
            whatIsSellingAudioClip == null || fishColorAudioClip == null || whatIsThatAudioClip == null)
        {
            Debug.LogWarning("Warning: Some AudioClips are not assigned. Voice questions may not play.");
        }

        // ��l�T�ΰ��
        if (highlightCircleImage != null)
        {
            highlightCircleImage.gameObject.SetActive(false);
        }
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
        // �i�ק�j�u�b���ݪ��a�I����J�����q�A�åB�� currentTargetStallName ���Ȯɤ~�ʴ��I��
        // �B�u�ˬd�ƹ�������U�@�� (GetMouseButtonDown(0))
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
                        correctAnswersCount++; // �I�����T�A�W�[����
                    }
                    else
                    {
                        Debug.LogWarning($"�I���F���~���u��: {clickedTextMeshPro?.text ?? "�����u��"}�C���T���׬O {currentTargetStallName}�C���D���~�I");
                    }
                    // �L���I�����T�P�_�A�u�n���@���I���Q�B�z�A�N�N currentTargetStallName �M�šC
                    // �o�˥i�H�T�O�b�@�� `clickResponseDuration` �����u�B�z�@�������I���A
                    // �קK���ƭp���έ��Ƥ�x�C
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

    // �i�D�n�ק�j
    IEnumerator MainClickSequence()
    {
        yield return new WaitForSeconds(initialTextDelay + questionBroadcastDelay); // �����u��W�����

        List<string> tempNonFishStallNames = new List<string>(nonFishStallNames);

        correctAnswersCount = 0; // �b�}�l�s�@���D�خɭ��m�p��

        // �Ĥ@�B�G���H����ܡ]���]�t���u�A�B�����ơ^
        for (int i = 0; i < 2; i++)
        {
            if (tempNonFishStallNames.Count == 0) // �p�G�S���������D���u�ﶵ�F
            {
                Debug.LogWarning("�S���������D���u�W�٥i���H����ܡI�нT�O�ܤ֦���ӫD���u�C");
                yield break; // �L�k�~��A�פ��{
            }

            int randomIndex = Random.Range(0, tempNonFishStallNames.Count);
            currentTargetStallName = tempNonFishStallNames[randomIndex]; // �]�w�ؼ��u��AUpdate �|��ť�o�ӥؼ�

            string initialQuestion = $"���I�� {currentTargetStallName} �u��I";
            Debug.Log($"Console ���D (�� {i + 1} ��): {initialQuestion}");
            PlayInitialVoiceQuestion(currentTargetStallName); // ����y��

            tempNonFishStallNames.RemoveAt(randomIndex); // �q�{�ɦC�������w�Q�襤���u��

            // �i�s�W�j�]�m�X�СA�q�� Update �i�H�}�l�ʴ��I��
            isWaitingForClickInput = true;

            // �p���`���ݮɶ��G�y������ + �y������᪺�w�� + ���a�I�������ɶ�
            AudioClip currentClip = GetAudioClipForStall(currentTargetStallName);
            float totalWaitTime = (currentClip != null ? currentClip.length : 0f) + voiceQuestionBufferTime + clickResponseDuration;

            yield return new WaitForSeconds(totalWaitTime); // ���ݫ��w�ɶ��A�L�׬O�_�I��

            // �i���m�j�M�� currentTargetStallName ��������ť�X�СA��ܸ��D�ت��I���T���ɶ��w�L
            currentTargetStallName = "";
            isWaitingForClickInput = false;

            // �T�O�D�P�D���������j�A�o�i�H�b totalWaitTime ���[�\�A�]�i�H�B�~�[�@�Ӥp����
            yield return new WaitForSeconds(timeBetweenQuestions);
        }

        // �ĤT���T�w�I�����u
        currentTargetStallName = "���u"; // �T�w�ؼЬ����u�AUpdate �|��ť�o�ӥؼ�
        string finalQuestion = $"���I�� {currentTargetStallName} �u��I";
        Debug.Log($"Console ���D (�� 3 ���A�T�w���u): {finalQuestion}");
        PlayInitialVoiceQuestion(currentTargetStallName); // �����u�y��

        // �i�s�W�j�]�m�X�СA�q�� Update �i�H�}�l�ʴ��I��
        isWaitingForClickInput = true;

        // �p���`���ݮɶ�
        AudioClip fishStallClip = GetAudioClipForStall("���u");
        float fishStallTotalWaitTime = (fishStallClip != null ? fishStallClip.length : 0f) + voiceQuestionBufferTime + clickResponseDuration;
        yield return new WaitForSeconds(fishStallTotalWaitTime); // ���ݫ��w�ɶ�

        // �i���m�j�M�� currentTargetStallName ��������ť�X��
        currentTargetStallName = "";
        isWaitingForClickInput = false;

        // �i�s�W�j�O���`���� Console
        Debug.Log($"���T�D�ؼ�: {correctAnswersCount}/3");

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

    IEnumerator FishStallQuestionSequence()
    {
        // ���ݶi�J���u���w�Įɶ� (�p�G�A�Ʊ�b���u���D�}�l�e���@�q�R�q)
        yield return new WaitForSeconds(timeBetweenQuestions); // �o��i�H�O�d�A�@�����D�}�l�e���w��

        // �Ĥ@�Ӱ��D
        Debug.Log("Console ���D: �o���u��b�椰��H");
        PlayVoiceClip(whatIsSellingAudioClip, "�o���u��b�椰��H");
        yield return StartCoroutine(WaitForVoiceToFinish(whatIsSellingAudioClip)); // ���ݻy�����񧹲�

        // �ĤG�Ӱ��D
        Debug.Log("Console ���D: �����C��O����H");
        PlayVoiceClip(fishColorAudioClip, "�����C��O����H");
        yield return StartCoroutine(WaitForVoiceToFinish(fishColorAudioClip)); // ���ݻy�����񧹲�

        // �ĤT�Ӱ��D (�a���)
        Debug.Log("Console ���D: ���ӬO����H");
        PlayVoiceClip(whatIsThatAudioClip, "���ӬO����H");
        yield return StartCoroutine(WaitForVoiceToFinish(whatIsThatAudioClip)); // ���ݻy�����񧹲�

        ShowHighlightCircle();
        yield return new WaitForSeconds(timeBetweenQuestions); // �o�̥i�H�ھڻݭn�վ㵥�ݮɶ��A�]���y���w����
        HideHighlightCircle();

        Debug.Log("Console: �Ҧ����u���D�w�����I");
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

    // �q�Ϊ��y�������k�A�Ω��l���D
    void PlayInitialVoiceQuestion(string stallName)
    {
        Debug.Log($"���ռ���y�����u��: '{stallName}' (����: {stallName.Length})");

        AudioClip clipToPlay = GetAudioClipForStall(stallName); // �ϥλ��U��k��� AudioClip

        PlayVoiceClip(clipToPlay, stallName); // �ϥγq�Ϊ������k
    }

    // �i�s�W�j���U��k�G�ھ��u��W����������� AudioClip
    private AudioClip GetAudioClipForStall(string stallName)
    {
        switch (stallName)
        {
            case "���u": return fishStallAudioClip;
            case "���G": return fruitStallAudioClip; // �T�O�o�̪��r��P�A������u��W�٩M AudioClip �ǰt
            case "�Z��": return weaponStallAudioClip;
            case "�ѥ]": return breadStallAudioClip;
            case "���u": return meatStallAudioClip;
            default: return null;
        }
    }

    // �q�Ϊ��y������p����k
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

    // ���ݻy�����񧹦�����{ (�Ω��u���D����)
    private IEnumerator WaitForVoiceToFinish(AudioClip clip)
    {
        if (voiceAudioSource == null)
        {
            Debug.LogWarning("AudioSource ���]�w�A�L�k���ݻy�����񧹦��C");
            yield break;
        }

        if (clip == null)
        {
            Debug.LogWarning("�n���ݪ� AudioClip ���šC");
            yield break;
        }

        yield return new WaitForSeconds(clip.length + voiceQuestionBufferTime);
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
}
