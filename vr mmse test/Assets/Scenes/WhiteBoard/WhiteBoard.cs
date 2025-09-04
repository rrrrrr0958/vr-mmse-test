using System.Collections.Generic;
using UnityEngine;

public class WhiteBoard : MonoBehaviour
{
    [Tooltip("The Render Texture to draw on")]
    public RenderTexture renderTexture;

    [Header("BrushSettings")]
    [Tooltip("Max distance for brush detection")]
    public float maxDistance = 0.2f;
    [Tooltip("Minimum distance between brush positions (pixels)")]
    public float minBrushDistance = 2f;

    private Material brushMaterial; // Material used for GL drawing

    public Color backGroundColor = Color.white;  // 只用來初始化清空畫布
    [Range(0, 1)]
    public float markerAlpha = 0.7f;

    [Header("Collider Type")]
    [Tooltip("Set false to use a MeshCollider for 3d objects")]
    public bool useBoxCollider = true;

    [System.Serializable]
    public class BrushSettings
    {
        [Header("Brush Object")]
        public Transform brushTransform;   // 筆尖 Transform（forward 指向白板）
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

        // 初始化畫布
        if (renderTexture != null)
        {
            // 建議確保 RT 設定（可選）
            EnsureRTSettings(renderTexture);

            RenderTexture.active = renderTexture;
            GL.Clear(true, true, backGroundColor);

            foreach (var b in brushes)
            {
                var c = b.color;
                c.a = markerAlpha; // 設定筆透明度
                b.color = c;
            }

            RenderTexture.active = null;
        }
        else
        {
            Debug.LogWarning("[WhiteBoard] renderTexture 未指定。");
        }
    }

    void Update()
    {
        if (renderTexture == null) return;

        RenderTexture.active = renderTexture;

        foreach (var brush in brushes)
        {
            if (brush.isHeld)
                DrawBrushOnTexture(brush);
        }

        RenderTexture.active = null;
    }

    void DrawBrushOnTexture(BrushSettings brush)
    {
        if (brush.brushTransform == null) return;

        Ray ray = new Ray(brush.brushTransform.position, brush.brushTransform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
        {
            if (hit.collider.gameObject == gameObject)
            {
                Vector2 uv;
                if (useBoxCollider)
                {
                    var box = GetComponent<BoxCollider>();
                    if (box == null)
                    {
                        Debug.LogError("缺少 BoxCollider，請改用 MeshCollider 或加上 BoxCollider。");
                        return;
                    }
                    Vector3 local = transform.InverseTransformPoint(hit.point);
                    uv = new Vector2(
                        (local.x / box.size.x) + 0.5f,
                        1.0f - ((local.y / box.size.y) + 0.5f)
                    );
                }
                else
                {
                    uv = hit.textureCoord;
                    uv.y = 1.0f - uv.y;
                }

                int x = (int)(uv.x * renderTexture.width);
                int y = (int)(uv.y * renderTexture.height);
                Vector2 current = new Vector2(x, y);

                if (!brush.isDrawing)
                {
                    brush.isFirstDraw = true;
                    brush.isDrawing = true;
                }

                if (brush.isFirstDraw)
                {
                    DrawAtPosition(current, brush.color, brush.sizeX, brush.sizeY, brush.brushTransform.rotation.eulerAngles.z);
                    brush.lastPosition = current;
                    brush.isFirstDraw = false;
                    return;
                }

                float dx = Mathf.Abs(current.x - brush.lastPosition.x);
                float dy = Mathf.Abs(current.y - brush.lastPosition.y);
                bool crossH = dx > renderTexture.width / 16;
                bool crossV = dy > renderTexture.height / 16;

                if (crossH || crossV)
                {
                    DrawAtPosition(current, brush.color, brush.sizeX, brush.sizeY, brush.brushTransform.rotation.eulerAngles.z);
                }
                else
                {
                    float dist = Vector2.Distance(current, brush.lastPosition);
                    int steps = Mathf.CeilToInt(dist / minBrushDistance);
                    for (int i = 1; i <= steps; i++)
                    {
                        Vector2 p = Vector2.Lerp(brush.lastPosition, current, i / (float)steps);
                        DrawAtPosition(p, brush.color, brush.sizeX, brush.sizeY, brush.brushTransform.rotation.eulerAngles.z);
                    }
                }

                brush.lastPosition = current;
            }
        }
        else
        {
            brush.isDrawing = false;
        }
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

    // 可選：確保 RT 設定
    static void EnsureRTSettings(RenderTexture rt)
    {
        // 這些屬性通常可在 Inspector 直接設；這裡只是示意
        rt.filterMode = FilterMode.Bilinear;
        rt.wrapMode = TextureWrapMode.Clamp;
        // 若 RT 是可重建的，確保 antiAliasing = 1
        //（部分情況需重建 RT 才會生效，或直接在 Inspector 設定好）
    }
}
