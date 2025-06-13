using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro; // TextMeshPro 的命名空間，必須保留！

public class GameManager : MonoBehaviour
{
    public float initialTextDelay = 3f; // 文字出現的延遲時間 (秒)
    public float questionBroadcastDelay = 2f; // 文字出現後，問題廣播的延遲時間 (秒)

    // 現在廣播問題的文字物件也改為 TextMeshPro 類型 (3D 版本)
    public TMPro.TextMeshPro questionBroadcastTextMeshPro;
    // public TextMesh questionBroadcast3DText; // <-- 這行現在必須註釋掉或刪除

    private GameObject[] stallNameTextObjects; // 存放所有攤位名稱文字物件的 GameObject
    private List<string> stallNames = new List<string>(); // 存放所有攤位名稱的字符串
    private string currentQuestionStallName; // 當前問題所指向的攤位名稱

    void Start()
    {
        Debug.Log("GameManager Start() called.");

        // 在遊戲開始時，隱藏所有攤位名稱文字並收集名稱
        HideAllStallNames();

        // 隱藏廣播文字 (確保預設是隱藏的)
        if (questionBroadcastTextMeshPro != null)
        {
            questionBroadcastTextMeshPro.gameObject.SetActive(false);
            Debug.Log("QuestionBroadcastText (TextMeshPro) set to inactive.");
        }
        else
        {
            Debug.LogError("QuestionBroadcastTextMeshPro (3D) is NOT assigned in the Inspector! Please assign your QuestionBroadcastText object.");
        }

        // 3秒後顯示攤位文字
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
            textObject.SetActive(false); // 直接禁用整個 GameObject，確保初始隱藏
            Debug.Log($"Disabled stall text object: {textObject.name}");

            // 嘗試獲取 TextMeshPro 組件 (因為攤位文字是 TextMeshPro 3D)
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
        yield return new WaitForSeconds(delay); // 等待指定秒數

        // 顯示所有攤位名稱文字
        foreach (GameObject textObject in stallNameTextObjects)
        {
            textObject.SetActive(true); // 啟用整個 GameObject
            Debug.Log($"Enabled stall text: {textObject.name}.");
        }

        // 在文字顯示後，延遲一段時間廣播問題
        StartCoroutine(BroadcastQuestionAfterDelay(questionBroadcastDelay));
    }

    IEnumerator BroadcastQuestionAfterDelay(float delay)
    {
        Debug.Log("BroadcastQuestionAfterDelay started.");
        yield return new WaitForSeconds(delay);

        // 隨機選擇一個攤位名稱
        if (stallNames.Count > 0)
        {
            int randomIndex = Random.Range(0, stallNames.Count);
            currentQuestionStallName = stallNames[randomIndex]; // 儲存當前問題的攤位名稱

            string question = "請點選" + currentQuestionStallName + "攤位！";
            Debug.Log($"Broadcasting question: {question}");

            // 現在廣播文字也使用 TextMeshPro 類型
            if (questionBroadcastTextMeshPro != null)
            {
                questionBroadcastTextMeshPro.text = question;
                questionBroadcastTextMeshPro.gameObject.SetActive(true); // 啟用 TextMeshPro GameObject
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
            // 如果沒有收集到攤位名字，廣播文字也無法顯示
            if (questionBroadcastTextMeshPro != null)
            {
                questionBroadcastTextMeshPro.text = "無法載入題目，請檢查攤位設定。";
                questionBroadcastTextMeshPro.gameObject.SetActive(true);
            }
        }
    }

    // 監聽滑鼠點擊
    void Update()
    {
        if (Input.GetMouseButtonDown(0)) // 檢查滑鼠左鍵是否按下
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition); // 從滑鼠位置發射射線
            RaycastHit hit;

            // 射線檢測，只檢測帶有 "StallNameText" 標籤的物件
            if (Physics.Raycast(ray, out hit) && hit.collider.CompareTag("StallNameText"))
            {
                // 因為攤位文字是 TextMeshPro (3D)，所以嘗試獲取 TMPro.TextMeshPro 組件
                TMPro.TextMeshPro clickedTextMeshPro3D = hit.collider.GetComponent<TMPro.TextMeshPro>();

                string clickedStallName = null;

                if (clickedTextMeshPro3D != null)
                {
                    clickedStallName = clickedTextMeshPro3D.text;
                    Debug.Log($"Clicked TextMeshPro 3D object: {hit.collider.name} with text: {clickedStallName}");
                }
                else
                {
                    Debug.LogWarning($"點擊的物件 '{hit.collider.name}' 有 'StallNameText' 標籤，但沒有 TextMeshPro (3D) 組件 (預期為攤位文字)。");
                }

                if (clickedStallName != null)
                {
                    if (clickedStallName == currentQuestionStallName)
                    {
                        Debug.Log("恭喜！點擊正確！");
                        // 更新廣播文字 (使用 TextMeshPro)
                        if (questionBroadcastTextMeshPro != null)
                        {
                            questionBroadcastTextMeshPro.text = "恭喜！點擊正確！";
                        }
                    }
                    else
                    {
                        Debug.Log("點擊錯誤，請再試一次。");
                        // 更新廣播文字 (使用 TextMeshPro)
                        if (questionBroadcastTextMeshPro != null)
                        {
                            questionBroadcastTextMeshPro.text = "點擊錯誤，請再試一次。";
                        }
                    }
                }
            }
        }
    }
}