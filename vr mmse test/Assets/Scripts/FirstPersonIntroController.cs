using UnityEngine;

public class FirstPersonIntroController : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform zooEntrance; // 拖曳 Zoo_Entrance 到這裡
    public float moveDuration = 10f; // 移動時間
    
    [Header("Camera Settings")]
    public float cameraHeight = 1.7f; // 第一人稱視角高度
    public float lookAheadDistance = 5f; // 視線前方距離
    
    [Header("Control Settings")]
    public bool disableOtherControlsOnStart = true; // 開始時禁用其他控制
    
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private float elapsedTime = 0f;
    private bool isMoving = false;
    
    void Start()
    {
        // 禁用其他可能的控制器
        if (disableOtherControlsOnStart)
        {
            DisableOtherControls();
        }
        
        // 設定攝影機初始高度
        Vector3 currentPos = transform.position;
        transform.position = new Vector3(currentPos.x, cameraHeight, currentPos.z);
        
        // 嘗試自動尋找 Zoo_Entrance
        if (zooEntrance == null)
        {
            GameObject entrance = GameObject.Find("Zoo_Entrance");
            if (entrance != null)
            {
                zooEntrance = entrance.transform;
            }
            else
            {
                Debug.LogError("找不到 Zoo_Entrance！請在 Inspector 中指定或確保場景中有名為 Zoo_Entrance 的物件");
                return;
            }
        }
        
        // 開始移動
        StartIntroMovement();
    }
    
    void DisableOtherControls()
    {
        // 禁用 XR Device Simulator
        GameObject xrSimulator = GameObject.Find("XR Device Simulator");
        if (xrSimulator != null)
        {
            xrSimulator.SetActive(false);
            Debug.Log("已暫時禁用 XR Device Simulator");
        }
        
        // 禁用其他可能的控制腳本
        // 例如：CharacterController, FirstPersonController 等
        var controllers = GetComponents<MonoBehaviour>();
        foreach (var controller in controllers)
        {
            if (controller != this && 
                (controller.GetType().Name.Contains("Controller") || 
                 controller.GetType().Name.Contains("Movement")))
            {
                controller.enabled = false;
                Debug.Log($"已禁用 {controller.GetType().Name}");
            }
        }
    }
    
    void StartIntroMovement()
    {
        if (zooEntrance == null) return;
        
        startPosition = transform.position;
        
        // 計算目標位置（在 Zoo_Entrance 前方一點的位置）
        Vector3 directionToEntrance = (zooEntrance.position - startPosition).normalized;
        targetPosition = zooEntrance.position - directionToEntrance * lookAheadDistance;
        targetPosition.y = cameraHeight; // 保持視角高度
        
        isMoving = true;
        elapsedTime = 0f;
        
        Debug.Log($"開始移動到 Zoo_Entrance，預計時間：{moveDuration} 秒");
    }
    
    void Update()
    {
        if (!isMoving || zooEntrance == null) return;
        
        // 更新計時器
        elapsedTime += Time.deltaTime;
        float progress = elapsedTime / moveDuration;
        
        if (progress >= 1f)
        {
            // 移動完成
            transform.position = targetPosition;
            isMoving = false;
            Debug.Log("已到達 Zoo_Entrance 附近");
            
            // 這裡可以觸發其他事件，如載入下一個場景或啟用玩家控制
            OnIntroComplete();
            return;
        }
        
        // 使用 Smoothstep 實現平滑減速
        float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);
        
        // 更新位置
        transform.position = Vector3.Lerp(startPosition, targetPosition, smoothProgress);
        
        // 平滑地看向目標
        Vector3 lookDirection = (zooEntrance.position - transform.position).normalized;
        lookDirection.y = 0; // 保持水平視角
        if (lookDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 2f);
        }
    }
    
    void OnIntroComplete()
    {
        // 移動完成後的處理
        // 重新啟用 XR Device Simulator
        GameObject xrSimulator = GameObject.Find("XR Device Simulator");
        if (xrSimulator != null && disableOtherControlsOnStart)
        {
            xrSimulator.SetActive(true);
            Debug.Log("已重新啟用 XR Device Simulator");
        }
        
        Debug.Log("Intro 完成！可以在這裡加入後續邏輯");
    }
    
    // 在 Scene 視圖中繪製路徑
    void OnDrawGizmos()
    {
        if (zooEntrance == null) return;
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(zooEntrance.position, 1f);
        
        if (Application.isPlaying && isMoving)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(startPosition, targetPosition);
        }
    }
}