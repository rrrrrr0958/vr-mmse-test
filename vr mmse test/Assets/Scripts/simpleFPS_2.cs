using UnityEngine;

public class SimpleFPS : MonoBehaviour
{
    public Transform cameraTransform; 
    public float moveSpeed = 5f;
    public float mouseSensitivity = 2f;

    float xRotation = 0f;
    float cameraInitialY = 0f;  // 🔹多加一個變數記住初始 Y

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 讀取初始旋轉
        Vector3 camAngles = cameraTransform.localEulerAngles;
        xRotation = camAngles.x;
        cameraInitialY = camAngles.y;   // 🔹存下 Y 軸
    }

    void Update()
    {
        // 滑鼠視角
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        // 🔹保留初始 Y，不要硬塞 0
        cameraTransform.localRotation = Quaternion.Euler(xRotation, cameraInitialY, 0f);

        // 🔹角色本體控制左右轉
        transform.Rotate(Vector3.up * mouseX);

        // 移動
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");
        Vector3 move = transform.right * moveX + transform.forward * moveZ;
        transform.position += move * moveSpeed * Time.deltaTime;
    }
}
