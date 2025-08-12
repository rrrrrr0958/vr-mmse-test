using UnityEngine;

public class SimpleFPS : MonoBehaviour
{
    public Transform cameraTransform; // 拖 Main Camera 進來
    public float moveSpeed = 5f;
    public float mouseSensitivity = 2f;

    float xRotation = 0f;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked; // 鎖定滑鼠
        transform.rotation = Quaternion.Euler(0f, 20.247f, 0f); // 人物本體朝向
        cameraTransform.localRotation = Quaternion.Euler(0f, 20.247f, 0f); // 相機抬頭/低頭角度
        Cursor.visible = false;
    }

    void Update()
    {
        // 滑鼠視角
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);

        // WASD 移動
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        Vector3 move = transform.right * moveX + transform.forward * moveZ;
        transform.position += move * moveSpeed * Time.deltaTime;
    }
}
