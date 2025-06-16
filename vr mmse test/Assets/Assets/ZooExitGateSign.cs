using UnityEngine;
using TMPro;

public class ZooExitGateSign : MonoBehaviour
{
    public string signText = "Exit";
    public float textSize = 2.5f;
    public Color textColor = Color.white;
    public TMP_FontAsset fontAsset;

    void Start()
    {
        GameObject textObj = new GameObject("ExitSignText");
        textObj.transform.SetParent(transform);
        textObj.transform.localPosition = new Vector3(0, 0.6f, -0.6f);
        var textMesh = textObj.AddComponent<TextMeshPro>();
        textMesh.text = signText;
        textMesh.fontSize = textSize;
        textMesh.color = textColor;
        textMesh.alignment = TextAlignmentOptions.Center;
        if (fontAsset != null) textMesh.font = fontAsset;
        textMesh.enableAutoSizing = true;
        textMesh.rectTransform.sizeDelta = new Vector2(4, 1);
    }
}
