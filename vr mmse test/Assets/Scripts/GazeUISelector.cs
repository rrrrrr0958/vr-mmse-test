using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.SceneManagement;
using System.Collections;

public class VRButtonSelector : MonoBehaviour
{
    public Camera vrCamera;
    public float maxDistance = 10f;
    public Color normalColor = Color.white;
    public Color highlightColor = Color.green;

    [Header("Fade Settings")]
    public CanvasGroup fadeCanvasGroup;  // 拖全螢幕黑色 UI
    public float fadeDuration = 1f;      // 淡入/淡出時間

    private Button currentButton;
    private Button lastButton;

    private InputDevice leftController;
    private InputDevice rightController;

    void Start()
    {
        leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        // 讓遊戲一開始就透明，不做淡入
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0;
        }
    }

    void Update()
    {
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
            Button btn = hit.collider.GetComponent<Button>() ?? hit.collider.GetComponentInParent<Button>();

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

        if (leftController.isValid)
        {
            if (leftController.TryGetFeatureValue(CommonUsages.primaryButton, out bool p1) && p1)
                pressed = true;
            if (leftController.TryGetFeatureValue(CommonUsages.secondaryButton, out bool p2) && p2)
                pressed = true;
        }

        if (rightController.isValid)
        {
            if (rightController.TryGetFeatureValue(CommonUsages.primaryButton, out bool p3) && p3)
                pressed = true;
            if (rightController.TryGetFeatureValue(CommonUsages.secondaryButton, out bool p4) && p4)
                pressed = true;
        }

        if (pressed && currentButton != null)
        {
            Debug.Log($"觸發按鈕：{currentButton.name} → 切換到 Prefabs 場景");

            // 使用淡出再切換場景
            if (fadeCanvasGroup != null)
                StartCoroutine(FadeOutAndLoad("Prefabs"));
            else
                SceneManager.LoadScene("Prefabs", LoadSceneMode.Single);
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

    IEnumerator FadeIn()
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            fadeCanvasGroup.alpha = 1 - (t / fadeDuration);
            yield return null;
        }
        fadeCanvasGroup.alpha = 0;
    }

    IEnumerator FadeOutAndLoad(string sceneName)
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            fadeCanvasGroup.alpha = t / fadeDuration;
            yield return null;
        }
        fadeCanvasGroup.alpha = 1;
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }
}
