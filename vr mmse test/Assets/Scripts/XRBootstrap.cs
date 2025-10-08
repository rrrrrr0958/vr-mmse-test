using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine.XR.Management; // 重要

public class XRBootstrap : MonoBehaviour
{
    private static XRBootstrap I;

    void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        StartCoroutine(EnsureXRRunning());
    }

    private void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        // 每次進入新場景，確保 XR 子系統與頭顯追蹤已經起來
        StartCoroutine(EnsureXRRunning());
    }

    private IEnumerator EnsureXRRunning()
    {
        var settings = XRGeneralSettings.Instance;
        if (settings == null || settings.Manager == null) yield break;

        // 如果尚未初始化，先初始化
        if (settings.Manager.activeLoader == null)
        {
            yield return settings.Manager.InitializeLoader();
        }

        // 啟動 XR 子系統
        settings.Manager.StartSubsystems();

        // 小等一下讓 HMD pose 回來（避免一進場視角被鎖）
        yield return new WaitForSeconds(0.2f);

        // 可選：這裡再檢查場景裡是否有 XR Origin
        // （若沒有，印警告以免用到錯相機）
        var xrOrigin = FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsInactive.Include);
        if (xrOrigin == null)
        {
            Debug.LogWarning("[XRBootstrap] 本場景找不到 XR Origin，請在每個場景放一個 XR Origin prefab。");
        }
    }
}
