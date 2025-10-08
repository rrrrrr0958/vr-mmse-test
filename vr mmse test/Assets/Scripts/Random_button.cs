using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIButtonRandomizer : MonoBehaviour
{
    [Header("要隨機排列的動物按鈕")]
    public Button[] animalButtons;
    
    // 儲存原始位置
    private Vector3[] originalPositions;
    
    private void Start()
    {
        // 儲存所有按鈕的原始位置
        SaveOriginalPositions();
        RandomizeButtonPositions();
    }
    
    private void SaveOriginalPositions()
    {
        originalPositions = new Vector3[animalButtons.Length];
        for (int i = 0; i < animalButtons.Length; i++)
        {
            originalPositions[i] = animalButtons[i].transform.position;
        }
    }
    
    [ContextMenu("隨機排列按鈕")]
    public void RandomizeButtonPositions()
    {
        if (animalButtons.Length == 0)
        {
            Debug.LogWarning("請確保動物按鈕陣列已設定！");
            return;
        }
        
        if (originalPositions == null || originalPositions.Length == 0)
        {
            SaveOriginalPositions();
        }
        
        // 創建位置陣列的副本並打亂
        Vector3[] shuffledPositions = new Vector3[originalPositions.Length];
        System.Array.Copy(originalPositions, shuffledPositions, originalPositions.Length);
        
        // 使用Fisher-Yates演算法打亂位置
        for (int i = shuffledPositions.Length - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            Vector3 temp = shuffledPositions[i];
            shuffledPositions[i] = shuffledPositions[randomIndex];
            shuffledPositions[randomIndex] = temp;
        }
        
        // 將打亂後的位置指派給按鈕
        for (int i = 0; i < animalButtons.Length; i++)
        {
            animalButtons[i].transform.position = shuffledPositions[i];
        }
    }
    
    // 重置到原始位置
    [ContextMenu("重置按鈕位置")]
    public void ResetButtonPositions()
    {
        if (originalPositions != null)
        {
            for (int i = 0; i < animalButtons.Length && i < originalPositions.Length; i++)
            {
                animalButtons[i].transform.position = originalPositions[i];
            }
        }
    }
    
    // 當遊戲重新開始時呼叫
    public void OnGameRestart()
    {
        RandomizeButtonPositions();
    }
}