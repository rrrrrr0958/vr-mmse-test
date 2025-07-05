using UnityEngine;
using System.Collections;

public class AutoWalkToEntrance : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 2.0f;
    [SerializeField] private float rotationSpeed = 2.0f;
    
    [Header("Target Settings")]
    [SerializeField] private string targetName = "Zoo_Entrance_Illustra_0618121213_texture (1)";
    [SerializeField] private float stopDistance = 2.0f;
    
    [Header("Camera Settings")]
    [SerializeField] private bool useXRCamera = true;
    
    private Transform target;
    private Transform playerTransform;
    private Camera playerCamera;
    private bool isWalking = false;
    private Vector3 targetPosition;
    
    void Start()
    {
        // 尋找目標
        GameObject targetObject = GameObject.Find(targetName);
        if (targetObject != null)
        {
            target = targetObject.transform;
            // 計算目標位置（在地面高度）
            targetPosition = new Vector3(target.position.x, transform.position.y, target.position.z);
        }
        else
        {
            Debug.LogError($"Target '{targetName}' not found!");
            enabled = false;
            return;
        }
        
        // 設定玩家變換和相機
        if (useXRCamera)
        {
            // 使用XR Origin系統
            GameObject xrOrigin = GameObject.Find("XR Origin (XR Rig)");
            if (xrOrigin != null)
            {
                playerTransform = xrOrigin.transform;
                Camera xrCamera = xrOrigin.GetComponentInChildren<Camera>();
                if (xrCamera != null)
                {
                    playerCamera = xrCamera;
                    Debug.Log("Using XR Camera for first-person view");
                }
            }
        }
        else
        {
            // 使用普通相機
            playerTransform = transform;
            GameObject normalCamera = GameObject.Find("Camera");
            if (normalCamera != null)
            {
                playerCamera = normalCamera.GetComponent<Camera>();
                // 將相機設為子物件以跟隨移動
                normalCamera.transform.parent = transform;
                normalCamera.transform.localPosition = new Vector3(0, 1.6f, 0);
                normalCamera.transform.localRotation = Quaternion.identity;
                Debug.Log("Using normal Camera for first-person view");
            }
        }
        
        if (playerCamera == null)
        {
            Debug.LogError("No camera found!");
            enabled = false;
            return;
        }
        
        // 開始自動行走
        StartCoroutine(AutoWalkCoroutine());
    }
    
    IEnumerator AutoWalkCoroutine()
    {
        // 等待一秒讓場景穩定
        yield return new WaitForSeconds(1.0f);
        
        isWalking = true;
        Debug.Log("Starting auto-walk to Zoo Entrance...");
        
        while (isWalking)
        {
            float distance = Vector3.Distance(playerTransform.position, targetPosition);
            
            if (distance > stopDistance)
            {
                // 計算方向
                Vector3 direction = (targetPosition - playerTransform.position).normalized;
                direction.y = 0; // 保持在水平面上
                
                // 旋轉朝向目標
                if (direction != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction);
                    playerTransform.rotation = Quaternion.Slerp(playerTransform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                }
                
                // 向前移動
                playerTransform.position += playerTransform.forward * walkSpeed * Time.deltaTime;
            }
            else
            {
                isWalking = false;
                Debug.Log("Arrived at Zoo Entrance!");
                
                // 可選：到達後看向入口
                Vector3 lookDirection = target.position - playerCamera.transform.position;
                lookDirection.y = 0;
                if (lookDirection != Vector3.zero)
                {
                    playerTransform.rotation = Quaternion.LookRotation(lookDirection);
                }
            }
            
            yield return null;
        }
    }
    
    // 在編輯器中繪製路徑
    void OnDrawGizmos()
    {
        if (target != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, targetPosition);
            Gizmos.DrawWireSphere(targetPosition, stopDistance);
        }
    }
}
