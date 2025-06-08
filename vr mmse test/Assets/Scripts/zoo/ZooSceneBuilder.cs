using UnityEngine;

public class ZooSceneBuilder : MonoBehaviour
{
    public Material grassGroundMaterial, waterMaterial, fenceMaterial, stoneMaterial;
    public Material frogMaterial, cowMaterial, sheepMaterial, duckMaterial;
    //public Material customSkyboxMaterial; // 仍然需要這個來拖曳天空盒材質

    public GameObject sheepPrefab; // 新增一個 GameObject 變數來引用綿羊 Prefab

    void Awake()
    {
        // 初始化所有材質，確保即使未從 Inspector 拖曳，也有預設顏色
        InitMaterials();

        // 創建地面
        CreateGround();

        // 設置定向光源 (太陽)
        SetupDirectionalLight();

        // --- 核心改動：將動物區域排列成半圓形，調整距離和間距 ---
        // 假設玩家中心點為 (0,0,0)

        // *** 調整點一：進一步縮小 radius (半徑) 讓動物更靠近玩家 ***
        float radius = 7f;
        float[] angles = { -50f, -20f, 20f, 50f };


        // 青蛙
        Vector3 frogPos = CalculateSemiCirclePosition(radius, angles[0]);
        CreateFrogPondArea(new Vector3(frogPos.x, 0.0f, frogPos.z));

        Vector3 cowPos = CalculateSemiCirclePosition(radius, angles[1]);
        CreateCowArea(new Vector3(cowPos.x, 0.0f, cowPos.z));

        Vector3 sheepPos = CalculateSemiCirclePosition(radius, angles[2]);
        CreateSheepArea(new Vector3(sheepPos.x, 0.0f, sheepPos.z));

        Vector3 duckPos = CalculateSemiCirclePosition(radius, angles[3]);
        CreateDuckPondArea(new Vector3(duckPos.x, 0.0f, duckPos.z));
    }

    /// <summary>
    /// 根據半徑和角度計算半圓上的位置。
    /// 0 度指向 Z 軸正方向，角度順時針為負，逆時針為正。
    /// </summary>
    /// <param name="radius">半圓的半徑。</param>
    /// <param name="angleDegrees">角度 (度)。</param>
    /// <returns>在半圓上的 Vector3 位置 (Y 軸為 0)。</returns>
    Vector3 CalculateSemiCirclePosition(float radius, float angleDeg)
    {
        float angleRad = angleDeg * Mathf.Deg2Rad;
        float x = radius * Mathf.Sin(angleRad);
        float z = radius * Mathf.Cos(angleRad);
        return new Vector3(x, 0f, z);
    }


    void InitMaterials()
    {
        // 確保所有材質都有預設顏色，如果沒有在 Inspector 中指定的話
        if (grassGroundMaterial == null) grassGroundMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.4f, 0.7f, 0.3f) }; // 深草綠
        if (waterMaterial == null) waterMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.3f, 0.6f, 0.8f, 0.7f) }; // 淺藍，帶一點透明度
        if (fenceMaterial == null) fenceMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.4f, 0.2f, 0.1f) }; // 棕色
        if (stoneMaterial == null) stoneMaterial = new Material(Shader.Find("Standard")) { color = Color.gray }; // 灰色

        if (frogMaterial == null) frogMaterial = new Material(Shader.Find("Standard")) { color = Color.green }; // 純綠
        if (cowMaterial == null) cowMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.6f, 0.3f, 0.1f) }; // 棕色
        if (sheepMaterial == null) sheepMaterial = new Material(Shader.Find("Standard")) { color = Color.white }; // 純白
        if (duckMaterial == null) duckMaterial = new Material(Shader.Find("Standard")) { color = Color.yellow }; // 純黃
    }

    void CreateGround()
    {
        GameObject ground = GameObject.Find("GrassGround");
        if (ground == null)
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "GrassGround";
            ground.transform.position = Vector3.zero; // 確保地面在原點
            ground.transform.localScale = new Vector3(10, 1, 10); // 調整大小以覆蓋足夠區域
        }

        // 移除 Plane 上可能存在的舊腳本 (如 BackgroundGrass)
        MonoBehaviour[] scripts = ground.GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour script in scripts)
        {
            if (script.GetType().Name == "BackgroundGrass")
            {
                Destroy(script);
                Debug.Log("已移除 GrassGround 上的 BackgroundGrass 腳本。");
            }
        }
        ground.GetComponent<Renderer>().material = grassGroundMaterial;
    }

    void SetupDirectionalLight()
    {
        Light light = FindObjectOfType<Light>();
        // 如果場景中沒有燈光或者不是定向光，則創建一個
        if (light == null || light.type != LightType.Directional)
        {
            GameObject go = new GameObject("Directional Light");
            light = go.AddComponent<Light>();
            light.type = LightType.Directional;
        }
        // 設定太陽的旋轉角度 (影響陰影方向)
        light.transform.rotation = Quaternion.Euler(50, -30, 0);
        light.color = Color.white;
        light.intensity = 1f; // 亮度
    }

    void CreateFence(Transform parent, float size)
    {
        float half = size / 2f;
        float spacing = 0.7f;
        // 創建圍欄柱子
        for (float x = -half; x <= half + 0.001f; x += spacing)
        {
            CreatePost(parent, new Vector3(x, 0, -half), 1f); // 前排
            CreatePost(parent, new Vector3(x, 0, +half), 1f); // 後排
        }
        for (float z = -half + spacing; z < half; z += spacing) // 避免重複角上的柱子
        {
            CreatePost(parent, new Vector3(-half, 0, z), 1f); // 左排
            CreatePost(parent, new Vector3(+half, 0, z), 1f); // 右排
        }
        // 創建圍欄橫桿
        CreateRail(parent, new Vector3(0, 0.6f, -half), new Vector3(size, 0.1f, 0.1f));
        CreateRail(parent, new Vector3(0, 0.6f, +half), new Vector3(size, 0.1f, 0.1f));
        CreateRail(parent, new Vector3(-half, 0.6f, 0), new Vector3(0.1f, 0.1f, size));
        CreateRail(parent, new Vector3(+half, 0.6f, 0), new Vector3(0.1f, 0.1f, size));
    }

    void CreatePost(Transform parent, Vector3 pos, float h)
    {
        GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        post.transform.SetParent(parent);
        post.transform.localPosition = pos + Vector3.up * (h / 2f);
        post.transform.localScale = new Vector3(0.1f, h / 2f, 0.1f);
        post.GetComponent<Renderer>().material = fenceMaterial;
    }

    void CreateRail(Transform parent, Vector3 pos, Vector3 scale)
    {
        GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rail.transform.SetParent(parent);
        rail.transform.localPosition = pos;
        rail.transform.localScale = scale;
        rail.GetComponent<Renderer>().material = fenceMaterial;
    }

    void CreateStone(Transform parent, Vector3 pos, Vector3 scale)
    {
        GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        s.transform.SetParent(parent);
        s.transform.localPosition = pos;
        s.transform.localScale = scale;
        s.GetComponent<Renderer>().material = stoneMaterial;
    }

    void CreateFrogPondArea(Vector3 pos)
    {
        GameObject area = new GameObject("FrogArea");
        area.transform.position = pos;
        GameObject pond = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pond.transform.SetParent(area.transform);
        pond.transform.localPosition = new Vector3(0, -0.05f, 0);
        pond.transform.localScale = new Vector3(2, 0.1f, 2);
        pond.GetComponent<Renderer>().material = waterMaterial;

        GameObject frog = GameObject.CreatePrimitive(PrimitiveType.Sphere); // 青蛙簡化為球體
        frog.transform.SetParent(area.transform);
        frog.transform.localPosition = new Vector3(0, 0.2f, 0); // 讓青蛙在水面上
        frog.transform.localScale = Vector3.one * 0.3f;
        frog.GetComponent<Renderer>().material = frogMaterial;

        CreateStone(area.transform, new Vector3(0.8f, 0, 0.8f), new Vector3(0.4f, 0.2f, 0.4f));
        //CreateFence(area.transform, 3f); // 圍欄尺寸為 3x3
    }

    void CreateCowArea(Vector3 pos)
    {
        GameObject area = new GameObject("CowArea");
        area.transform.position = pos;
        GameObject cow = GameObject.CreatePrimitive(PrimitiveType.Capsule); // 牛簡化為膠囊體
        cow.transform.SetParent(area.transform);
        cow.transform.localPosition = new Vector3(0, 0.5f, 0); // 站在草地上
        cow.transform.localScale = new Vector3(1f, 1f, 1f);
        cow.GetComponent<Renderer>().material = cowMaterial;

        CreateFence(area.transform, 3f); // 圍欄尺寸為 3x3
    }

    void CreateSheepArea(Vector3 pos)
    {
        GameObject area = new GameObject("SheepArea");
        area.transform.position = pos;

        // --- MODIFIED SHEEP POSITIONS (第二次調整) ---
        // 再次調整後的羊位置，整體往前移動，讓它們更靠近圍欄中央偏前的位置
        Vector3[] sheepPositions = new Vector3[]
        {
            new Vector3(-0.6f, 0.4f, -0.8f),    // 第一隻羊，靠左後方
            new Vector3(0.0f, 0.4f, -0.2f),     // 第二隻羊，中間偏後方
            new Vector3(0.6f, 0.4f, -0.8f)      // 第三隻羊，靠右後方
            // Y 軸高度 0.4f 應該能讓羊站在地面上，如果羊腳陷入地面，請稍微調高
        };

        float sheepScale = 0.8f; // 羊的大小比例

        if (sheepPrefab != null)
        {
            foreach (Vector3 sheepLocalPos in sheepPositions)
            {
                GameObject sheep = Instantiate(sheepPrefab, area.transform);
                sheep.transform.localPosition = sheepLocalPos;
                sheep.transform.localScale = Vector3.one * sheepScale;
                // 您可能還想調整羊的旋轉，讓它們面向不同方向
                // sheep.transform.localRotation = Quaternion.Euler(0, Random.Range(0, 360), 0); // 隨機旋轉
            }
        }
        else
        {
            Debug.LogWarning("Sheep Prefab is not assigned. Using default Sphere for SheepArea.");
            GameObject sheep = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sheep.transform.SetParent(area.transform);
            sheep.transform.localPosition = new Vector3(0, 0.4f, 0);
            sheep.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
            sheep.GetComponent<Renderer>().material = sheepMaterial;
        }

        CreateFence(area.transform, 3f); // 圍欄尺寸為 3x3
    

    // 如果 sheepPrefab 已在 Inspector 中指定，則實例化它
    //if (sheepPrefab != null)
    //{
    //    GameObject sheep = Instantiate(sheepPrefab, area.transform);
    //    sheep.transform.localPosition = new Vector3(0, 0.4f, 0); // 調整位置讓羊站在地面上
    //    sheep.transform.localScale = Vector3.one * 0.8f; // 根據需要調整大小
    //    // 如果 Prefab 已經有自己的材質，就不要再設定 sheepMaterial 了
    //    // 如果 Prefab 沒有材質，你可以在此設定預設材質
    //    // sheep.GetComponent<Renderer>().material = sheepMaterial;
    //}
    //else
    //{
    //    // 如果沒有指定 Prefab，則退回使用球體作為預設
    //    GameObject sheep = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    //    sheep.transform.SetParent(area.transform);
    //    sheep.transform.localPosition = new Vector3(0, 0.4f, 0); // 站在草地上
    //    sheep.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
    //    sheep.GetComponent<Renderer>().material = sheepMaterial;
    //    Debug.LogWarning("Sheep Prefab is not assigned. Using default Sphere for SheepArea.");
    //}

    //CreateFence(area.transform, 3f); // 圍欄尺寸為 3x3
}

    void CreateDuckPondArea(Vector3 pos)
    {
        GameObject area = new GameObject("DuckArea");
        area.transform.position = pos;
        GameObject pond = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pond.transform.SetParent(area.transform);
        pond.transform.localPosition = new Vector3(0, -0.05f, 0);
        pond.transform.localScale = new Vector3(1.8f, 0.1f, 1.8f);
        pond.GetComponent<Renderer>().material = waterMaterial;

        GameObject duck = GameObject.CreatePrimitive(PrimitiveType.Sphere); // 鴨子簡化為球體
        duck.transform.SetParent(area.transform);
        duck.transform.localPosition = new Vector3(0.3f, 0.2f, 0.3f); // 讓鴨子在水面上
        duck.transform.localScale = Vector3.one * 0.25f;
        duck.GetComponent<Renderer>().material = duckMaterial;

        CreateStone(area.transform, new Vector3(-0.6f, 0, 0.6f), new Vector3(0.3f, 0.15f, 0.3f));
        //CreateFence(area.transform, 3f); // 圍欄尺寸為 3x3
    }
}