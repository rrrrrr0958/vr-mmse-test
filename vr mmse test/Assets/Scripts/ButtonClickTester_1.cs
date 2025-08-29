using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ButtonClickTester : MonoBehaviour
{
    void Start()
    {
        // 測試按鈕點擊功能
        StartCoroutine(TestButtonClicks());
    }
    
    IEnumerator TestButtonClicks()
    {
        // 等待 2 秒讓系統初始化
        yield return new WaitForSeconds(2f);
        
        Debug.Log("=== 開始測試按鈕點擊 ===");
        
        // 尋找所有按鈕並模擬點擊
        AnimalButtonScript[] buttons = FindObjectsOfType<AnimalButtonScript>();
        
        foreach (AnimalButtonScript buttonScript in buttons)
        {
            // 模擬點擊
            buttonScript.OnButtonClick();
            yield return new WaitForSeconds(0.5f); // 等待 0.5 秒
        }
        
        Debug.Log("=== 測試完成，顯示當前點擊序列 ===");
        
        // 顯示 GameManager 中的點擊序列
        if (GameManager.instance != null)
        {
            Debug.Log($"點擊序列長度: {GameManager.instance.clickedAnimalSequence.Count}");
            for (int i = 0; i < GameManager.instance.clickedAnimalSequence.Count; i++)
            {
                Debug.Log($"第 {i + 1} 次點擊: {GameManager.instance.clickedAnimalSequence[i]}");
            }
            
            // 測試 JSON 轉換
            string json = GameManager.instance.ConvertGameDataToJson();
            Debug.Log("JSON 測試完成！");
        }
        else
        {
            Debug.LogError("找不到 GameManager！");
        }
    }
}