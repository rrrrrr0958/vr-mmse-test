using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class AnimalButtonTestRunner : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(TestAnimalButtons());
    }

    IEnumerator TestAnimalButtons()
    {
        yield return new WaitForSeconds(2f);

        Debug.Log("=== 開始測試動物按鈕 ===");

        // 直接找場景內所有 Button 來模擬點擊（或只找 Panel_1 底下的也可以）
        Button[] buttons = FindObjectsOfType<Button>();

        int clicked = 0;
        foreach (var btn in buttons)
        {
            // 只測試「動物按鈕」：有 AnimalButtonScript 的才點
            var abs = btn.GetComponent<AnimalButtonScript>();
            if (abs != null)
            {
                btn.onClick.Invoke();
                clicked++;
                yield return new WaitForSeconds(0.2f);
                if (clicked >= 6) break; // 隨便點幾個就好，避免選超過3個
            }
        }

        Debug.Log("=== 動物按鈕測試完成 ===");

        if (GameManager.instance != null)
        {
            var seq = GameManager.instance.ClickedAnimalSequence; // ★ 使用公開唯讀屬性
            Debug.Log($"總共記錄了 {seq.Count} 個動物選擇");
            for (int i = 0; i < seq.Count; i++)
            {
                Debug.Log($"第 {i + 1} 個選擇: {seq[i]}");
            }
        }
        else
        {
            Debug.LogError("找不到 GameManager！");
        }
    }
}
