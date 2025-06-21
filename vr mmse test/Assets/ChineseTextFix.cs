using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class ChineseTextFix : MonoBehaviour
{
    [Header("Text Components to Fix")]
    public Text statusText;
    public Text buttonText;
    
    [Header("Text Settings")]
    public string correctText = "歡迎來到動物園！";
    public string buttonCorrectText = "繼續遊戲";
    
    void Start()
    {
        // 自動尋找文字元件
        if (statusText == null)
            statusText = GameObject.Find("StatusText")?.GetComponent<Text>();
        
        if (buttonText == null)
            buttonText = GameObject.Find("ButtonText")?.GetComponent<Text>();
        
        // 等待一幀後修正文字
        Invoke("FixAllText", 0.1f);
    }
    
    void FixAllText()
    {
        // 修正主要狀態文字
        FixStatusText();
        
        // 修正按鈕文字
        FixButtonText();
        
        Debug.Log("Chinese text fix applied");
    }
    
    void FixStatusText()
    {
        if (statusText != null)
        {
            // 嘗試多種修正方法
            
            // 方法1：直接設定正確文字
            statusText.text = correctText;
            
            // 方法2：設定字體屬性
            statusText.fontSize = 36;
            statusText.color = Color.white;
            statusText.alignment = TextAnchor.MiddleCenter;
            statusText.lineSpacing = 1.0f;
            
            // 方法3：如果有 Arial Unicode MS 字體，使用它
            Font arialUnicode = Resources.FindObjectsOfTypeAll<Font>()
                .FirstOrDefault(f => f.name.Contains("Arial") && f.name.Contains("Unicode"));
            
            if (arialUnicode != null)
            {
                statusText.font = arialUnicode;
                Debug.Log("Applied Arial Unicode font");
            }
            
            // 方法4：強制重新渲染
            statusText.enabled = false;
            statusText.enabled = true;
            
            Debug.Log($"Status text set to: {statusText.text}");
        }
    }
    
    void FixButtonText()
    {
        if (buttonText != null)
        {
            buttonText.text = buttonCorrectText;
            buttonText.fontSize = 24;
            buttonText.color = Color.white;
            buttonText.alignment = TextAnchor.MiddleCenter;
            
            // 強制重新渲染
            buttonText.enabled = false;
            buttonText.enabled = true;
            
            Debug.Log($"Button text set to: {buttonText.text}");
        }
    }
    
    // 如果文字仍然顯示錯誤，嘗試反轉字串
    string ReverseString(string input)
    {
        char[] chars = input.ToCharArray();
        System.Array.Reverse(chars);
        return new string(chars);
    }
    
    // 手動修正按鈕
    [ContextMenu("Fix Text Now")]
    public void ManualFixText()
    {
        FixAllText();
    }
    
    // 嘗試反轉文字
    [ContextMenu("Try Reverse Text")]
    public void TryReverseText()
    {
        if (statusText != null)
        {
            string reversed = ReverseString(correctText);
            statusText.text = reversed;
            Debug.Log($"Tried reversed text: {reversed}");
        }
    }
    
    // 嘗試英文文字
    [ContextMenu("Try English Text")]
    public void TryEnglishText()
    {
        if (statusText != null)
        {
            statusText.text = "Welcome to the Zoo!";
            Debug.Log("Set to English text");
        }
    }
    
    void Update()
    {
        // 每秒檢查一次文字是否正確
        if (Time.frameCount % 60 == 0)
        {
            if (statusText != null && statusText.text != correctText)
            {
                Debug.Log($"Text mismatch detected. Current: '{statusText.text}', Expected: '{correctText}'");
                statusText.text = correctText;
            }
        }
    }
}