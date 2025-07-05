using UnityEngine;

public class FirstPersonIntroSetup : MonoBehaviour
{
    [Header("自動設置選項")]
    public bool autoSetupOnStart = true;
    public Vector3 cameraStartPosition = new Vector3(0, 1.7f, -10f);
    
    void Start()
    {
        if (autoSetupOnStart)
        {
            SetupFirstPersonIntro();
        }
    }
    
    [ContextMenu("設置第一人稱 Intro")]
    public void SetupFirstPersonIntro()
    {
        // 檢查是否已有主攝影機
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            // 創建新的攝影機
            GameObject cameraObj = new GameObject("Main Camera");
            mainCamera = cameraObj.AddComponent<Camera>();
            cameraObj.tag = "MainCamera";
            
            // 添加 Audio Listener
            cameraObj.AddComponent<AudioListener>();
        }
        
        // 設置攝影機位置
        mainCamera.transform.position = cameraStartPosition;
        
        // 添加 FirstPersonIntroController
        FirstPersonIntroController introController = mainCamera.GetComponent<FirstPersonIntroController>();
        if (introController == null)
        {
            introController = mainCamera.gameObject.AddComponent<FirstPersonIntroController>();
        }
        
        // 尋找 Zoo_Entrance
        GameObject zooEntrance = GameObject.Find("Zoo_Entrance");
        if (zooEntrance != null)
        {
            introController.zooEntrance = zooEntrance.transform;
            Debug.Log("已找到並設置 Zoo_Entrance");
        }
        else
        {
            Debug.LogWarning("找不到 Zoo_Entrance，請手動指定或創建一個名為 Zoo_Entrance 的物件");
        }
        
        Debug.Log("第一人稱 Intro 設置完成！");
    }
}