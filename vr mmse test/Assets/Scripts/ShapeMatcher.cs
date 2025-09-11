using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Shape matching utility using Hu-moments for main structure comparison
/// </summary>
public class ShapeMatcher : MonoBehaviour
{
    [System.Serializable]
    public struct Point2D
    {
        public float x, y;
        public Point2D(float x, float y) { this.x = x; this.y = y; }
        public Point2D(Vector2 v) { this.x = v.x; this.y = v.y; }
    }

    [System.Serializable]
    public struct HuMoments
    {
        public float h1, h2, h3, h4, h5, h6, h7;
        
        public HuMoments(float h1, float h2, float h3, float h4, float h5, float h6, float h7)
        {
            this.h1 = h1; this.h2 = h2; this.h3 = h3; this.h4 = h4;
            this.h5 = h5; this.h6 = h6; this.h7 = h7;
        }
    }

    /// <summary>
    /// Calculate Hu-moments for a set of contour points
    /// </summary>
    public static HuMoments CalculateHuMoments(List<Point2D> contour)
    {
        if (contour == null || contour.Count < 3)
            return new HuMoments(0, 0, 0, 0, 0, 0, 0);

        // Calculate geometric moments
        var moments = CalculateGeometricMoments(contour);
        
        // Calculate central moments
        var centralMoments = CalculateCentralMoments(contour, moments);
        
        // Calculate normalized central moments
        var normalizedMoments = CalculateNormalizedMoments(centralMoments);
        
        // Calculate Hu-moments
        return CalculateHuMomentsFromNormalized(normalizedMoments);
    }

    /// <summary>
    /// Compare two shapes using Hu-moments similarity
    /// Returns a similarity score between 0 (completely different) and 1 (identical)
    /// </summary>
    public static float CompareShapes(HuMoments template, HuMoments player)
    {
        // Calculate distance between Hu-moments using log-space comparison
        float[] templateMoments = { template.h1, template.h2, template.h3, template.h4, template.h5, template.h6, template.h7 };
        float[] playerMoments = { player.h1, player.h2, player.h3, player.h4, player.h5, player.h6, player.h7 };
        
        float totalDistance = 0f;
        int validMoments = 0;
        
        for (int i = 0; i < 7; i++)
        {
            if (Mathf.Abs(templateMoments[i]) > 1e-10f && Mathf.Abs(playerMoments[i]) > 1e-10f)
            {
                float logTemplate = Mathf.Log10(Mathf.Abs(templateMoments[i]));
                float logPlayer = Mathf.Log10(Mathf.Abs(playerMoments[i]));
                totalDistance += Mathf.Abs(logTemplate - logPlayer);
                validMoments++;
            }
        }
        
        if (validMoments == 0) return 0f;
        
        float averageDistance = totalDistance / validMoments;
        
        // Convert distance to similarity score (0-1 range)
        // Using exponential decay for similarity calculation
        float similarity = Mathf.Exp(-averageDistance * 0.5f);
        return Mathf.Clamp01(similarity);
    }

    private static float[,] CalculateGeometricMoments(List<Point2D> contour)
    {
        float[,] moments = new float[4, 4]; // up to m03, m30
        
        foreach (var point in contour)
        {
            for (int p = 0; p < 4; p++)
            {
                for (int q = 0; q < 4; q++)
                {
                    if (p + q <= 3)
                    {
                        moments[p, q] += Mathf.Pow(point.x, p) * Mathf.Pow(point.y, q);
                    }
                }
            }
        }
        
        return moments;
    }

    private static float[,] CalculateCentralMoments(List<Point2D> contour, float[,] moments)
    {
        // Calculate centroid
        float cx = moments[1, 0] / moments[0, 0];
        float cy = moments[0, 1] / moments[0, 0];
        
        float[,] centralMoments = new float[4, 4];
        
        foreach (var point in contour)
        {
            float dx = point.x - cx;
            float dy = point.y - cy;
            
            for (int p = 0; p < 4; p++)
            {
                for (int q = 0; q < 4; q++)
                {
                    if (p + q <= 3)
                    {
                        centralMoments[p, q] += Mathf.Pow(dx, p) * Mathf.Pow(dy, q);
                    }
                }
            }
        }
        
        return centralMoments;
    }

    private static float[,] CalculateNormalizedMoments(float[,] centralMoments)
    {
        float[,] normalizedMoments = new float[4, 4];
        float m00 = centralMoments[0, 0];
        
        if (m00 > 0)
        {
            for (int p = 0; p < 4; p++)
            {
                for (int q = 0; q < 4; q++)
                {
                    if (p + q >= 2 && p + q <= 3)
                    {
                        float gamma = ((p + q) / 2.0f) + 1;
                        normalizedMoments[p, q] = centralMoments[p, q] / Mathf.Pow(m00, gamma);
                    }
                }
            }
        }
        
        return normalizedMoments;
    }

    private static HuMoments CalculateHuMomentsFromNormalized(float[,] eta)
    {
        float h1 = eta[2, 0] + eta[0, 2];
        
        float h2 = Mathf.Pow(eta[2, 0] - eta[0, 2], 2) + 4 * Mathf.Pow(eta[1, 1], 2);
        
        float h3 = Mathf.Pow(eta[3, 0] - 3 * eta[1, 2], 2) + Mathf.Pow(3 * eta[2, 1] - eta[0, 3], 2);
        
        float h4 = Mathf.Pow(eta[3, 0] + eta[1, 2], 2) + Mathf.Pow(eta[2, 1] + eta[0, 3], 2);
        
        float h5 = (eta[3, 0] - 3 * eta[1, 2]) * (eta[3, 0] + eta[1, 2]) * 
                   (Mathf.Pow(eta[3, 0] + eta[1, 2], 2) - 3 * Mathf.Pow(eta[2, 1] + eta[0, 3], 2)) +
                   (3 * eta[2, 1] - eta[0, 3]) * (eta[2, 1] + eta[0, 3]) * 
                   (3 * Mathf.Pow(eta[3, 0] + eta[1, 2], 2) - Mathf.Pow(eta[2, 1] + eta[0, 3], 2));
        
        float h6 = (eta[2, 0] - eta[0, 2]) * 
                   (Mathf.Pow(eta[3, 0] + eta[1, 2], 2) - Mathf.Pow(eta[2, 1] + eta[0, 3], 2)) +
                   4 * eta[1, 1] * (eta[3, 0] + eta[1, 2]) * (eta[2, 1] + eta[0, 3]);
        
        float h7 = (3 * eta[2, 1] - eta[0, 3]) * (eta[3, 0] + eta[1, 2]) * 
                   (Mathf.Pow(eta[3, 0] + eta[1, 2], 2) - 3 * Mathf.Pow(eta[2, 1] + eta[0, 3], 2)) -
                   (eta[3, 0] - 3 * eta[1, 2]) * (eta[2, 1] + eta[0, 3]) * 
                   (3 * Mathf.Pow(eta[3, 0] + eta[1, 2], 2) - Mathf.Pow(eta[2, 1] + eta[0, 3], 2));
        
        return new HuMoments(h1, h2, h3, h4, h5, h6, h7);
    }

    /// <summary>
    /// Extract edge points from a 2D binary image/mask
    /// </summary>
    public static List<Point2D> ExtractEdgePoints(bool[,] binaryImage)
    {
        List<Point2D> edgePoints = new List<Point2D>();
        int height = binaryImage.GetLength(0);
        int width = binaryImage.GetLength(1);
        
        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                if (binaryImage[y, x])
                {
                    // Check if it's an edge point (has at least one non-filled neighbor)
                    bool isEdge = !binaryImage[y-1, x] || !binaryImage[y+1, x] || 
                                  !binaryImage[y, x-1] || !binaryImage[y, x+1] ||
                                  !binaryImage[y-1, x-1] || !binaryImage[y-1, x+1] ||
                                  !binaryImage[y+1, x-1] || !binaryImage[y+1, x+1];
                    
                    if (isEdge)
                    {
                        edgePoints.Add(new Point2D(x, y));
                    }
                }
            }
        }
        
        return edgePoints;
    }

    /// <summary>
    /// Calculate total brush length from a list of drawing points
    /// </summary>
    public static float CalculateBrushLength(List<Point2D> drawingPoints)
    {
        if (drawingPoints == null || drawingPoints.Count < 2)
            return 0f;
        
        float totalLength = 0f;
        for (int i = 1; i < drawingPoints.Count; i++)
        {
            float dx = drawingPoints[i].x - drawingPoints[i-1].x;
            float dy = drawingPoints[i].y - drawingPoints[i-1].y;
            totalLength += Mathf.Sqrt(dx * dx + dy * dy);
        }
        
        return totalLength;
    }
}