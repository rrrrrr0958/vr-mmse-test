using UnityEngine;
using UnityEngine.UI;

public class VRButtonSelector : MonoBehaviour
{
    [Header("VR Settings")]
    public Camera vrCamera;                 // 玩家頭盔相機
    public float viewAngleThreshold = 15f;  // 視角判斷閾值（角度）
    public float maxViewDistance = 15f;     // 可偵測距離

    [Header("UI Elements")]
    public GameObject sofa;                 // 沙發物件
    public GameObject hintText;             // 提示文字
    public Button actionButton;             // 按鈕（僅顯示用）

    private bool isSofaVisible = false;     // 是否目前看到沙發

    void Start()
    {
        // 初始隱藏提示與按鈕
        if (hintText != null) hintText.SetActive(false);
        if (actionButton != null) actionButton.gameObject.SetActive(false);
    }

    void Update()
    {
        CheckSofaInView();  // 每幀檢查玩家是否看到沙發
    }

    /// <summary>
    /// 檢查玩家視線是否看向沙發
    /// </summary>
    void CheckSofaInView()
    {
        if (sofa == null || vrCamera == null) return;

        Vector3 dirToSofa = (sofa.transform.position - vrCamera.transform.position).normalized;
        float angle = Vector3.Angle(vrCamera.transform.forward, dirToSofa);
        float distance = Vector3.Distance(vrCamera.transform.position, sofa.transform.position);

        bool visibleNow = (angle < viewAngleThreshold && distance < maxViewDistance);

        if (visibleNow && !isSofaVisible)
        {
            // 第一次看到沙發 → 顯示提示與按鈕
            isSofaVisible = true;
            if (hintText != null) hintText.SetActive(true);
            if (actionButton != null) actionButton.gameObject.SetActive(true);
        }

    }
    public void OnSceneSwitchButtonClicked_sofa()
    {
        SceneFlowManager.instance.LoadNextScene();
        // SceneFlowManager.instance.LoadNextScene();
        // SceneManager.LoadScene("NextSceneName");
    }
}
