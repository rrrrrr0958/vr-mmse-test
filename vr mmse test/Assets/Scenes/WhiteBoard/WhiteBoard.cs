using System.Collections.Generic;
using UnityEngine;

public class WhiteBoard : MonoBehaviour
{
    [Tooltip("The Render Texture to draw on")]
    public RenderTexture renderTexture;

    [Header("BrushSettings")]
    [Tooltip("Max distance for brush detection (for physical pen mode)")]
    public float maxDistance = 0.2f;

    [Tooltip("Minimum distance between brush positions (pixels)")]
    public float minBrushDistance = 1f;

    private Material brushMaterial; // Material used for GL drawing

    public Color backGroundColor = Color.white;  // 初始化清空畫布
    [Range(0, 1)]
    public float markerAlpha = 0.7f;

    [Header("Collider Type")]
    [Tooltip("Set false to use a MeshCollider for 3d objects")]
    public bool useBoxCollider = true;

    [Header("Raycast Filter")]
    [Tooltip("只讓繪圖 Ray 命中這些層（建議只勾 Whiteboard 層）")]
    public LayerMask drawLayers = ~0;

    [Header("Lifecycle")]
    public bool autoClearOnExit = false; // 停止 Play/關閉物件時自動清空（方便開發）

    [System.Serializable]
    public class BrushSettings
    {
        [Header("Brush Object")]
        public Transform brushTransform;   // 筆尖 Transform（physical-pen 模式會用到）
        public bool isHeld = true;         // 是否啟用/拿著（可由其他腳本控制）

        [Header("Visual")]
        public Color color = Color.black;  // 筆色
        public int sizeY = 20;
        public int sizeX = 20;

        [HideInInspector] public Vector2 lastPosition;
        [HideInInspector] public bool isFirstDraw = true;
        [HideInInspector] public bool isDrawing = false;
    }

    [Header("Add Brushes")]
    public List<BrushSettings> brushes = new List<BrushSettings>();

    void Start()
    {
        brushMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));

        var r = GetComponent<Renderer>();
        if (r != null && renderTexture != null)
            r.material.mainTexture = renderTexture;

        if (renderTexture != null)
        {
            EnsureRTSettings(renderTexture);

            var prev = RenderTexture.active;
            RenderTexture.active = renderTexture;
            GL.Clear(true, true, backGroundColor);

            foreach (var b in brushes)
            {
                var c = b.color;
                c.a = markerAlpha;
                b.color = c;
                // 起手狀態
                b.isFirstDraw = true;
                b.isDrawing = false;
            }

            RenderTexture.active = prev;
        }
        else
        {
            Debug.LogWarning("[WhiteBoard] renderTexture 未指定。");
        }
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        if (autoClearOnExit) ClearBoard();
#endif
    }

    void Update()
    {
        // 仍保留「實體筆」模式（brushTransform + isHeld）：
        if (renderTexture == null) return;

        var prev = RenderTexture.active;
        RenderTexture.active = renderTexture;

        foreach (var brush in brushes)
        {
            if (!brush.isHeld) continue;
            if (brush.brushTransform == null) continue;

            // 從筆尖 forward 發射 Ray（只打到 drawLayers）
            Ray ray = new Ray(brush.brushTransform.position, brush.brushTransform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, drawLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.gameObject == gameObject)
                {
                    Vector2 current = HitToTexel(hit);
                    ProcessStroke(brush, current, brush.brushTransform.rotation.eulerAngles.z);
                }
            }
            else
            {
                brush.isDrawing = false;
                brush.isFirstDraw = true;
            }
        }

        RenderTexture.active = prev;
    }

    // ======= 供「XR Ray + 扳機」直接呼叫的 API =======

    /// <summary>用 RaycastHit（必須命中這塊 WhiteBoard）來畫，適用射線模式。</summary>
    public void StrokeFromHit(RaycastHit hit, int brushIndex, float rotationZDeg = 0f)
    {
        if (renderTexture == null) return;
        if (brushIndex < 0 || brushIndex >= brushes.Count) return;
        if (hit.collider.gameObject != gameObject) return;

        var brush = brushes[brushIndex];
        var prev = RenderTexture.active;
        RenderTexture.active = renderTexture;

        Vector2 current = HitToTexel(hit);
        ProcessStroke(brush, current, rotationZDeg);

        RenderTexture.active = prev;
    }

    /// <summary>結束該支筆的筆劃（扳機放開）</summary>
    public void EndStroke(int brushIndex)
    {
        if (brushIndex < 0 || brushIndex >= brushes.Count) return;
        var brush = brushes[brushIndex];
        brush.isDrawing = false;
        brush.isFirstDraw = true;
    }

    // ======= 內部工具 =======

    // 把命中位置轉成 RenderTexture 的像素座標
    Vector2 HitToTexel(RaycastHit hit)
    {
        Vector2 uv;
        if (useBoxCollider)
        {
            var box = GetComponent<BoxCollider>();
            if (box == null)
            {
                Debug.LogError("缺少 BoxCollider，請改用 MeshCollider 或加上 BoxCollider。");
                return Vector2.zero;
            }
            Vector3 local = transform.InverseTransformPoint(hit.point);
            uv = new Vector2(
                (local.x / box.size.x) + 0.5f,
                1f - ((local.y / box.size.y) + 0.5f)
            );
        }
        else
        {
            uv = hit.textureCoord;
            uv.y = 1f - uv.y;
        }

        int x = Mathf.Clamp((int)(uv.x * renderTexture.width), 0, renderTexture.width - 1);
        int y = Mathf.Clamp((int)(uv.y * renderTexture.height), 0, renderTexture.height - 1);
        return new Vector2(x, y);
    }

    // 統一處理插值與落筆
    void ProcessStroke(BrushSettings brush, Vector2 current, float rotZDeg)
    {
        if (!brush.isDrawing)
        {
            brush.isFirstDraw = true;
            brush.isDrawing = true;
        }

        if (brush.isFirstDraw)
        {
            DrawAtPosition(current, brush.color, brush.sizeX, brush.sizeY, rotZDeg);
            brush.lastPosition = current;
            brush.isFirstDraw = false;
            return;
        }

        // BoxCollider / 射線模式：永遠插值（避免斷點）
        float dist = Vector2.Distance(current, brush.lastPosition);
        int steps = Mathf.Max(1, Mathf.CeilToInt(dist / Mathf.Max(0.1f, minBrushDistance)));
        for (int i = 1; i <= steps; i++)
        {
            Vector2 p = Vector2.Lerp(brush.lastPosition, current, i / (float)steps);
            DrawAtPosition(p, brush.color, brush.sizeX, brush.sizeY, rotZDeg);
        }

        brush.lastPosition = current;
    }

    void DrawAtPosition(Vector2 pos, Color color, float sizeX, float sizeY, float rotDeg)
    {
        GL.PushMatrix();
        GL.LoadPixelMatrix(0, renderTexture.width, renderTexture.height, 0);
        brushMaterial.SetPass(0);

        GL.Begin(GL.QUADS);
        GL.Color(color);

        float rad = rotDeg * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);

        Vector2[] v = new Vector2[4];
        v[0] = new Vector2(-sizeX, -sizeY);
        v[1] = new Vector2( sizeX, -sizeY);
        v[2] = new Vector2( sizeX,  sizeY);
        v[3] = new Vector2(-sizeX,  sizeY);

        for (int i = 0; i < 4; i++)
        {
            float rx = v[i].x * cos + v[i].y * sin;
            float ry = -v[i].x * sin + v[i].y * cos;
            GL.Vertex3(pos.x + rx, pos.y + ry, 0);
        }

        GL.End();
        GL.PopMatrix();
    }

    // 清空畫布（Inspector 右鍵可呼叫）
    [ContextMenu("Clear Board")]
    public void ClearBoard()
    {
        if (renderTexture == null) return;
        var prev = RenderTexture.active;
        RenderTexture.active = renderTexture;
        GL.Clear(true, true, backGroundColor);
        RenderTexture.active = prev;

        // 重置筆狀態
        foreach (var b in brushes)
        {
            b.isFirstDraw = true;
            b.isDrawing = false;
        }
    }

    static void EnsureRTSettings(RenderTexture rt)
    {
        rt.filterMode = FilterMode.Bilinear;
        rt.wrapMode = TextureWrapMode.Clamp;
        // 建議在 Inspector 令 antiAliasing=1
    }
}
