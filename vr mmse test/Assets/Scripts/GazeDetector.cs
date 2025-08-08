using UnityEngine;
using System.Collections;

public class GazeDetector_ShowCanvas : MonoBehaviour
{
    public Camera playerCamera;         
    public float maxDistance = 10f;     
    public float gazeDuration = 1.0f;   
    public GameObject sofaUI;           
    public float fadeDuration = 1.0f;   // 淡入時間(秒)

    private float gazeTimer = 0f;
    private GameObject currentGazed;
    private bool found = false;

    private CanvasGroup sofaCanvasGroup;

    void Start()
    {
        if (sofaUI != null)
        {
            sofaCanvasGroup = sofaUI.GetComponent<CanvasGroup>();
            if (sofaCanvasGroup != null)
            {
                sofaCanvasGroup.alpha = 0f; // 確保一開始透明
                sofaUI.SetActive(false);   // 一開始隱藏
            }
        }
    }

    void Update()
    {
        if (found) return;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, maxDistance))
        {
            if (hit.collider.CompareTag("Sofa"))
            {
                if (currentGazed == hit.collider.gameObject)
                {
                    gazeTimer += Time.deltaTime;
                }
                else
                {
                    currentGazed = hit.collider.gameObject;
                    gazeTimer = 0f;
                }

                if (gazeTimer >= gazeDuration)
                {
                    OnSofaFound();
                }
                return;
            }
        }

        gazeTimer = 0f;
        currentGazed = null;
    }

    void OnSofaFound()
    {
        found = true;
        Debug.Log("找到沙發！顯示並淡入 Canvas");

        if (sofaUI != null && sofaCanvasGroup != null)
        {
            sofaUI.SetActive(true); // 顯示物件
            StartCoroutine(FadeInUI());
        }
        else
        {
            Debug.LogWarning("sofaUI 或 CanvasGroup 沒有設定！");
        }
    }

    IEnumerator FadeInUI()
    {
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            sofaCanvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }
        sofaCanvasGroup.alpha = 1f; // 確保完全顯示
    }
}
