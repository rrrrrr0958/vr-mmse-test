using UnityEngine;
using UnityEngine.UI;

public class VRButtonSelector : MonoBehaviour
{
    [Header("VR Settings")]
    public Camera vrCamera; 				// 玩家頭盔相機
    public float viewAngleThreshold = 15f; 	// 視角判斷閾值（角度）
    public float maxViewDistance = 15f; 	// 可偵測距離

    [Header("UI Elements")]
    public GameObject sofa; 				// 沙發物件
    public GameObject hintText; 			// 提示文字
    public Button actionButton; 			// 按鈕（僅顯示用）
    public GameObject extraUIObject;        // 【新增】想要一起出現的物件

    [Header("Audio Settings")]
    public AudioClip guidingVoiceClip; 		// 要循環播放的語音音檔
    private AudioSource audioSource; 		// 引用 AudioSource 元件

    private bool isSofaVisible = false; 	// 是否目前看到沙發

    void Start()
    {
        // 1. 確保有 AudioSource 元件
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            // 如果物件上沒有 AudioSource，自動新增一個
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // 初始隱藏提示、按鈕【和新增的物件】
        if (hintText != null) hintText.SetActive(false);
        if (actionButton != null) actionButton.gameObject.SetActive(false);
        if (extraUIObject != null) extraUIObject.SetActive(false); // 【新增】初始隱藏

        // 2. 開始播放語音
        if (guidingVoiceClip != null)
        {
            audioSource.clip = guidingVoiceClip;
            audioSource.loop = true; // 設置為循環播放
            audioSource.playOnAwake = false;
            audioSource.Play();
        }
    }

    void Update()
    {
        CheckSofaInView(); 	// 每幀檢查玩家是否看到沙發
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
            // 第一次看到沙發 → 顯示提示、按鈕【和新增的物件】
            isSofaVisible = true;
            if (hintText != null) hintText.SetActive(true);
            if (actionButton != null) actionButton.gameObject.SetActive(true);
            if (extraUIObject != null) extraUIObject.SetActive(true); // 【新增】同時顯示新的 UI 物件
            
            // 3. 停止播放語音
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }
    }
    public void OnSceneSwitchButtonClicked_sofa()
    {
        SceneFlowManager.instance.LoadNextScene();
        // SceneFlowManager.instance.LoadNextScene();
        // SceneManager.LoadScene("NextSceneName");
    }
}
