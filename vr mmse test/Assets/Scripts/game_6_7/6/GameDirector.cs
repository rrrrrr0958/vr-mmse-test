using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit; // for XRBaseInteractable

public class GameDirector : MonoBehaviour
{
    public static GameDirector Instance { get; private set; }

    public enum Phase { Game1, Transition, Game2 }
    public Phase phase = Phase.Game1;

    [Header("UI Panels")]
    public GameObject game1UI;         // 題目敘述等（Game1 時顯示）
    public GameObject levelClearPanel; // 「恭喜完成」面板（預設關閉）

    [Header("Timings")]
    public float delayShowClear = 8f;  // 選到後過幾秒顯示完成面板
    public float delayToGame2   = 2f;  // 顯示面板後再過幾秒切到 Game2

    [Header("XR")]
    [Tooltip("XR Origin（或 XR Rig）物件，切到 Game2 會把它 Y 軸旋轉 180 度")]
    public Transform xrOrigin;

    public bool answerLocked { get; private set; } = false;   // 一旦選過就鎖
    public string chosenId { get; private set; } = null;      // 第一次選到的 id

    void Awake()
    {  
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (levelClearPanel) levelClearPanel.SetActive(false);
        if (game1UI) game1UI.SetActive(phase == Phase.Game1);
    }

    public bool CanInteractGame1()
    {
        return phase == Phase.Game1 && !answerLocked;
    }

    // QuizManager 決定正誤之後呼叫這個
    public void LockAndAdvance(bool correct, string pickedId)
    {
        if (answerLocked) return; // 已經處理過就忽略
        answerLocked = true;
        chosenId = pickedId;

        Debug.Log($"[Game1] 目標物: {pickedId}  選擇：{(correct ? "正確" : "錯誤")}");

        if (game1UI) game1UI.SetActive(false);

        StartCoroutine(DoTransition());
    }

    IEnumerator DoTransition()
    {
        phase = Phase.Transition;

        yield return new WaitForSeconds(delayShowClear);
        if (levelClearPanel) levelClearPanel.SetActive(true);

        yield return new WaitForSeconds(delayToGame2);
        EnterGame2();
    }

    void EnterGame2()
    {
        if (levelClearPanel) levelClearPanel.SetActive(false);
        phase = Phase.Game2;

        // 1) 把所有高亮關掉 & 清掉全域選中
        SelectionHighlightRegistry.ClearAll();

        // 2) 關閉 Game1 所有互動（防止 hover/選取）
        SetGame1InteractablesEnabled(false);

        // 3) 視角轉 180 度（旋轉 XR Origin）
        if (xrOrigin) xrOrigin.Rotate(0f, 180f, 0f, Space.World);

        Debug.Log("[Game2] 開始！");
    }

    void SetGame1InteractablesEnabled(bool on)
    {
#if UNITY_2022_2_OR_NEWER
        var list = Object.FindObjectsByType<SelectableTarget>(FindObjectsSortMode.None);
#else
        var list = Object.FindObjectsOfType<SelectableTarget>(true);
#endif
        foreach (var t in list)
        {
            var xri = t.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
            if (xri) xri.enabled = on;
        }
    }
}
