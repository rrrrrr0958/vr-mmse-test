using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Example usage and integration guide for the new VR drawing evaluation system
/// </summary>
public class VRDrawingEvaluationExample : MonoBehaviour
{
    [Header("Example Template")]
    [SerializeField] private Texture2D circleTemplate; // Example: a circle template
    
    [Header("Example Player Drawing")]
    [SerializeField] private List<Vector2> exampleDrawing = new List<Vector2>();
    
    [Header("Evaluation Component")]
    [SerializeField] private WhiteboardChamferJudge evaluator;

    private void Start()
    {
        // Initialize evaluator if not assigned
        if (evaluator == null)
        {
            evaluator = gameObject.AddComponent<WhiteboardChamferJudge>();
        }
        
        // Create an example circular drawing for testing
        CreateExampleCircularDrawing();
    }

    /// <summary>
    /// Create an example circular drawing for testing purposes
    /// </summary>
    private void CreateExampleCircularDrawing()
    {
        exampleDrawing.Clear();
        
        Vector2 center = new Vector2(50, 50);
        float radius = 30f;
        int points = 64;
        
        for (int i = 0; i <= points; i++)
        {
            float angle = (float)i / points * 2 * Mathf.PI;
            Vector2 point = center + new Vector2(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius
            );
            exampleDrawing.Add(point);
        }
        
        Debug.Log($"Created example circular drawing with {exampleDrawing.Count} points");
    }

    /// <summary>
    /// Test the evaluation system with example data
    /// </summary>
    [ContextMenu("Test Evaluation")]
    public void TestEvaluation()
    {
        if (evaluator == null)
        {
            Debug.LogError("Evaluator not found!");
            return;
        }
        
        if (exampleDrawing.Count == 0)
        {
            CreateExampleCircularDrawing();
        }
        
        // Set the player drawing
        evaluator.SetPlayerDrawing(exampleDrawing);
        
        // If we have a template, set it
        if (circleTemplate != null)
        {
            evaluator.SetTemplateImage(circleTemplate);
        }
        
        // Evaluate the drawing
        float score = evaluator.EvaluateNow();
        
        Debug.Log($"Evaluation complete! Score: {score:F3}");
        Debug.Log($"Structure Score: {evaluator.GetStructureScore():F3}");
        Debug.Log($"Coverage Score: {evaluator.GetCoverageScore():F3}");
        Debug.Log($"Chamfer Score: {evaluator.GetChamferScore():F3}");
        Debug.Log($"Brush Length: {evaluator.GetBrushLength():F1}");
    }

    /// <summary>
    /// Create an example template image programmatically for testing
    /// </summary>
    [ContextMenu("Create Test Template")]
    public void CreateTestTemplate()
    {
        int size = 100;
        Texture2D template = new Texture2D(size, size);
        
        // Fill with white background
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                template.SetPixel(x, y, Color.white);
            }
        }
        
        // Draw a black circle
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size * 0.3f;
        
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                if (Mathf.Abs(distance - radius) < 1.5f) // Circle edge
                {
                    template.SetPixel(x, y, Color.black);
                }
            }
        }
        
        template.Apply();
        circleTemplate = template;
        
        Debug.Log("Created test circle template");
    }

    /// <summary>
    /// Integration guide for VR drawing systems
    /// </summary>
    public void IntegrationGuide()
    {
        Debug.Log(@"
=== VR Drawing Evaluation Integration Guide ===

1. Setup:
   - Add WhiteboardChamferJudge component to your evaluation object
   - Assign a template image (black drawing on white background)
   - Configure minimum brush length and weights as needed

2. During drawing:
   - Collect drawing points as List<Vector2> from VR input
   - Call SetPlayerDrawing(points) to update the drawing data

3. Evaluation:
   - Call EvaluateNow() to get the final score (0-1 range)
   - Use individual score components for detailed feedback:
     * GetStructureScore() - Hu-moments similarity (main structure)
     * GetCoverageScore() - Coverage with improved denominator
     * GetChamferScore() - Strict Chamfer distance
     * GetBrushLength() - Total drawing length

4. Score Interpretation:
   - Total Score: Weighted combination (80% structure, 20% details)
   - Structure Score: Shape similarity using Hu-moments
   - Coverage Score: How well drawing covers template (with dilation)
   - Chamfer Score: Distance accuracy between drawings
   - Minimum brush length filter prevents trivial high scores

5. Debug UI:
   - Assign UI Text components to see real-time scores
   - Use context menu 'Evaluate Drawing' for testing
        ");
    }
}