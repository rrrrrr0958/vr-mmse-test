using UnityEngine;
using TMPro; // �ޤJTextMeshPro�R�W�Ŷ�
using System.Collections; // �ޤJ�Ω��{���R�W�Ŷ�
using System.Collections.Generic; // �ޤJ�Ω�List���R�W�Ŷ�
using System.Linq; // �ޤJ�Ω�LINQ�A��K�H�����

public class QuestionManager : MonoBehaviour
{
    public TextMeshPro questionText; // �{�b�O TextMeshPro (3D)
    public GameObject panelBackground; // �I���i�H�O 3D Quad �� Plane
    public float delayBetweenQuestions = 3.0f; // �C�D����������ɶ�

    private string initialMoneyQuestion = "�{�b�A��100��";
    private List<string> answerOptions = new List<string>
    {
        "��O25���R�F������Ѧh��?",
        "��O7���R�F�ѥ]����Ѧh��?",
        "��O35���R�F���G����Ѧh��?",
        "��O15���R�F�Z������Ѧh��?",
        "��O30���R�F�פ���Ѧh��?"
    };

    private List<string> currentQuestionSequence = new List<string>(); // �x�s�����C�����H���D�اǦC
    private int currentQuestionIndex = 0; // ��e�D�ت�����

    // �o�̻ݭn�A���y����J�t�Ϊ��ѦҡC
    // �ѩ�Unity���ت��y���ѧO�\�঳���A�A�i��ݭn��X�ĤT�贡��A
    // �Ҧp�GWindows Speech Recognition (PC), iOS Speech Framework (iOS) �� Google Cloud Speech-to-Text�C
    // �Ȯɧڭ̥Τ@�Ӽ������y����J�Ӫ�ܡC
    // public YourSpeechRecognitionSystem speechRecognizer; // ���]�A��X���y���ѧO�t��

    void Start()
    {
        if (questionText == null)
        {
            Debug.LogError("�бNTextMeshProUGUI�ե�즲��Question Text���I");
            return;
        }
        if (panelBackground == null)
        {
            Debug.LogError("�бNPanel�I����GameObject�즲��Panel Background���I");
            return;
        }

        panelBackground.SetActive(false); // ��l�����í��O�M��r

        // �o�̥i�H��l�ƧA���y���ѧO�t��
        // if (speechRecognizer != null)
        // {
        //     speechRecognizer.OnSpeechRecognized += HandleSpeechInput;
        // }

        StartCoroutine(StartGameSequence());
    }

    IEnumerator StartGameSequence()
    {
        GenerateRandomQuestions();
        yield return StartCoroutine(DisplayNextQuestion()); // ����ܲĤ@�q�T�w��r
    }

    void GenerateRandomQuestions()
    {
        // �q answerOptions ���H�����3���D��
        // �ϥ�LINQ��OrderBy(Guid.NewGuid())�i���H���ƧǡA�M����e3��
        currentQuestionSequence = answerOptions.OrderBy(x => System.Guid.NewGuid()).Take(3).ToList();

        // ��X�H�����ǡA�Ω�ո�
        Debug.Log("�H���D�ض��ǡG");
        foreach (var q in currentQuestionSequence)
        {
            Debug.Log(q);
        }
    }

    IEnumerator DisplayNextQuestion()
    {
        panelBackground.SetActive(true); // ��ܭ��O�M��r

        if (currentQuestionIndex == 0)
        {
            // ����ܩT�w�D��
            questionText.text = initialMoneyQuestion;
            Debug.Log("����D��: " + initialMoneyQuestion);
            yield return new WaitForSeconds(delayBetweenQuestions); // ���d�@�q�ɶ�
        }

        if (currentQuestionIndex < currentQuestionSequence.Count)
        {
            questionText.text = currentQuestionSequence[currentQuestionIndex];
            Debug.Log("����D��: " + currentQuestionSequence[currentQuestionIndex]);

            // �b�o�̵��ݻy����J
            // ��ڱ��p�U�A�A�ݭn�@�Ӿ���ӱ����y����J��Ĳ�o�U�@�D
            // yield return StartCoroutine(WaitForSpeechInput()); // ���]���o�Ө�{�ӵ��ݻy����J

            yield return new WaitForSeconds(delayBetweenQuestions); // �ȮɥΩ���������ݻy����J�ɶ�

            currentQuestionIndex++;

            // �p�G�٦��D�ءA�~����ܤU�@�D
            if (currentQuestionIndex <= currentQuestionSequence.Count)
            {
                yield return StartCoroutine(DisplayNextQuestion());
            }
            else
            {
                Debug.Log("�Ҧ��D�ؤw��ܧ����I");
                questionText.text = "�Ҧ��D�ؤw��ܧ����I";
                // panelBackground.SetActive(false); // �i�H��̫ܳ�����
            }
        }
        else
        {
            Debug.Log("�Ҧ��D�ؤw��ܧ����I");
            questionText.text = "�Ҧ��D�ؤw��ܧ����I";
            // panelBackground.SetActive(false); // �i�H��̫ܳ�����
        }
    }

    // �����y����J�B�z�A��ڻݭn�P�A���y���ѧO�t�ξ�X
    // public void HandleSpeechInput(string recognizedText)
    // {
    //     Debug.Log("�ѧO�쪺�y��: " + recognizedText);
    //     // �b�o�̧A�i�H�B�z�y����J�A�Ҧp�ˬd���׬O�_���T
    //     // �M��ھڵ��רM�w�O�_�i�J�U�@�D
    //     // �p�G���ץ��T�A�i�H�I�s StopCoroutine(WaitForSpeechInput()) �ñҰ� DisplayNextQuestion()
    // }
}