using UnityEngine;

public class BackgroundGrass : MonoBehaviour
{
    void Awake()
    {
        // 直接修改這個 GameObject 的屬性
        // 假設這個腳本是附加到你想要變成草地的 Plane 上

        // 調整地板大小 (如果需要的話)
        // 注意：localScale 是相對於父物件的，這裡修改的是腳本所附加的 Plane 本身
        transform.localScale = new Vector3(5, 1, 3);

        // 設定草綠色
        // GetComponent<Renderer>() 會取得這個 GameObject 上的 Renderer 組件
        GetComponent<Renderer>().material.color = new Color(0.6f, 0.8f, 0.5f);

        // 你可以給這個 GameObject 命名，方便在 Hierarchy 視窗中識別
        // (通常如果已經有名字了就不需要再設定了)
        // gameObject.name = "GameGround"; 
    }
}