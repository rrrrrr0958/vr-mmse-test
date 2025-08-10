using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

public class VRButtonSelector : MonoBehaviour
{
    public Camera vrCamera;
    public float maxDistance = 10f;
    public Color normalColor = Color.white;
    public Color highlightColor = Color.green;

    private Button currentButton;
    private Button lastButton;

    private InputDevice leftController;
    private InputDevice rightController;

    void Start()
    {
        leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
    }

    void Update()
    {
        // 確保控制器裝置是有效的
        if (!leftController.isValid)
            leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (!rightController.isValid)
            rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        CheckButtonHover();
        CheckButtonPress();
    }

    void CheckButtonHover()
    {
        Ray ray = new Ray(vrCamera.transform.position, vrCamera.transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, maxDistance))
        {
            Button btn = hit.collider.GetComponent<Button>();
            if (btn != null)
            {
                currentButton = btn;

                if (lastButton != currentButton)
                {
                    ResetButtonColor(lastButton);
                    HighlightButton(currentButton);
                    lastButton = currentButton;
                }
                return;
            }
        }

        if (lastButton != null)
        {
            ResetButtonColor(lastButton);
            lastButton = null;
            currentButton = null;
        }
    }

    void CheckButtonPress()
    {
        bool pressed = false;

        // 左手
        if (leftController.isValid)
        {
            if (leftController.TryGetFeatureValue(CommonUsages.primaryButton, out bool p1) && p1)
            {
                pressed = true;
                Debug.Log("Left Controller A 按下");
            }
            if (leftController.TryGetFeatureValue(CommonUsages.secondaryButton, out bool p2) && p2)
            {
                pressed = true;
                Debug.Log("Left Controller B 按下");
            }
        }

        // 右手
        if (rightController.isValid)
        {
            if (rightController.TryGetFeatureValue(CommonUsages.primaryButton, out bool p3) && p3)
            {
                pressed = true;
                Debug.Log("Right Controller A 按下");
            }
            if (rightController.TryGetFeatureValue(CommonUsages.secondaryButton, out bool p4) && p4)
            {
                pressed = true;
                Debug.Log("Right Controller B 按下");
            }
        }

        if (pressed && currentButton != null)
        {
            Debug.Log($"觸發按鈕：{currentButton.name}");
            currentButton.onClick.Invoke();
        }
    }

    void HighlightButton(Button btn)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null)
            img.color = highlightColor;
    }

    void ResetButtonColor(Button btn)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null)
            img.color = normalColor;
    }
}
