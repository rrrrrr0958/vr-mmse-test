using UnityEngine;
using TMPro; // �ޤJTextMeshPro�R�W�Ŷ�
using System.Collections; // �ޤJ�Ω��{���R�W�Ŷ�
using System.Collections.Generic; // �ޤJ�Ω�List���R�W�Ŷ�
using System.Linq; // �ޤJ�Ω�LINQ�A��K�H�����

public class QuestionManager : MonoBehaviour
{
    public TextMeshPro questionText; // �Ω���ܤ�r�� TextMeshPro (3D)
    public GameObject panelBackground; // ���D�I�����O�� GameObject (3D Quad �� Plane)
    public float delayBetweenQuestions = 3.0f; // �C�D����������ɶ�

    public AudioSource questionAudioSource; // �Ω󼽩��D�ػy���� AudioSource

    // �Ҧ����D�ؤ�r�������b Script ���w�q
    private string initialMoneyQuestion = "�{�b�A��100��";
    private List<string> answerOptions = new List<string>
    {
        "��O25���R�F������Ѧh��?",
        "��O7���R�F�ѥ]����Ѧh��?",
        "��O35���R�F���G����Ѧh��?",
        "��O15���R�F�Z������Ѧh��?",
        "��O30���R�F�פ���Ѧh��?"
    };

    // �Ҧ��D�ع��������W���q�A�ݭn�b Inspector ���s��
    public AudioClip initialMoneyAudio; // "�{�b�A��100��" �����W
    public List<AudioClip> answerOptionAudios; // a-e �D�ت����W�C��

    private List<int> currentQuestionSequenceIndices = new List<int>(); // �x�s�H����ܪ��D�ئb answerOptions��������
    private int currentQuestionIndexInSequence = 0; // ��e�b�H���ǦC�����D�د���

    void Start()
    {
        // �ˬd���n���ե�O�_�w�s��
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

        // �ˬd���W���O�_�w�s��
        if (initialMoneyAudio == null)
        {
            Debug.LogError("�Ь� '�{�b�A��100��' ���ѭ��W��� (Initial Money Audio)�I");
            return;
        }
        // �T�O���W�C���ƶq�P�D�ؤ�r�C���ƶq�@�P
        if (answerOptionAudios == null || answerOptionAudios.Count != answerOptions.Count)
        {
            Debug.LogError("�нT�O Answer Option Audios �C���� " + answerOptions.Count + " �ӭ��W���A�B�P�D�ض��Ǥ@�P�I");
            return;
        }

        panelBackground.SetActive(false); // ��l�����í��O�M��r

        StartCoroutine(StartGameSequence());
    }

    IEnumerator StartGameSequence()
    {
        GenerateRandomQuestions();
        yield return StartCoroutine(DisplayAndPlayQuestion()); // �Ұ���ܩM����y������{
    }

    void GenerateRandomQuestions()
    {
        // �q 0 �� answerOptions.Count-1 �����ޤ��H�����3��
        // �o�˥i�H�T�O�H����ܪ��D�ع����쥿�T�����W����
        currentQuestionSequenceIndices = Enumerable.Range(0, answerOptions.Count)
                                         .OrderBy(x => System.Guid.NewGuid())
                                         .Take(3)
                                         .ToList();

        // ��X�H�����ǡA�Ω�ո�
        Debug.Log("�H���D�ض��� (����)�G");
        foreach (var index in currentQuestionSequenceIndices)
        {
            Debug.Log($"�D��: {answerOptions[index]}, ���W: {answerOptionAudios[index]?.name}");
        }
    }

    IEnumerator DisplayAndPlayQuestion()
    {
        panelBackground.SetActive(true); // ��ܭ��O�M��r

        // �B�z�T�w�D�� "�{�b�A��100��"
        if (currentQuestionIndexInSequence == 0)
        {
            questionText.text = initialMoneyQuestion; // ��ܩT�w��r
            Debug.Log("����D��: " + initialMoneyQuestion);

            if (initialMoneyAudio != null)
            {
                questionAudioSource.clip = initialMoneyAudio;
                questionAudioSource.Play();
                // ���ݭ��W���񧹲��A�Φܤ� delayBetweenQuestions �ɶ�
                yield return new WaitForSeconds(Mathf.Max(initialMoneyAudio.length, delayBetweenQuestions));
            }
            else
            {
                yield return new WaitForSeconds(delayBetweenQuestions); // �p�G�S�����W�A�u����
            }
        }

        // �B�z�H����ܪ��D�� (a-e)
        // �`�N�o�̪��P�_����AcurrentQuestionIndexInSequence �w�g�]�t��l�D�ءA�ҥH�n�ˬd�O�_�p���H���ǦC���`���� + 1 (�]����l�D�ئ��Τ@�Ӷ��q)
        if (currentQuestionIndexInSequence < currentQuestionSequenceIndices.Count + 1)
        {
            // �p�G��e���ެO 0�A��ܤw�g��ܹL��l�D�ءA�{�b�n�}�l��ܲĤ@���H���D��
            // �p�G��e���ޤj�� 0�A��ܭn��ܲ� currentQuestionIndexInSequence ���H���D��
            int actualRandomQuestionIndex = currentQuestionIndexInSequence - 1; // ��1�]���H���D�رq����0�}�l�A�ӧڭ̪�sequence����0�w�g���F�T�w�D��

            if (actualRandomQuestionIndex >= 0 && actualRandomQuestionIndex < currentQuestionSequenceIndices.Count)
            {
                int questionListIndex = currentQuestionSequenceIndices[actualRandomQuestionIndex]; // ����H���襤�D�ت���l�C�����
                string currentQuestionText = answerOptions[questionListIndex]; // �����������r
                AudioClip currentQuestionAudio = answerOptionAudios[questionListIndex]; // ������������W

                questionText.text = currentQuestionText;
                Debug.Log("����D��: " + currentQuestionText);

                if (currentQuestionAudio != null)
                {
                    questionAudioSource.clip = currentQuestionAudio;
                    questionAudioSource.Play();
                    // ���ݭ��W���񧹲��A�Φܤ� delayBetweenQuestions �ɶ�
                    yield return new WaitForSeconds(Mathf.Max(currentQuestionAudio.length, delayBetweenQuestions));
                }
                else
                {
                    yield return new WaitForSeconds(delayBetweenQuestions); // �p�G�S�����W�A�u����
                }
            }

            currentQuestionIndexInSequence++; // �e�i��U�@���D�ء]�L�׬O�T�w�٬O�H���^

            // �p�G�٦��D�ػݭn��ܡ]�]�A�H���D�ةM��l�D�ء^�A�h�~�򻼰j�I�s
            // �`�@�|��� 1 (�T�w�D��) + 3 (�H���D��) = 4 �Ӷ��q
            if (currentQuestionIndexInSequence <= currentQuestionSequenceIndices.Count) // <= ����٨S��ܧ��Ҧ��H���D��
            {
                yield return StartCoroutine(DisplayAndPlayQuestion());
            }
            else
            {
                Debug.Log("�Ҧ��D�ؤw��ܧ����I");
                questionText.text = "�Ҧ��D�ؤw��ܧ����I";
                // panelBackground.SetActive(false); // �i�H��̫ܳ�����
            }
        }
        else // �Ҧ��D�س���ܧ����F
        {
            Debug.Log("�Ҧ��D�ؤw��ܧ����I");
            questionText.text = "�Ҧ��D�ؤw��ܧ����I";
            // panelBackground.SetActive(false); // �i�H��̫ܳ�����
        }
    }
}