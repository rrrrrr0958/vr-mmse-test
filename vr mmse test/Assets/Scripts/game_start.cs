using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro; // TextMeshPro 的命名空間，必須保留！

public class GameManager : MonoBehaviour
{
    public float initialTextDelay = 3f; // 文字出現的延遲時間 (秒)
    public float questionBroadcastDelay = 2f; // 文字出現後，問題廣播的延遲時間 (秒)

    // 廣播問題的文字物件，現在確定是 TextMeshPro (3D 版本)
    public TMPro.TextMeshPro questionBroadcastTextMeshPro;

    private GameObject[] stallClickableRoots; // 存放所有攤位根物件 (帶有 Collider 和 Tag)
    private List<string> stallNames = new List<string>(); // 存放所有攤位名稱的字符串
    private string currentQuestionStallName; // 當前問題所指向的攤位名稱

    void Start()
    {
        Debug.Log("GameManager Start() called.");

        HideAllStallNames(); // 處理根物件的隱藏和名稱收集

        if (questionBroadcastTextMeshPro != null)
        {
            questionBroadcastTextMeshPro.gameObject.SetActive(false);
            Debug.Log("QuestionBroadcastText (TextMeshPro) set to inactive.");
        }
        else
        {
            Debug.LogError("QuestionBroadcastTextMeshPro (3D) is NOT assigned in the Inspector! Please assign your QuestionBroadcastText object.");
        }

        StartCoroutine(ShowTextsAfterDelay(initialTextDelay));
    }

    void HideAllStallNames()
    {
        Debug.Log("HideAllStallNames is called.");
        stallClickableRoots = GameObject.FindGameObjectsWithTag("StallNameText");
        Debug.Log("Found " + stallClickableRoots.Length + " stall clickable root objects by tag.");

        if (stallClickableRoots.Length == 0)
        {
            Debug.LogWarning("No GameObjects with 'StallNameText' tag found. Please ensure your stall root objects have this tag.");
        }

        foreach (GameObject rootObject in stallClickableRoots) // 遍歷這些根物件
        {
            rootObject.SetActive(false); // 禁用整個根物件，其下的子物件也會被禁用
            Debug.Log($"Disabled stall root object: {rootObject.name}");

            // 從根物件的子物件中查找 TextMeshPro 組件 (用於收集名稱)
            TMPro.TextMeshPro tmpro = rootObject.GetComponentInChildren<TMPro.TextMeshPro>();

            if (tmpro != null)
            {
                stallNames.Add(tmpro.text);
                Debug.Log($"Collected stall name: {tmpro.text} from {rootObject.name}'s child (TextMeshPro 3D).");
            }
            else
            {
                // 這種情況通常意味著你把 TextMeshPro 組件放在了子物件上，但沒找到
                Debug.LogWarning($"Root object '{rootObject.name}' has 'StallNameText' tag but no TextMeshPro (3D) component found in its children. Please check its hierarchy.");
            }
        }
        Debug.Log($"Total stall names collected: {stallNames.Count}");
    }

    IEnumerator ShowTextsAfterDelay(float delay)
    {
        Debug.Log("ShowTextsAfterDelay started.");
        yield return new WaitForSeconds(delay); // 等待指定秒數

        foreach (GameObject rootObject in stallClickableRoots) // 遍歷根物件
        {
            rootObject.SetActive(true); // 啟用整個根物件
            Debug.Log($"Enabled stall root: {rootObject.name}.");
        }

        StartCoroutine(BroadcastQuestionAfterDelay(questionBroadcastDelay));
    }

    IEnumerator BroadcastQuestionAfterDelay(float delay)
    {
        Debug.Log("BroadcastQuestionAfterDelay started.");
        yield return new WaitForSeconds(delay);

        if (stallNames.Count > 0)
        {
            int randomIndex = Random.Range(0, stallNames.Count);
            currentQuestionStallName = stallNames[randomIndex];

            string question = "請點選" + currentQuestionStallName + "攤位！";
            Debug.Log($"Broadcasting question: {question}");

            if (questionBroadcastTextMeshPro != null)
            {
                questionBroadcastTextMeshPro.text = question;
                questionBroadcastTextMeshPro.gameObject.SetActive(true);
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
            if (questionBroadcastTextMeshPro != null)
            {
                questionBroadcastTextMeshPro.text = "無法載入題目，請檢查攤位設定。";
                questionBroadcastTextMeshPro.gameObject.SetActive(true);
            }
        }
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0)) // 檢查滑鼠左鍵是否按下
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition); // 從滑鼠位置發射射線
            RaycastHit hit;

            // ***** 新增 LayerMask *****
            int stallLayerMask = 1 << LayerMask.NameToLayer("StallLayer"); // 確保 "StallLayer" 與 Unity 中的名稱完全一致
                                                                           // ************************

            // 將 LayerMask 作為第四個參數傳入 Raycast
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, stallLayerMask))
            {
                Debug.Log($"Raycast hit object: {hit.collider.name} (Tag: {hit.collider.tag})");

                // 檢查被点击的物体是否有 "StallNameText" 标签
                if (hit.collider.CompareTag("StallNameText"))
                {
                    Debug.Log($"Hit object '{hit.collider.name}' has 'StallNameText' tag. Attempting to get TextMeshPro component from its children...");

                    // 從被点击的 Collider 所属的 GameObject (现在是根物件) 的子物件中获取 TextMeshPro 组件
                    TMPro.TextMeshPro clickedTextMeshPro3D = hit.collider.gameObject.GetComponentInChildren<TMPro.TextMeshPro>();

                    string clickedStallName = null;

                    if (clickedTextMeshPro3D != null)
                    {
                        clickedStallName = clickedTextMeshPro3D.text;
                        Debug.Log($"Successfully got TextMeshPro component from child of {hit.collider.name}. Text: {clickedStallName}");
                    }
                    else
                    {
                        // 如果 hit.collider.gameObject 有 StallNameText 標籤，但找不到 TextMeshPro 組件
                        Debug.LogWarning($"點擊的物件 '{hit.collider.name}' 有 'StallNameText' 標籤，但**沒有在其自身或子物件中找到 TextMeshPro (3D) 組件**。請檢查其層級結構或確認 TextMeshPro 組件類型是否正確。");
                    }

                    if (clickedStallName != null)
                    {
                        if (clickedStallName == currentQuestionStallName)
                        {
                            Debug.Log("恭喜！點擊正確！");
                            if (questionBroadcastTextMeshPro != null)
                            {
                                questionBroadcastTextMeshPro.text = "恭喜！點擊正確！";
                            }
                        }
                        else
                        {
                            Debug.Log("點擊錯誤，請再試一次。");
                            if (questionBroadcastTextMeshPro != null)
                            {
                                questionBroadcastTextMeshPro.text = "點擊錯誤，請再試一次。";
                            }
                        }
                    }
                }
                else
                {
                    // 這條日誌現在只會在 Raycast 命中了 StallLayer 上的物件，但該物件沒有 StallNameText 標籤時出現。
                    Debug.LogWarning($"Hit object '{hit.collider.name}' does NOT have 'StallNameText' tag. **這通常表示你的 Raycast 命中了 StallLayer 中不該被判定的物件。**");
                }
            }
            else
            {
                // 這條日誌表示 Raycast 沒有擊中任何 StallLayer 上的物件
                Debug.Log("Raycast hit nothing on StallLayer.");
            }
        }
    }
}