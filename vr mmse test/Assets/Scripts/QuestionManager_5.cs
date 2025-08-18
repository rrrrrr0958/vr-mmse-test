using UnityEngine;
using TMPro; // �ޤJTextMeshPro�R�W�Ŷ�
using System.Collections; // �ޤJ�Ω��{���R�W�Ŷ�
using System.Collections.Generic; // �ޤJ�Ω�List���R�W�Ŷ�
using System.Linq; // �ޤJ�Ω�LINQ�A��K�H�����
using UnityEngine.Networking; // �s�W�o��
using System.Text.RegularExpressions; // �Ω󥿳W��F���A�����Ʀr

public class QuestionManager : MonoBehaviour
{
    public TextMeshPro questionText; // �Ω���ܤ�r�� TextMeshPro (3D)
    public GameObject panelBackground; // ���D�I�����O�� GameObject (3D Quad �� Plane)
    public float delayBetweenQuestions = 3.0f; // �C�D����������ɶ�

    public AudioSource questionAudioSource; // �Ω󼽩��D�ػy���� AudioSource

    private string initialMoneyQuestion = "�{�b�A��100��";
    private List<string> answerOptions = new List<string>
    {
        "��O25���R�F������Ѧh��?",
        "��O7���R�F�ѥ]����Ѧh��?",
        "��O35���R�F���G����Ѧh��?",
        "��O15���R�F�Z������Ѧh��?",
        "��O30���R�F�פ���Ѧh��?"
    };

    public AudioClip initialMoneyAudio;
    public List<AudioClip> answerOptionAudios;

    private List<int> currentQuestionSequenceIndices = new List<int>();

    // �l�ܥثe���B
    private int currentMoney = 100;

    [Header("���A���]�w")]
    public string serverUrl = "http://localhost:5000/recognize_speech";
    public float recordingDuration = 5.0f;

    private AudioClip recordingClip;

    void Start()
    {
        if (questionText == null)
        {
            Debug.LogError("�бN TextMeshPro (3D) �ե�즲�� Question Text ���I");
            return;
        }
        if (panelBackground == null)
        {
            Debug.LogError("�бN Panel �I���� GameObject �즲�� Panel Background ���I");
            return;
        }
        if (questionAudioSource == null)
        {
            Debug.LogError("�бN AudioSource �ե�즲�� Question Audio Source ���I");
            return;
        }
        if (initialMoneyAudio == null)
        {
            Debug.LogError("�Ь� '�{�b�A��100��' ���ѭ��W��� (Initial Money Audio)�I");
            return;
        }
        if (answerOptionAudios == null || answerOptionAudios.Count != answerOptions.Count)
        {
            Debug.LogError("�нT�O Answer Option Audios �C���� " + answerOptions.Count + " �ӭ��W���A�B�P�D�ض��Ǥ@�P�I");
            return;
        }

        panelBackground.SetActive(false);
        StartCoroutine(StartGameSequence());
    }

    IEnumerator StartGameSequence()
    {
        GenerateRandomQuestions();
        currentMoney = 100;
        panelBackground.SetActive(true);

        // 1. �B�z�T�w�D��
        questionText.text = initialMoneyQuestion;
        Debug.Log("����D��: " + initialMoneyQuestion);

        if (initialMoneyAudio != null)
        {
            questionAudioSource.clip = initialMoneyAudio;
            questionAudioSource.Play();
            yield return new WaitForSeconds(Mathf.Max(initialMoneyAudio.length, delayBetweenQuestions));
        }
        else
        {
            yield return new WaitForSeconds(delayBetweenQuestions);
        }

        // 2. �̧ǳB�z�C���H���D��
        for (int i = 0; i < currentQuestionSequenceIndices.Count; i++)
        {
            int questionListIndex = currentQuestionSequenceIndices[i];
            string currentQuestionText = answerOptions[questionListIndex];
            AudioClip currentQuestionAudio = answerOptionAudios[questionListIndex];

            questionText.text = currentQuestionText;
            Debug.Log("����D��: " + currentQuestionText);

            if (currentQuestionAudio != null)
            {
                questionAudioSource.clip = currentQuestionAudio;
                questionAudioSource.Play();
                yield return new WaitForSeconds(Mathf.Max(currentQuestionAudio.length, delayBetweenQuestions));
            }
            else
            {
                yield return new WaitForSeconds(delayBetweenQuestions);
            }

            // ����ק�: �ǻ���e�D�ت����� 'i' �� WaitForAnswer
            yield return StartCoroutine(WaitForAnswer(i));
        }

        Debug.Log("�Ҧ��D�ؤw��ܧ����I");
        questionText.text = "�Ҧ��D�ؤw��ܧ����I";
    }

    void GenerateRandomQuestions()
    {
        currentQuestionSequenceIndices = Enumerable.Range(0, answerOptions.Count)
                                         .OrderBy(x => System.Guid.NewGuid())
                                         .Take(3)
                                         .ToList();
    }

    // �����@�ӰѼơA�H�K���T�P�_�O���@�D�D��
    IEnumerator WaitForAnswer(int questionSequenceIndex)
    {
        Debug.Log("�л��X�A������...");
        questionText.text = "�л��X�A������...";

        if (Microphone.devices.Length > 0)
        {
            Debug.Log("�}�l����...");
            recordingClip = Microphone.Start(null, false, (int)recordingDuration, 44100);
            yield return new WaitForSeconds(recordingDuration);
            Microphone.End(null);
            Debug.Log("���������C");

            byte[] wavData = ConvertAudioClipToWav(recordingClip);
            // �ǻ��Ѽƨ� SendAudioToServer
            yield return StartCoroutine(SendAudioToServer(wavData, questionSequenceIndex));
        }
        else
        {
            Debug.LogError("�S�������J���]�ơI");
            questionText.text = "�S�������J���]�ơI";
            yield return new WaitForSeconds(2.0f);
        }
    }

    // �����@�ӰѼơA�H�K���T�P�_�O���@�D�D��
    IEnumerator SendAudioToServer(byte[] audioData, int questionSequenceIndex)
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
                // �ǻ��Ѽƨ� CheckAnswer
                CheckAnswer(response.transcription, questionSequenceIndex);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("�ѪR JSON ����: " + ex.Message);
                questionText.text = "���ѥ��ѡA�ЦA�դ@���C";
            }
        }
        else
        {
            Debug.LogError("�y�����ѽШD����: " + request.error);
            questionText.text = "�������~�Φ��A�����D�C";
        }
    }

    // �����@�ӰѼơA�H�K���T�P�_�O���@�D�D��
    void CheckAnswer(string userResponse, int questionSequenceIndex)
    {
        if (string.IsNullOrEmpty(userResponse))
        {
            Debug.Log("�S��ť��^���C");
            questionText.text = "�S��ť��^���C";
            return;
        }

        int questionListIndex = currentQuestionSequenceIndices[questionSequenceIndex];
        string question = answerOptions[questionListIndex];

        Match match = Regex.Match(question, @"��O(\d+)��");
        int spentMoney = 0;
        if (match.Success)
        {
            spentMoney = int.Parse(match.Groups[1].Value);
        }

        // ����ק�G�ϥ� currentMoney �Ӥ��O 100
        int correctAnswer = currentMoney - spentMoney;
        string correctAnswerStr = correctAnswer.ToString();

        string normalizedResponse = userResponse.Replace("�C", "").Replace("��", "").Trim();

        Debug.Log($"�A���F: \"{normalizedResponse}\"�A���T���׬O: \"{correctAnswerStr}\"");

        if (normalizedResponse == correctAnswerStr)
        {
            Debug.Log("���ץ��T�I");
            questionText.text = "���ץ��T�I";
            // ���ץ��T�ɡA��s currentMoney ���s��
            currentMoney = correctAnswer;
        }
        else
        {
            Debug.Log("���׿��~�I");
            questionText.text = $"���׿��~�C���T���׬O {correctAnswer}�C";
        }
    }

    // �N AudioClip �ഫ�� WAV �줸�հ}�C�����U�禡
    byte[] ConvertAudioClipToWav(AudioClip clip)
    {
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

//[System.Serializable]
//public class RecognitionResponse
//{
//    public string transcription;
//}