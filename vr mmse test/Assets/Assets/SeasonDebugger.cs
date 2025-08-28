using UnityEngine;

public class SeasonDebugger : MonoBehaviour
{
    [Header("Debug Controls")]
    public KeyCode springKey = KeyCode.Alpha1;
    public KeyCode summerKey = KeyCode.Alpha2;
    public KeyCode autumnKey = KeyCode.Alpha3;
    public KeyCode winterKey = KeyCode.Alpha4;
    
    private SimpleTestManager testManager;
    private Renderer windowRenderer;
    
    void Start()
    {
        testManager = FindObjectOfType<SimpleTestManager>();
        
        GameObject windowObj = GameObject.Find("Window_Interactive");
        if (windowObj != null)
        {
            windowRenderer = windowObj.GetComponent<Renderer>();
        }
        
        CheckSetup();
        CreateTestMaterials();
    }
    
    void CreateTestMaterials()
    {
        if (testManager != null)
        {
            Material[] testMaterials = new Material[4];
            
            // 春天 - 綠色
            testMaterials[0] = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            testMaterials[0].color = new Color(0.5f, 1f, 0.5f);
            
            // 夏天 - 黃色
            testMaterials[1] = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            testMaterials[1].color = new Color(1f, 1f, 0.3f);
            
            // 秋天 - 橙色
            testMaterials[2] = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            testMaterials[2].color = new Color(1f, 0.5f, 0.2f);
            
            // 冬天 - 藍色
            testMaterials[3] = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            testMaterials[3].color = new Color(0.7f, 0.9f, 1f);
            
            testManager.seasonMaterials = testMaterials;
            testManager.sceneryRenderer = windowRenderer;
            
            Debug.Log("測試材質已創建並設置");
        }
    }
    
    void CheckSetup()
    {
        Debug.Log("=== 季節系統檢查 ===");
        
        if (testManager == null)
        {
            Debug.LogError("SimpleTestManager 未找到！");
        }
        
        if (windowRenderer == null)
        {
            Debug.LogError("Window_Interactive 的 Renderer 未找到！");
        }
        
        Debug.Log("使用按鍵 1,2,3,4 測試季節切換");
    }
    
    void Update()
    {
        if (Input.GetKeyDown(springKey))
        {
            TestSeason(0, "春天");
        }
        else if (Input.GetKeyDown(summerKey))
        {
            TestSeason(1, "夏天");
        }
        else if (Input.GetKeyDown(autumnKey))
        {
            TestSeason(2, "秋天");
        }
        else if (Input.GetKeyDown(winterKey))
        {
            TestSeason(3, "冬天");
        }
    }
    
    void TestSeason(int index, string name)
    {
        if (testManager != null && windowRenderer != null && 
            testManager.seasonMaterials != null && 
            index < testManager.seasonMaterials.Length)
        {
            windowRenderer.material = testManager.seasonMaterials[index];
            Debug.Log("切換到: " + name);
        }
    }
}