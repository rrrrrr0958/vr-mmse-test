using System.Collections.Generic;
using UnityEngine;

public class WhiteBoard : MonoBehaviour
{
    [Header("Draw Position Correction")]
    [Tooltip("調整筆跡對齊位置用")]
    public Vector2 drawOffset = Vector2.zero;   // (X, Y) 偏移量
    public Vector2 drawScale  = Vector2.one;    // (X, Y) 縮放比例

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

    [Range(0f, 1f)] public float markerAlpha = 1.0f;     // 筆跡不透明度
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

    // === 顯示材質與色彩空間修正 ===
    [Header("Board Material & Color Space Fix")]
    [Tooltip("把白板材質強制改成 Unlit，避免受光變灰")]
    public bool forceUnlitBoardMaterial = true;
    [Tooltip("清空/繪製 RenderTexture 時自動處理 sRGB 寫入（Linear 專案建議開）")]
    public bool fixSRGBWriteForRT = true;

    // === 紅點貼面視覺游標（解決側看視差） ===
    [Header("Cursor (Red Dot)")]
    [Tooltip("紅色點點的 Transform（可留空；若 Auto Create 開啟會自動生成）")]
    public Transform cursorDot;
    [Range(0f, 0.02f), Tooltip("避免Z-fighting的微量抬起距離")]
    public float cursorLift = 0.0015f;
    [Tooltip("是否在執行時自動生成紅點物件")]
    public bool autoCreateCursorDot = true;

    [Tooltip("紅點的世界直徑（公尺；不受父物件縮放）")]
    public float cursorSize = 0.03f;

    [Tooltip("紅點顏色")]
    public Color cursorColor = new Color(1f, 0f, 0f, 1f);

    [Tooltip("紅點額外亮度/飽和加成（>1 更醒目）")]
    public float cursorHDRBoost = 1.5f;

    [Tooltip("紅點材質強制 Opaque（避免看起來半透明）")]
    public bool cursorForceOpaque = true;

    [Header("Cursor Plane Snap (for BoxCollider)")]
    [Tooltip("將紅點投影到 Renderer 的可見平面（BoxCollider 仍在用時建議開）")]
    public bool snapCursorToRendererPlane = true;
    [Tooltip("若可見表面不在 pivot 平面，可沿 forward 微調（公尺）")]
    public float rendererPlaneOffset = 0f;

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

    // === 筆跡顏色增益 & 不透明控制 ===
    [Header("Brush Color/Opacity")]
    [Tooltip("對每筆顏色的飽和度做乘法增益（1=不變）")]
    public float brushSaturationBoost = 1.2f;
    [Tooltip("對每筆顏色的亮度做乘法增益（1=不變）")]
    public float brushValueBoost = 1.1f;

    [Tooltip("強制讓筆跡中心完全不透明（alpha=1），並縮小邊緣過渡")]
    public bool strokeForceOpaque = true;
    [Range(0.01f, 0.2f), Tooltip("不透明時的邊緣過渡寬度，越小越實")]
    public float strokeEdgeWidthWhenOpaque = 0.06f;

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
        // 先不要覆蓋不透明模式的設定（見 SyncBrushMaterialParams）
        SyncBrushMaterialParams();

        // 將畫布掛到 Renderer（若有）並強制 Unlit 顯示
var r = GetComponent<Renderer>();
if (r && renderTexture)
{
    ApplyRenderTextureToRenderer(r, renderTexture, forceUnlitBoardMaterial);
}

        if (renderTexture != null)
        {
            EnsureRTSettings(renderTexture);

            var prev = RenderTexture.active;
            RenderTexture.active = renderTexture;

            bool s = PushSRGBWriteIfNeeded();
            GL.Clear(true, true, backGroundColor);
            PopSRGBWrite(s);

            foreach (var b in brushes)
            {
                var c = b.color; c.a = markerAlpha; b.color = c;
                b.isFirstDraw = true; b.isDrawing = false; b.lastPosition = Vector2.zero;
            }

            RenderTexture.active = prev;
        }
        else
        {
            Debug.LogWarning("[WhiteBoard] renderTexture 未指定。");
        }

        // 自動生成紅點（若需要）
        if (autoCreateCursorDot && cursorDot == null)
            EnsureCursorDot();

        if (cursorDot)
        {
            cursorDot.gameObject.SetActive(false);
            SetCursorWorldSize(cursorSize); // 以世界尺寸設定一次
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

            var r = GetComponent<Renderer>();
    if (r)
    {
        // 讀 sharedMaterial 避免每幀意外產生材質實例
        var m = r.sharedMaterial;
        bool needFix = false;

        if (m != null)
        {
            if (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != renderTexture) needFix = true;
            if (m.HasProperty("_MainTex") && m.GetTexture("_MainTex") != renderTexture) needFix = true;
        }
        else
        {
            needFix = true; // 沒材質也算需要修
        }

        if (needFix)
        {
            ApplyRenderTextureToRenderer(r, renderTexture, forceUnlitBoardMaterial);
        }
    }

        // 同步材質參數（但不覆蓋不透明模式）
        SyncBrushMaterialParams();

        // 即時套用 Inspector 的紅點大小（不依賴是否命中）
        if (cursorDot) SetCursorWorldSize(cursorSize);

        var prev = RenderTexture.active;
        RenderTexture.active = renderTexture;

        bool anyHitThisFrame = false; // 控制紅點顯示/隱藏

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

                    // === 紅點貼面：位置、朝向、尺寸、顏色 ===
                    if (cursorDot)
                    {
                        Vector3 pos = hit.point;
                        Vector3 nrm = hit.normal;

                        if (snapCursorToRendererPlane)
                        {
                            var rend = GetComponent<Renderer>();
                            if (rend)
                            {
                                Plane plane = new Plane(rend.transform.forward, rend.transform.position + rend.transform.forward * rendererPlaneOffset);
                                float d = plane.GetDistanceToPoint(pos);
                                pos = pos - plane.normal * d;
                                nrm = plane.normal;
                            }
                        }

                        cursorDot.gameObject.SetActive(true);
                        cursorDot.position = pos + nrm * cursorLift;
                        cursorDot.rotation = Quaternion.LookRotation(-nrm, Vector3.up);

                        // 顏色/亮度（使用 material 實例，避免共用材質被覆蓋）
                        var mr = cursorDot.GetComponent<MeshRenderer>();
                        if (mr)
                        {
                            var mat = mr.material;
                            Color boosted = BoostHSV(cursorColor, brushSaturationBoost, Mathf.Max(1f, cursorHDRBoost));
                            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", boosted);
                            if (mat.HasProperty("_Color"))     mat.SetColor("_Color", boosted);
                            if (mat.HasProperty("_EmissionColor"))
                            {
                                mat.SetColor("_EmissionColor", boosted * cursorHDRBoost);
                                mat.EnableKeyword("_EMISSION");
                            }
                            if (cursorForceOpaque) MakeMaterialOpaque(mat);
                        }
                    }
                    anyHitThisFrame = true;
                }
            }
            else
            {
                brush.isDrawing = false;
                brush.isFirstDraw = true;
            }
        }

        // 若本幀沒有命中白板，就隱藏紅點
        if (!anyHitThisFrame && cursorDot) cursorDot.gameObject.SetActive(false);

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

        // 同步紅點（可選）
        if (cursorDot)
        {
            Vector3 pos = hit.point;
            Vector3 nrm = hit.normal;

            if (snapCursorToRendererPlane)
            {
                var rend = GetComponent<Renderer>();
                if (rend)
                {
                    Plane plane = new Plane(rend.transform.forward, rend.transform.position + rend.transform.forward * rendererPlaneOffset);
                    float d = plane.GetDistanceToPoint(pos);
                    pos = pos - plane.normal * d;
                    nrm = plane.normal;
                }
            }

            cursorDot.gameObject.SetActive(true);
            cursorDot.position = pos + nrm * cursorLift;
            cursorDot.rotation = Quaternion.LookRotation(-nrm, Vector3.up);
            SetCursorWorldSize(cursorSize);

            var mr = cursorDot.GetComponent<MeshRenderer>();
            if (mr)
            {
                var mat = mr.material;
                Color boosted = BoostHSV(cursorColor, brushSaturationBoost, Mathf.Max(1f, cursorHDRBoost));
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", boosted);
                if (mat.HasProperty("_Color"))     mat.SetColor("_Color", boosted);
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.SetColor("_EmissionColor", boosted * cursorHDRBoost);
                    mat.EnableKeyword("_EMISSION");
                }
                if (cursorForceOpaque) MakeMaterialOpaque(mat);
            }
        }
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

        int x = Mathf.Clamp((int)((uv.x + drawOffset.x) * drawScale.x * renderTexture.width), 0, renderTexture.width - 1);
        int y = Mathf.Clamp((int)((uv.y + drawOffset.y) * drawScale.y * renderTexture.height), 0, renderTexture.height - 1);
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

        // 顏色增益
        color = BoostHSV(color, brushSaturationBoost, brushValueBoost);

        // 筆跡不透明控制：中心實心（alpha=1），邊緣極薄過渡
        if (strokeForceOpaque)
        {
            color.a = 1f;
            brushMaterial.SetFloat("_EdgeWidth", Mathf.Clamp(strokeEdgeWidthWhenOpaque, 0.01f, 0.2f));
            brushMaterial.SetFloat("_Hardness", 1f);
        }
        else
        {
            color.a = markerAlpha;
            brushMaterial.SetFloat("_EdgeWidth", Mathf.Clamp(edgeWidth, 0.01f, 0.5f));
            brushMaterial.SetFloat("_Hardness",  Mathf.Clamp01(hardness));
        }

        // 套顏色
        brushMaterial.SetColor("_Color", color);

        // sRGB 寫入修正
        bool s = PushSRGBWriteIfNeeded();

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

        // 還原 sRGB 狀態
        PopSRGBWrite(s);
    }

    // 清空畫布（Inspector 右鍵可呼叫）
    [ContextMenu("Clear Board")]
    public void ClearBoard()
    {
        if (renderTexture == null) return;
        var prev = RenderTexture.active;
        RenderTexture.active = renderTexture;

        bool s = PushSRGBWriteIfNeeded();
        GL.Clear(true, true, backGroundColor);
        PopSRGBWrite(s);

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

        // 若強制不透明，邊緣硬化的數值交給 DrawAtPosition 控制，不在這裡覆寫
        if (!strokeForceOpaque)
        {
            brushMaterial.SetFloat("_EdgeWidth", Mathf.Clamp(edgeWidth, 0.01f, 0.5f));
            brushMaterial.SetFloat("_Hardness",  Mathf.Clamp01(hardness));
        }
    }

    static void EnsureRTSettings(RenderTexture rt)
    {
        rt.filterMode = FilterMode.Bilinear;   // 放大時較不鋸齒
        rt.wrapMode   = TextureWrapMode.Clamp;
        // 不依賴 MSAA；antiAliasing=1 即可
    }

    // ===== sRGB Write 切換（Linear 專案避免偏灰） =====
    bool PushSRGBWriteIfNeeded()
    {
        if (!fixSRGBWriteForRT) return false;
#if UNITY_2017_1_OR_NEWER
        bool should = (QualitySettings.activeColorSpace == ColorSpace.Linear);
        if (should)
        {
            GL.sRGBWrite = true;
            return true;
        }
#endif
        return false;
    }
    void PopSRGBWrite(bool changed)
    {
#if UNITY_2017_1_OR_NEWER
        if (changed) GL.sRGBWrite = false;
#endif
    }
    void ApplyRenderTextureToRenderer(Renderer r, RenderTexture rt, bool forceUnlit)
{
    if (!r || !rt) return;

    Material mat;
    if (forceUnlit)
    {
        // 建 Unlit 實例，確保不吃光
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Unlit/Texture");
        mat = new Material(sh);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     Color.white);
    }
    else
    {
        // 用現有材質的實例
        mat = new Material(r.sharedMaterial ? r.sharedMaterial : r.material);
    }

    // ★ 關鍵：同時設定 URP 與 Built-in 的貼圖欄位
    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", rt);   // URP
    if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", rt);   // Built-in
    r.material = mat;

    // 不要影子影響亮度
    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    r.receiveShadows    = false;
}


    // 依「世界直徑(公尺)」設定紅點大小，抵銷父物件縮放
    void SetCursorWorldSize(float worldDiameter)
    {
        if (!cursorDot) return;
        worldDiameter = Mathf.Max(0.001f, worldDiameter);

        Vector3 parentLossy = Vector3.one;
        if (cursorDot.parent != null) parentLossy = cursorDot.parent.lossyScale;

        float sx = worldDiameter / (Mathf.Approximately(parentLossy.x, 0f) ? 1f : parentLossy.x);
        float sy = worldDiameter / (Mathf.Approximately(parentLossy.y, 0f) ? 1f : parentLossy.y);
        float sz = worldDiameter / (Mathf.Approximately(parentLossy.z, 0f) ? 1f : parentLossy.z);
        cursorDot.localScale = new Vector3(sx, sy, sz);
    }

    // 在 Inspector 改 cursorSize 時即時更新
    void OnValidate()
    {
        if (cursorDot != null)
            SetCursorWorldSize(cursorSize);
    }

    // 自動建立紅點物件（Unlit 紅色小Quad）
    void EnsureCursorDot()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "CursorDot (auto)";
        go.transform.localScale = Vector3.one; // 真正大小用 SetCursorWorldSize 控制

        // 刪除Collider避免擋到Ray
        var col = go.GetComponent<Collider>();
        if (col) Destroy(col);

        // 指定 Unlit 紅色材質（URP 或內建）
        var mr = go.GetComponent<MeshRenderer>();
        Material mat = null;
        var urp = Shader.Find("Universal Render Pipeline/Unlit");
        if (urp != null)
        {
            mat = new Material(urp);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", cursorColor);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", cursorColor);
        }
        else
        {
            var builtIn = Shader.Find("Unlit/Color");
            mat = new Material(builtIn);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", cursorColor);
        }
        mr.material = mat; // 用實例
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        cursorDot = go.transform;
        cursorDot.gameObject.SetActive(false);

        SetCursorWorldSize(cursorSize);

        if (cursorForceOpaque) MakeMaterialOpaque(mr.material);
    }

    // 將 RGB 顏色做 HSV 增益（S、V 乘上係數），並 clamp 到 [0,1]
    static Color BoostHSV(Color c, float sMul, float vMul)
    {
        Color.RGBToHSV(new Color(c.r, c.g, c.b, 1f), out float h, out float s, out float v);
        s = Mathf.Clamp01(s * Mathf.Max(0f, sMul));
        v = Mathf.Clamp01(v * Mathf.Max(0f, vMul));
        Color rgb = Color.HSVToRGB(h, s, v);
        rgb.a = c.a; // alpha 由外部控制
        return rgb;
    }

    // 將 URP/Built-in 材質強制 Opaque（避免任何 alpha 混合）
    static void MakeMaterialOpaque(Material m)
    {
        if (m == null) return;

        // URP: 0=Opaque, 1=Transparent
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 0f);
        m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.EnableKeyword("_SURFACE_TYPE_OPAQUE");

        // 關閉常見透明關鍵字
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.DisableKeyword("_ALPHATEST_ON");
        m.DisableKeyword("_ALPHABLEND_ON");

        // Blend One, Zero (= 不混合)、ZWrite 開、RenderQueue Opaque
        if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
        if (m.HasProperty("_ZWrite"))   m.SetInt("_ZWrite", 1);
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
    }
}
