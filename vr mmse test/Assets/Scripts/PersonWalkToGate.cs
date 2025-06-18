using UnityEngine;

namespace ZooExit
{
    public class PersonWalkToGate : MonoBehaviour
    {
        [Header("Movement Settings")]
        public float walkSpeed = 2.0f;
        public float rotationSpeed = 5.0f;
        
        [Header("Target")]
        public Transform gateTarget;
        public Vector3 gatePosition = new Vector3(0f, 0f, 5f); // 預設閘門位置
        
        [Header("Animation")]
        public bool isWalking = false;
        public float bobHeight = 0.1f;
        public float bobSpeed = 5f;
        
        private Vector3 startPosition;
        private Vector3 targetPosition;
        private float originalY;
        private bool hasReachedGate = false;
        private float walkProgress = 0f;
        
        void Start()
        {
            // 記錄起始位置
            startPosition = transform.position;
            originalY = transform.position.y;
            
            // 設定目標位置
            if (gateTarget != null)
            {
                targetPosition = gateTarget.position;
            }
            else
            {
                targetPosition = gatePosition;
            }
            
            Debug.Log($"Person starting walk from {startPosition} to {targetPosition}");
        }
        
        void Update()
        {
            if (!hasReachedGate)
            {
                WalkToTarget();
            }
        }
        
        void WalkToTarget()
        {
            Vector3 direction = (targetPosition - transform.position).normalized;
            direction.y = 0; // 保持在地面上
            
            // 檢查是否到達目標
            float distanceToTarget = Vector3.Distance(transform.position, targetPosition);
            if (distanceToTarget < 0.5f)
            {
                hasReachedGate = true;
                isWalking = false;
                Debug.Log("Reached the gate!");
                return;
            }
            
            // 移動
            Vector3 movement = direction * walkSpeed * Time.deltaTime;
            transform.position += movement;
            
            // 旋轉面向目標
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
            
            // 行走動畫（上下擺動）
            isWalking = true;
            walkProgress += bobSpeed * Time.deltaTime;
            float bobOffset = Mathf.Sin(walkProgress) * bobHeight;
            Vector3 pos = transform.position;
            pos.y = originalY + bobOffset;
            transform.position = pos;
        }
        
        // 公開方法讓外部調用
        public void StartWalking()
        {
            hasReachedGate = false;
            walkProgress = 0f;
            if (gateTarget != null)
            {
                targetPosition = gateTarget.position;
            }
            else
            {
                targetPosition = gatePosition;
            }
        }
        
        public void SetGatePosition(Vector3 newGatePosition)
        {
            gatePosition = newGatePosition;
            targetPosition = newGatePosition;
        }
        
        public void SetGateTarget(Transform newTarget)
        {
            gateTarget = newTarget;
            if (gateTarget != null)
            {
                targetPosition = gateTarget.position;
            }
        }
        
        // 在Scene視圖中顯示路徑
        void OnDrawGizmos()
        {
            if (Application.isPlaying)
            {
                // 顯示起始位置到目標的路徑
                Gizmos.color = Color.green;
                Gizmos.DrawLine(startPosition, targetPosition);
                
                // 顯示目標位置
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(targetPosition, 0.5f);
                
                // 顯示當前位置
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(transform.position, 0.3f);
            }
        }
    }
}