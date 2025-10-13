using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class UIAnimation : MonoBehaviour
{
    public Image image;       // UI Image
    public Sprite sprite1;    // 第一張圖片
    public Sprite sprite2;    // 第二張圖片
    public float frameTime = 0.5f; // 每張圖片停留時間（秒）
    public int repeatCount = 3;    // 重複播放次數

    void Start()
    {
        if (image != null && sprite1 != null && sprite2 != null)
        {
            StartCoroutine(PlayAnimation());
        }
        else
        {
            Debug.LogWarning("請確認 image 與 sprites 已經設定！");
        }
    }

    IEnumerator PlayAnimation()
    {
        for (int i = 0; i < repeatCount; i++)
        {
            image.sprite = sprite1;
            yield return new WaitForSeconds(frameTime);

            image.sprite = sprite2;
            yield return new WaitForSeconds(frameTime);
        }
        image.enabled = false;
    }
    

}
