using UnityEngine;
using System.Collections;

public class AnimalButtonTestRunner : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(TestAnimalButtons());
    }
    
    IEnumerator TestAnimalButtons()
    {
        yield return new WaitForSeconds(2f); // 等待系統初始化
        
        Debug.Log("=== 開始測試動物按鈕 ===");
        
        // 找到 AnimalSelectionManager
        AnimalSelectionManager manager = FindObjectOfType<AnimalSelectionManager>();
        if (manager != null)
        {
            // 模擬點擊每個動物按鈕
            string[] animals = {"兔子", "老虎", "水牛", "熊貓", "鹿", "狼"};
            
            foreach (string animal in animals)
            {
                manager.OnAnimalSelected(animal);
                yield return new WaitForSeconds(0.5f);
            }
            
            Debug.Log("=== 動物按鈕測試完成 ===");
            
            // 顯示結果
            if (GameManager.instance != null)
            {
                Debug.Log($"總共記錄了 {GameManager.instance.clickedAnimalSequence.Count} 個動物選擇");
                for (int i = 0; i < GameManager.instance.clickedAnimalSequence.Count; i++)
                {
                    Debug.Log($"第 {i + 1} 個選擇: {GameManager.instance.clickedAnimalSequence[i]}");
                }
            }
        }
        else
        {
            Debug.LogError("找不到 AnimalSelectionManager！");
        }
    }
}