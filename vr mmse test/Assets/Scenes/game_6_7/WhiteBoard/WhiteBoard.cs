using System.Collections.Generic;
using UnityEngine;

public class WhiteBoard : MonoBehaviour
{
    [Tooltip("The Render Texture to draw on")]
    public RenderTexture renderTexture;

    [Header("Brush Input")]
    [Tooltip("Max distance for brush detection (for physical pen mode)")]
    public float maxDistance = 0.2f;

    [Tooltip("Minimum distance between brush positions (pixels)")]
    public float minBrushDistance = 1f;

    [Header("Soft Brush (No MSAA Needed)")]
    [Tooltip("Assign the shader 'Hidden/SoftBrushRadial'. Leave empty to auto-find.")]
    public Shader softBrushShader;          // 指向 Hidden/SoftBrushRadial（可留空自動找）
    private Material brushMaterial;         // 柔邊筆刷材質（GL 疊加）

    [Range(0f, 1f)] public float markerAlpha = 1.0f;     // 筆跡不透明度（建議 0.9~1.0 更銳利）
    [Range(0.01f, 0.5f)] public float edgeWidth = 0.12f; // 邊緣過渡寬度（越小越銳）
    [Range(0f, 1f)] public float hardness  = 0.8f;       // 邊緣硬度（越大越硬）
    [Range(0f, 1f)] public float smoothFactor = 0.25f;   // 位置平滑（0=關；0.2~0.3 推薦）

    [Header("Board")]
    public Color backGroundColor = Color.white;          // 初始化清空畫布
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
        // 建立柔邊筆刷材質
        if (softBrushShader == null)
            softBrushShader = Shader.Find("Hidden/SoftBrushRadial");
        if (softBrushShader == null)
        {
            Debug.LogError("[WhiteBoard] 找不到 Shader 'Hidden/SoftBrushRadial'，請先加入對應 shader。");
            enabled = false; return;
        }
        brushMaterial = new Material(softBrushShader);
        SyncBrushMaterialParams();

        // 將畫布掛到 Renderer（若有）
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
                // 套用整體 alpha 至每支筆的顏色
                var c = b.color; c.a = markerAlpha; b.color = c;
                b.isFirstDraw = true; b.isDrawing = false; b.lastPosition = Vector2.zero;
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
        if (renderTexture == null || brushMaterial == null) return;

        // 過程中若你在 Inspector 動態調整參數，這裡讓材質即時同步
        SyncBrushMaterialParams();

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
        if (renderTexture == null || brushMaterial == null) return;
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

    // 統一處理插值與落筆（含位置平滑）
    void ProcessStroke(BrushSettings brush, Vector2 current, float rotZDeg)
    {
        if (!brush.isDrawing)
        {
            brush.isFirstDraw = true;
            brush.isDrawing = true;
        }

        // 一階平滑（EMA）：抑制手抖與鋸齒擺動
        if (!brush.isFirstDraw)
            current = Vector2.Lerp(brush.lastPosition, current, Mathf.Clamp01(smoothFactor));

        if (brush.isFirstDraw)
        {
            DrawAtPosition(current, brush.color, brush.sizeX, brush.sizeY, rotZDeg);
            brush.lastPosition = current;
            brush.isFirstDraw = false;
            return;
        }

        // 永遠插值（避免斷點）
        float dist = Vector2.Distance(current, brush.lastPosition);
        int steps = Mathf.Max(1, Mathf.CeilToInt(dist / Mathf.Max(0.1f, minBrushDistance)));
        for (int i = 1; i <= steps; i++)
        {
            Vector2 p = Vector2.Lerp(brush.lastPosition, current, i / (float)steps);
            DrawAtPosition(p, brush.color, brush.sizeX, brush.sizeY, rotZDeg);
        }

        brush.lastPosition = current;
    }

    // 以「柔邊圓刷」貼合到畫布（GL + UV + 透明混合）
    void DrawAtPosition(Vector2 pos, Color color, float sizeX, float sizeY, float rotDeg)
    {
        if (brushMaterial == null) return;

        // 同步顏色與不透明度（每筆即時）
        color.a = markerAlpha;
        brushMaterial.SetColor("_Color", color);

        GL.PushMatrix();
        GL.LoadPixelMatrix(0, renderTexture.width, renderTexture.height, 0);

        // Shader 內已設 Blend SrcAlpha OneMinusSrcAlpha
        brushMaterial.SetPass(0);

        float rad = rotDeg * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);

        // 以筆心為中心的矩形（sizeX/sizeY 決定半徑；可形成橢圓）
        Vector2[] v = new Vector2[4];
        v[0] = new Vector2(-sizeX, -sizeY);
        v[1] = new Vector2( sizeX, -sizeY);
        v[2] = new Vector2( sizeX,  sizeY);
        v[3] = new Vector2(-sizeX,  sizeY);

        Vector2[] uv = new Vector2[4] {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };

        GL.Begin(GL.QUADS);
        for (int i = 0; i < 4; i++)
        {
            float rx = v[i].x * cos + v[i].y * sin;
            float ry = -v[i].x * sin + v[i].y * cos;

            GL.TexCoord2(uv[i].x, uv[i].y);   // 先給 UV
            GL.Vertex3(pos.x + rx, pos.y + ry, 0f);
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

        foreach (var b in brushes)
        {
            b.isFirstDraw = true;
            b.isDrawing = false;
            b.lastPosition = Vector2.zero;
        }
    }

    // ---- 小工具 ----
    void SyncBrushMaterialParams()
    {
        if (brushMaterial == null) return;
        brushMaterial.SetFloat("_EdgeWidth", Mathf.Clamp(edgeWidth, 0.01f, 0.5f));
        brushMaterial.SetFloat("_Hardness",  Mathf.Clamp01(hardness));
    }

    static void EnsureRTSettings(RenderTexture rt)
    {
        rt.filterMode = FilterMode.Bilinear;   // 放大時較不鋸齒
        rt.wrapMode   = TextureWrapMode.Clamp;
        // 不依賴 MSAA；antiAliasing=1 即可
    }
}
