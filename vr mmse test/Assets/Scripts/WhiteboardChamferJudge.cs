using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Whiteboard drawing evaluation using improved scoring algorithm
/// Integrates Hu-moments for main structure comparison (80%) and Coverage+Chamfer for details (20%)
/// </summary>
public class WhiteboardChamferJudge : MonoBehaviour
{
    [Header("Evaluation Settings")]
    [SerializeField] private float minimumBrushLength = 50f; // Minimum drawing length to avoid 0 score
    [SerializeField] private float structureWeight = 0.8f; // Hu-moments weight
    [SerializeField] private float detailWeight = 0.2f; // Coverage + Chamfer weight
    [SerializeField] private float coverageWeight = 0.5f; // Within detail weight
    [SerializeField] private float chamferWeight = 0.5f; // Within detail weight
    [SerializeField] private int dilationRadius = 3; // Template dilation radius for coverage calculation
    
    [Header("Template Data")]
    [SerializeField] private Texture2D templateImage; // Reference template
    [SerializeField] private List<Vector2> templateContour = new List<Vector2>(); // Template edge points
    
    [Header("Player Drawing Data")]
    [SerializeField] private List<Vector2> playerDrawingPoints = new List<Vector2>(); // Raw drawing strokes
    [SerializeField] private bool[,] playerBinaryImage; // Rasterized player drawing
    
    [Header("Debug UI")]
    [SerializeField] private Text scoreText;
    [SerializeField] private Text structureScoreText;
    [SerializeField] private Text coverageScoreText;
    [SerializeField] private Text chamferScoreText;
    [SerializeField] private Text brushLengthText;
    [SerializeField] private Toggle showDebugInfo = null;
    [SerializeField] private RawImage debugImageDisplay;
    
    [Header("Cached Results")]
    [SerializeField] private float lastTotalScore = 0f;
    [SerializeField] private float lastStructureScore = 0f;
    [SerializeField] private float lastCoverageScore = 0f;
    [SerializeField] private float lastChamferScore = 0f;
    [SerializeField] private float lastBrushLength = 0f;
    
    // Cached template data
    private ShapeMatcher.HuMoments templateHuMoments;
    private List<ShapeMatcher.Point2D> templateEdgePoints;
    private bool[,] templateDilatedMask; // For improved coverage calculation
    private bool isTemplateProcessed = false;

    void Start()
    {
        ProcessTemplate();
    }

    /// <summary>
    /// Process template image to extract features
    /// </summary>
    private void ProcessTemplate()
    {
        if (templateImage == null)
        {
            Debug.LogError("Template image not assigned!");
            return;
        }
        
        try
        {
            // Convert template to binary image
            bool[,] templateBinary = ConvertTextureToBinary(templateImage);
            
            // Extract edge points
            templateEdgePoints = ShapeMatcher.ExtractEdgePoints(templateBinary);
            
            if (templateEdgePoints.Count == 0)
            {
                Debug.LogError("Template image contains no edge points! Check if template has visible content.");
                return;
            }
            
            // Calculate Hu-moments for template
            templateHuMoments = ShapeMatcher.CalculateHuMoments(templateEdgePoints);
            
            // Create dilated template mask for coverage calculation
            templateDilatedMask = CreateDilatedMask(templateBinary, dilationRadius);
            
            // Convert edge points to Vector2 for backwards compatibility
            templateContour.Clear();
            foreach (var point in templateEdgePoints)
            {
                templateContour.Add(new Vector2(point.x, point.y));
            }
            
            isTemplateProcessed = true;
            Debug.Log($"Template processed successfully: {templateEdgePoints.Count} edge points, Hu-moments calculated");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error processing template: {e.Message}");
            isTemplateProcessed = false;
        }
    }

    /// <summary>
    /// Main evaluation function with new scoring algorithm
    /// </summary>
    public float EvaluateNow()
    {
        if (!isTemplateProcessed)
        {
            Debug.LogError("Template not processed! Call ProcessTemplate() first or assign templateImage.");
            return 0f;
        }
        
        if (templateEdgePoints == null || templateEdgePoints.Count == 0)
        {
            Debug.LogError("Template has no edge points! Check template image.");
            return 0f;
        }
        
        if (playerDrawingPoints == null || playerDrawingPoints.Count == 0)
        {
            Debug.LogWarning("No player drawing data!");
            return 0f;
        }
        
        // Calculate brush length
        var playerPoints = playerDrawingPoints.Select(v => new ShapeMatcher.Point2D(v)).ToList();
        lastBrushLength = ShapeMatcher.CalculateBrushLength(playerPoints);
        
        // Check minimum brush length requirement
        if (lastBrushLength < minimumBrushLength)
        {
            Debug.Log($"Drawing too short ({lastBrushLength:F1} < {minimumBrushLength}), score = 0");
            lastTotalScore = 0f;
            lastStructureScore = 0f;
            lastCoverageScore = 0f;
            lastChamferScore = 0f;
            UpdateDebugUI();
            return 0f;
        }
        
        // Rasterize player drawing if needed
        if (playerBinaryImage == null)
        {
            playerBinaryImage = RasterizeDrawing(playerDrawingPoints, templateImage.width, templateImage.height);
        }
        
        // Extract player edge points
        var playerEdgePoints = ShapeMatcher.ExtractEdgePoints(playerBinaryImage);
        
        if (playerEdgePoints.Count == 0)
        {
            Debug.LogWarning("Player drawing has no edge points!");
            lastTotalScore = 0f;
            lastStructureScore = 0f;
            lastCoverageScore = 0f;
            lastChamferScore = 0f;
            UpdateDebugUI();
            return 0f;
        }
        
        // Calculate structure score using Hu-moments (80% weight)
        var playerHuMoments = ShapeMatcher.CalculateHuMoments(playerEdgePoints);
        lastStructureScore = ShapeMatcher.CompareShapes(templateHuMoments, playerHuMoments);
        
        // Calculate detail scores (20% weight total)
        lastCoverageScore = CalculateImprovedCoverageScore(playerEdgePoints);
        lastChamferScore = CalculateStrictChamferScore(playerEdgePoints);
        
        // Combined detail score
        float detailScore = (lastCoverageScore * coverageWeight + lastChamferScore * chamferWeight);
        
        // Final weighted score
        lastTotalScore = (lastStructureScore * structureWeight + detailScore * detailWeight);
        lastTotalScore = Mathf.Clamp01(lastTotalScore);
        
        UpdateDebugUI();
        
        Debug.Log($"Evaluation Complete - Total: {lastTotalScore:F3}, Structure: {lastStructureScore:F3}, " +
                  $"Coverage: {lastCoverageScore:F3}, Chamfer: {lastChamferScore:F3}, Brush: {lastBrushLength:F1}");
        
        return lastTotalScore;
    }

    /// <summary>
    /// Improved coverage score with max(player_edges, template_dilated) as denominator
    /// </summary>
    private float CalculateImprovedCoverageScore(List<ShapeMatcher.Point2D> playerEdgePoints)
    {
        if (playerEdgePoints.Count == 0) return 0f;
        
        // Count template dilated points
        int templateDilatedPoints = 0;
        int height = templateDilatedMask.GetLength(0);
        int width = templateDilatedMask.GetLength(1);
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (templateDilatedMask[y, x])
                    templateDilatedPoints++;
            }
        }
        
        // Count overlapping points
        int overlappingPoints = 0;
        foreach (var point in playerEdgePoints)
        {
            int x = Mathf.RoundToInt(point.x);
            int y = Mathf.RoundToInt(point.y);
            
            if (x >= 0 && x < width && y >= 0 && y < height && templateDilatedMask[y, x])
            {
                overlappingPoints++;
            }
        }
        
        // Use max(player edges, template dilated) as denominator
        int denominator = Mathf.Max(playerEdgePoints.Count, templateDilatedPoints);
        
        if (denominator == 0) return 0f;
        
        float coverage = (float)overlappingPoints / denominator;
        return Mathf.Clamp01(coverage);
    }

    /// <summary>
    /// Stricter Chamfer distance calculation
    /// </summary>
    private float CalculateStrictChamferScore(List<ShapeMatcher.Point2D> playerEdgePoints)
    {
        if (playerEdgePoints.Count == 0 || templateEdgePoints.Count == 0) return 0f;
        
        // Calculate average minimum distances from player to template
        float totalPlayerToTemplate = 0f;
        int validPlayerPoints = 0;
        
        foreach (var playerPoint in playerEdgePoints)
        {
            float minDist = float.MaxValue;
            foreach (var templatePoint in templateEdgePoints)
            {
                float dx = playerPoint.x - templatePoint.x;
                float dy = playerPoint.y - templatePoint.y;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                
                if (dist < minDist)
                    minDist = dist;
            }
            
            if (minDist < float.MaxValue)
            {
                totalPlayerToTemplate += minDist;
                validPlayerPoints++;
            }
        }
        
        // Calculate average minimum distances from template to player
        float totalTemplateToPlayer = 0f;
        int validTemplatePoints = 0;
        
        foreach (var templatePoint in templateEdgePoints)
        {
            float minDist = float.MaxValue;
            foreach (var playerPoint in playerEdgePoints)
            {
                float dx = templatePoint.x - playerPoint.x;
                float dy = templatePoint.y - playerPoint.y;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                
                if (dist < minDist)
                    minDist = dist;
            }
            
            if (minDist < float.MaxValue)
            {
                totalTemplateToPlayer += minDist;
                validTemplatePoints++;
            }
        }
        
        // Strict Chamfer: use the maximum of both average distances
        float avgPlayerToTemplate = validPlayerPoints > 0 ? totalPlayerToTemplate / validPlayerPoints : 100f;
        float avgTemplateToPlayer = validTemplatePoints > 0 ? totalTemplateToPlayer / validTemplatePoints : 100f;
        
        float maxAvgDistance = Mathf.Max(avgPlayerToTemplate, avgTemplateToPlayer);
        
        // Convert distance to similarity score (0-1 range)
        // Using exponential decay with stricter parameters
        float similarity = Mathf.Exp(-maxAvgDistance * 0.1f);
        return Mathf.Clamp01(similarity);
    }

    /// <summary>
    /// Create dilated mask from binary image
    /// </summary>
    private bool[,] CreateDilatedMask(bool[,] binaryImage, int radius)
    {
        int height = binaryImage.GetLength(0);
        int width = binaryImage.GetLength(1);
        bool[,] dilated = new bool[height, width];
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (binaryImage[y, x])
                {
                    // Dilate around this point
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        for (int dx = -radius; dx <= radius; dx++)
                        {
                            int ny = y + dy;
                            int nx = x + dx;
                            
                            if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                            {
                                if (dx * dx + dy * dy <= radius * radius)
                                {
                                    dilated[ny, nx] = true;
                                }
                            }
                        }
                    }
                }
            }
        }
        
        return dilated;
    }

    /// <summary>
    /// Convert texture to binary image
    /// </summary>
    private bool[,] ConvertTextureToBinary(Texture2D texture)
    {
        bool[,] binary = new bool[texture.height, texture.width];
        
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                Color pixel = texture.GetPixel(x, y);
                // Consider pixel as filled if it's not transparent and not white
                binary[y, x] = pixel.a > 0.5f && (pixel.r < 0.9f || pixel.g < 0.9f || pixel.b < 0.9f);
            }
        }
        
        return binary;
    }

    /// <summary>
    /// Rasterize drawing points to binary image
    /// </summary>
    private bool[,] RasterizeDrawing(List<Vector2> drawingPoints, int width, int height)
    {
        bool[,] rasterized = new bool[height, width];
        
        if (drawingPoints.Count < 2) return rasterized;
        
        // Draw lines between consecutive points
        for (int i = 1; i < drawingPoints.Count; i++)
        {
            DrawLine(rasterized, drawingPoints[i-1], drawingPoints[i], width, height);
        }
        
        return rasterized;
    }

    /// <summary>
    /// Draw line between two points using Bresenham's algorithm
    /// </summary>
    private void DrawLine(bool[,] image, Vector2 start, Vector2 end, int width, int height)
    {
        int x0 = Mathf.RoundToInt(start.x);
        int y0 = Mathf.RoundToInt(start.y);
        int x1 = Mathf.RoundToInt(end.x);
        int y1 = Mathf.RoundToInt(end.y);
        
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;
        
        while (true)
        {
            if (x0 >= 0 && x0 < width && y0 >= 0 && y0 < height)
            {
                image[y0, x0] = true;
            }
            
            if (x0 == x1 && y0 == y1) break;
            
            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    /// <summary>
    /// Update debug UI elements
    /// </summary>
    private void UpdateDebugUI()
    {
        if (scoreText != null)
            scoreText.text = $"Total Score: {lastTotalScore:F3}";
        
        if (structureScoreText != null)
            structureScoreText.text = $"Structure (Hu): {lastStructureScore:F3}";
        
        if (coverageScoreText != null)
            coverageScoreText.text = $"Coverage: {lastCoverageScore:F3}";
        
        if (chamferScoreText != null)
            chamferScoreText.text = $"Chamfer: {lastChamferScore:F3}";
        
        if (brushLengthText != null)
            brushLengthText.text = $"Brush Length: {lastBrushLength:F1}";
    }

    /// <summary>
    /// Public methods for external access
    /// </summary>
    public void SetPlayerDrawing(List<Vector2> points)
    {
        playerDrawingPoints = points;
        playerBinaryImage = null; // Reset cached binary image
    }
    
    public void SetTemplateImage(Texture2D template)
    {
        templateImage = template;
        isTemplateProcessed = false;
        ProcessTemplate();
    }
    
    public float GetLastScore() => lastTotalScore;
    public float GetStructureScore() => lastStructureScore;
    public float GetCoverageScore() => lastCoverageScore;
    public float GetChamferScore() => lastChamferScore;
    public float GetBrushLength() => lastBrushLength;

    /// <summary>
    /// Debug method to visualize evaluation
    /// </summary>
    [ContextMenu("Evaluate Drawing")]
    public void EvaluateDrawingDebug()
    {
        EvaluateNow();
    }
}