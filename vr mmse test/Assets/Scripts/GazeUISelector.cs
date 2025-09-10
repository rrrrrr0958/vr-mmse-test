using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.SceneManagement;
using System.Collections;

public class VRButtonSelector : MonoBehaviour
{
    [Header("VR Settings")]
    public Camera vrCamera;
    public float maxDistance = 10f;
    public Color normalColor = Color.white;
    public Color highlightColor = Color.green;

    [Header("Scene Transition")]
    public string targetSceneName = "Login Scene"; // 這裡可以在 Inspector 設定要切換的場景名稱
    public CanvasGroup fadeCanvasGroup;  
    public float fadeDuration = 1f;

    private Button currentButton;
    private Button lastButton;

    private InputDevice leftController;
    private InputDevice rightController;

    void Awake()
    {
        // 確保淡入淡出控制器跨場景保留
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0;
        }

        // 每次場景載入後自動淡入
        SceneManager.sceneLoaded += (scene, mode) =>
        {
            StartCoroutine(FadeIn());
        };
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
            Debug.Log($"觸發按鈕：{currentButton.name} → 切換到 {targetSceneName} 場景");
            StartCoroutine(FadeOutAndLoad(targetSceneName));
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
        if (fadeCanvasGroup == null) yield break;

        float t = 0f;
        fadeCanvasGroup.alpha = 1;
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
        if (fadeCanvasGroup == null)
        {
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            yield break;
        }

        float t = 0f;
        fadeCanvasGroup.alpha = 0;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            fadeCanvasGroup.alpha = t / fadeDuration;
            yield return null;
        }
        fadeCanvasGroup.alpha = 1;

        yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        // 淡入會在 sceneLoaded 事件中自動觸發
    }
}
