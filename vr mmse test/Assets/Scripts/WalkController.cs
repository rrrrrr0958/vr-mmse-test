using UnityEngine;

namespace ZooExit
{
    public class WalkController : MonoBehaviour
    {
        [Header("References")]
        public PersonWalkToGate personWalkScript;
        public KeyCode startWalkKey = KeyCode.Space;
        
        [Header("Camera Settings")]
        public Camera mainCamera;
        public bool followPerson = true;
        public Vector3 cameraOffset = new Vector3(0, 2, -3);
        public float cameraSmoothSpeed = 2f;
        
        private Transform personTransform;
        
        void Start()
        {
            // 自動找到PersonWalkToGate腳本
            if (personWalkScript == null)
            {
                personWalkScript = FindObjectOfType<PersonWalkToGate>();
            }
            
            if (personWalkScript != null)
            {
                personTransform = personWalkScript.transform;
            }
            
            // 自動找到主攝影機
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null)
                {
                    mainCamera = FindObjectOfType<Camera>();
                }
            }
            
            Debug.Log("Press SPACE to start walking to the gate!");
        }
        
        void Update()
        {
            // 按空白鍵開始行走
            if (Input.GetKeyDown(startWalkKey))
            {
                if (personWalkScript != null)
                {
                    personWalkScript.StartWalking();
                    Debug.Log("Started walking to gate!");
                }
            }
            
            // 攝影機跟隨
            if (followPerson && mainCamera != null && personTransform != null)
            {
                Vector3 targetPosition = personTransform.position + cameraOffset;
                mainCamera.transform.position = Vector3.Lerp(
                    mainCamera.transform.position, 
                    targetPosition, 
                    cameraSmoothSpeed * Time.deltaTime
                );
                
                // 讓攝影機看向人物
                Vector3 lookDirection = personTransform.position - mainCamera.transform.position;
                lookDirection.y = 0; // 保持水平視角
                if (lookDirection != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                    mainCamera.transform.rotation = Quaternion.Slerp(
                        mainCamera.transform.rotation,
                        targetRotation,
                        cameraSmoothSpeed * Time.deltaTime
                    );
                }
            }
        }
        
        void OnGUI()
        {
            // 顯示控制說明
            GUI.Box(new Rect(10, 10, 250, 100), "Walk to Gate Controls");
            GUI.Label(new Rect(20, 35, 230, 20), "Press SPACE to start walking");
            GUI.Label(new Rect(20, 55, 230, 20), "Toggle Follow Camera: F");
            
            if (GUI.Button(new Rect(20, 75, 100, 20), "Start Walk"))
            {
                if (personWalkScript != null)
                {
                    personWalkScript.StartWalking();
                }
            }
            
            // 切換攝影機跟隨
            if (Input.GetKeyDown(KeyCode.F))
            {
                followPerson = !followPerson;
                Debug.Log($"Camera follow: {followPerson}");
            }
        }
    }
}