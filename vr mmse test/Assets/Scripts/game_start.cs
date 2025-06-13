using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro; // TextMeshPro ���R�W�Ŷ��A�����O�d�I

public class GameManager : MonoBehaviour
{
    public float initialTextDelay = 3f; // ��r�X�{������ɶ� (��)
    public float questionBroadcastDelay = 2f; // ��r�X�{��A���D�s��������ɶ� (��)

    // �{�b�s�����D����r����]�אּ TextMeshPro ���� (3D ����)
    public TMPro.TextMeshPro questionBroadcastTextMeshPro;
    // public TextMesh questionBroadcast3DText; // <-- �o��{�b�����������ΧR��

    private GameObject[] stallNameTextObjects; // �s��Ҧ��u��W�٤�r���� GameObject
    private List<string> stallNames = new List<string>(); // �s��Ҧ��u��W�٪��r�Ŧ�
    private string currentQuestionStallName; // ��e���D�ҫ��V���u��W��

    void Start()
    {
        Debug.Log("GameManager Start() called.");

        // �b�C���}�l�ɡA���éҦ��u��W�٤�r�æ����W��
        HideAllStallNames();

        // ���üs����r (�T�O�w�]�O���ê�)
        if (questionBroadcastTextMeshPro != null)
        {
            questionBroadcastTextMeshPro.gameObject.SetActive(false);
            Debug.Log("QuestionBroadcastText (TextMeshPro) set to inactive.");
        }
        else
        {
            Debug.LogError("QuestionBroadcastTextMeshPro (3D) is NOT assigned in the Inspector! Please assign your QuestionBroadcastText object.");
        }

        // 3�������u���r
        StartCoroutine(ShowTextsAfterDelay(initialTextDelay));
    }

    void HideAllStallNames()
    {
        Debug.Log("HideAllStallNames is called.");
        stallNameTextObjects = GameObject.FindGameObjectsWithTag("StallNameText");
        Debug.Log("Found " + stallNameTextObjects.Length + " stall name text objects by tag.");

        if (stallNameTextObjects.Length == 0)
        {
            Debug.LogWarning("No GameObjects with 'StallNameText' tag found. Please ensure your text objects have this tag.");
        }

        foreach (GameObject textObject in stallNameTextObjects)
        {
            textObject.SetActive(false); // �����T�ξ�� GameObject�A�T�O��l����
            Debug.Log($"Disabled stall text object: {textObject.name}");

            // ������� TextMeshPro �ե� (�]���u���r�O TextMeshPro 3D)
            TMPro.TextMeshPro tmpro = textObject.GetComponent<TMPro.TextMeshPro>();

            if (tmpro != null)
            {
                stallNames.Add(tmpro.text);
                Debug.Log($"Collected stall name: {tmpro.text} from {textObject.name} (TextMeshPro 3D).");
            }
            else
            {
                Debug.LogWarning($"Found GameObject '{textObject.name}' with 'StallNameText' tag but no TextMeshPro (3D) component. Please check its components.");
            }
        }
        Debug.Log($"Total stall names collected: {stallNames.Count}");
    }

    IEnumerator ShowTextsAfterDelay(float delay)
    {
        Debug.Log("ShowTextsAfterDelay started.");
        yield return new WaitForSeconds(delay); // ���ݫ��w���

        // ��ܩҦ��u��W�٤�r
        foreach (GameObject textObject in stallNameTextObjects)
        {
            textObject.SetActive(true); // �ҥξ�� GameObject
            Debug.Log($"Enabled stall text: {textObject.name}.");
        }

        // �b��r��ܫ�A����@�q�ɶ��s�����D
        StartCoroutine(BroadcastQuestionAfterDelay(questionBroadcastDelay));
    }

    IEnumerator BroadcastQuestionAfterDelay(float delay)
    {
        Debug.Log("BroadcastQuestionAfterDelay started.");
        yield return new WaitForSeconds(delay);

        // �H����ܤ@���u��W��
        if (stallNames.Count > 0)
        {
            int randomIndex = Random.Range(0, stallNames.Count);
            currentQuestionStallName = stallNames[randomIndex]; // �x�s��e���D���u��W��

            string question = "���I��" + currentQuestionStallName + "�u��I";
            Debug.Log($"Broadcasting question: {question}");

            // �{�b�s����r�]�ϥ� TextMeshPro ����
            if (questionBroadcastTextMeshPro != null)
            {
                questionBroadcastTextMeshPro.text = question;
                questionBroadcastTextMeshPro.gameObject.SetActive(true); // �ҥ� TextMeshPro GameObject
                Debug.Log("Broadcast text (TextMeshPro) enabled and set.");
            }
            else
            {
                Debug.LogError("Broadcast Text (TextMeshPro) object is NOT assigned in the Inspector for broadcasting!");
            }
        }
        else
        {
            Debug.LogError("No stall names collected for broadcasting. Check 'StallNameText' tags and correct TextMeshPro components for stall names.");
            // �p�G�S���������u��W�r�A�s����r�]�L�k���
            if (questionBroadcastTextMeshPro != null)
            {
                questionBroadcastTextMeshPro.text = "�L�k���J�D�ءA���ˬd�u��]�w�C";
                questionBroadcastTextMeshPro.gameObject.SetActive(true);
            }
        }
    }

    // ��ť�ƹ��I��
    void Update()
    {
        if (Input.GetMouseButtonDown(0)) // �ˬd�ƹ�����O�_���U
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition); // �q�ƹ���m�o�g�g�u
            RaycastHit hit;

            // �g�u�˴��A�u�˴��a�� "StallNameText" ���Ҫ�����
            if (Physics.Raycast(ray, out hit) && hit.collider.CompareTag("StallNameText"))
            {
                // �]���u���r�O TextMeshPro (3D)�A�ҥH������� TMPro.TextMeshPro �ե�
                TMPro.TextMeshPro clickedTextMeshPro3D = hit.collider.GetComponent<TMPro.TextMeshPro>();

                string clickedStallName = null;

                if (clickedTextMeshPro3D != null)
                {
                    clickedStallName = clickedTextMeshPro3D.text;
                    Debug.Log($"Clicked TextMeshPro 3D object: {hit.collider.name} with text: {clickedStallName}");
                }
                else
                {
                    Debug.LogWarning($"�I�������� '{hit.collider.name}' �� 'StallNameText' ���ҡA���S�� TextMeshPro (3D) �ե� (�w�����u���r)�C");
                }

                if (clickedStallName != null)
                {
                    if (clickedStallName == currentQuestionStallName)
                    {
                        Debug.Log("���ߡI�I�����T�I");
                        // ��s�s����r (�ϥ� TextMeshPro)
                        if (questionBroadcastTextMeshPro != null)
                        {
                            questionBroadcastTextMeshPro.text = "���ߡI�I�����T�I";
                        }
                    }
                    else
                    {
                        Debug.Log("�I�����~�A�ЦA�դ@���C");
                        // ��s�s����r (�ϥ� TextMeshPro)
                        if (questionBroadcastTextMeshPro != null)
                        {
                            questionBroadcastTextMeshPro.text = "�I�����~�A�ЦA�դ@���C";
                        }
                    }
                }
            }
        }
    }
}