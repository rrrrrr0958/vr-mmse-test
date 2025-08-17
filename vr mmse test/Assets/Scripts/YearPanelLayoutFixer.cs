using UnityEngine;
using UnityEngine.UI;

namespace StudyApp
{
    public class YearPanelLayoutFixer : MonoBehaviour
    {
        void Start()
        {
            FixYearPanelLayout();
        }

        [ContextMenu("Fix Year Panel Layout")]
        public void FixYearPanelLayout()
        {
            Debug.Log("開始修復年份面板排列...");
            
            // 找到Panel_Year物件 - 使用instanceID
            GameObject panelYear = GetGameObjectByInstanceID(47486);
            if (panelYear == null)
            {
                Debug.LogError("無法找到Panel_Year (ID: 47486)");
                return;
            }

            Debug.Log($"找到Panel_Year: {panelYear.name}");

            // 取得GridLayoutGroup組件
            GridLayoutGroup gridLayout = panelYear.GetComponent<GridLayoutGroup>();
            if (gridLayout == null)
            {
                Debug.LogError("Panel_Year 沒有GridLayoutGroup組件");
                return;
            }

            // 設置為橫列排列 (固定行數為1)
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedRowCount;
            gridLayout.constraintCount = 1;
            
            // 可選：調整間距
            gridLayout.spacing = new Vector2(10f, 10f);
            
            Debug.Log("Year Panel 已設置為橫列排列");
            
            // 檢查目前的設置
            Debug.Log($"Constraint: {gridLayout.constraint}");
            Debug.Log($"Constraint Count: {gridLayout.constraintCount}");
            
            // 強制重新計算佈局
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelYear.GetComponent<RectTransform>());
        }

        private GameObject GetGameObjectByInstanceID(int instanceID)
        {
            // 這個方法可能無法工作，所以我們改用其他方法
            GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (GameObject obj in allObjects)
            {
                if (obj.GetInstanceID() == instanceID)
                {
                    return obj;
                }
            }
            return null;
        }
    }
}