using UnityEngine;
using System.Collections;

public class ButtonClickTester : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(TestButtonClicks());
    }

    IEnumerator TestButtonClicks()
    {
        yield return new WaitForSeconds(2f);

        Debug.Log("=== 開始測試按鈕點擊 ===");

        // 找所有有 AnimalButtonScript 的物件，直接呼叫它的 OnButtonClick()
        AnimalButtonScript[] buttons = FindObjectsOfType<AnimalButtonScript>();
        foreach (var buttonScript in buttons)
        {
            buttonScript.OnButtonClick();
            yield return new WaitForSeconds(0.2f);
        }

        Debug.Log("=== 測試完成，顯示當前點擊序列 ===");

        if (GameManager.instance != null)
        {
            var seq = GameManager.instance.ClickedAnimalSequence; // ★ 使用屬性
            Debug.Log($"點擊序列長度: {seq.Count}");
            for (int i = 0; i < seq.Count; i++)
            {
                Debug.Log($"第 {i + 1} 次點擊: {seq[i]}");
            }

            // JSON（GameManager.ConvertGameDataToJson 有預設 playerId/accuracy/timeUsed 才可這樣呼叫）
            string json = GameManager.instance.ConvertGameDataToJson();
            Debug.Log("JSON 測試完成！\n" + json);
        }
        else
        {
            Debug.LogError("找不到 GameManager！");
        }
    }
}
