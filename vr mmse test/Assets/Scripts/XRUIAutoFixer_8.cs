// Assets/Scripts/Dev/XRUIAutoFixer.cs
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.XR.Interaction.Toolkit.UI;

[DefaultExecutionOrder(-1000)]
public class XRUIAutoFixer : MonoBehaviour
{
    void Awake()
    {
        FixEventSystem();
        FixCanvases();
        FixOverlays();
        Debug.Log("[XRUIAutoFixer] Applied.");
    }

    // ---- helpers: cross-version find ----
    static T[] FindAll<T>() where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
        return Object.FindObjectsOfType<T>(true);
#endif
    }

    void FixEventSystem()
    {
        var systems = FindAll<EventSystem>();
        EventSystem es = systems.Length > 0 ? systems[0] : new GameObject("EventSystem").AddComponent<EventSystem>();

        // 只留一個 EventSystem
        for (int i = 1; i < systems.Length; i++)
            if (systems[i]) Destroy(systems[i].gameObject);

#if ENABLE_INPUT_SYSTEM
        // 用新輸入系統的 UI 模組
        var old = es.GetComponent<StandaloneInputModule>();
        if (old) DestroyImmediate(old);
        var ui = es.GetComponent<InputSystemUIInputModule>() ?? es.gameObject.AddComponent<InputSystemUIInputModule>();
        ui.pointerBehavior = UIPointerBehavior.AllPointersAsIs; // 讓滑鼠/觸控/XR 都能用
#else
        // 備援：舊輸入系統
        var sim = es.GetComponent<StandaloneInputModule>() ?? es.gameObject.AddComponent<StandaloneInputModule>();
        sim.forceModuleActive = true;
#endif
    }

    void FixCanvases()
    {
        var mainCam = Camera.main;

        foreach (var c in FindAll<Canvas>())
        {
            if (c.renderMode == RenderMode.WorldSpace)
            {
                if (!c.GetComponent<GraphicRaycaster>()) c.gameObject.AddComponent<GraphicRaycaster>();
                if (!c.GetComponent<TrackedDeviceGraphicRaycaster>()) c.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
                if (!c.worldCamera && mainCam) c.worldCamera = mainCam; // 必填，否則 World Space UI 可能收不到指標
            }
        }
    }

    void FixOverlays()
    {
        // FadeOverlay：透明時不吃點擊
        var fo = GameObject.Find("FadeOverlay");
        if (fo)
        {
            var cg = fo.GetComponent<CanvasGroup>();
            if (cg && cg.alpha <= 0.001f)
            {
                cg.blocksRaycasts = false;
                cg.interactable   = false;
            }
        }

        // 大面積 Image 取消 Raycast Target（避免擋 UI）
        foreach (var img in FindAll<Image>())
        {
            if (!img.raycastTarget) continue;
            var r = img.rectTransform.rect;
            if (r.width >= 500f && r.height >= 300f) img.raycastTarget = false;
        }
    }
}
