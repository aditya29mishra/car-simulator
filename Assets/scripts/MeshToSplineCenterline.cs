#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using EdyCommonTools;
using System.Collections.Generic;
using System.Linq;

public class MeshToSplineCenterline : MonoBehaviour
{
    public Transform roadTransform;
    public Spline spline;
    public int sampleCount = 50;
    public float sectionWidth = 1.5f; // Cross-section thickness

    [MenuItem("Tools/Auto-Extract Spline Centerline From Mesh", false, 12)]
    static void GenerateSplineMenu()
    {
        var gen = Selection.activeGameObject?.GetComponent<MeshToSplineCenterline>();
        if (!gen) { Debug.LogWarning("Select an object with MeshToSplineCenterline attached."); return; }
        gen.GenerateCenterline();
    }

    public void GenerateCenterline()
    {
        if (!roadTransform) { Debug.LogError("No roadTransform assigned."); return; }
        if (!spline) spline = GetComponent<Spline>();
        if (!spline) { Debug.LogError("No Spline component found."); return; }

        var mf = roadTransform.GetComponentInChildren<MeshFilter>();
        if (!mf || !mf.sharedMesh) { Debug.LogError("No MeshFilter/mesh found."); return; }
        Mesh mesh = mf.sharedMesh;

        // Transform all mesh vertices to world space
        var vertsWorld = mesh.vertices.Select(v =>
            mf.transform.TransformPoint(v)).ToArray();

        // Sample along main axis (local Z: min.z to max.z)
        var bounds = mesh.bounds;
        List<Vector3> pathPoints = new List<Vector3>();
        for (int i = 0; i < sampleCount; ++i)
        {
            float t = i / (float)(sampleCount - 1);
            float localZ = Mathf.Lerp(bounds.min.z, bounds.max.z, t);

            // Get vertices within the cross-section slab at this Z (in mesh local space)
            var pointsInSection = new List<Vector3>();
            for (int k = 0; k < mesh.vertexCount; ++k)
            {
                var local = mesh.vertices[k];
                if (Mathf.Abs(local.z - localZ) <= sectionWidth)
                    pointsInSection.Add(vertsWorld[k]);
            }

            if (pointsInSection.Count == 0) continue;

            // Use the average as the centerline point
            Vector3 mid = Vector3.zero;
            foreach (var p in pointsInSection) mid += p;
            mid /= pointsInSection.Count;
            pathPoints.Add(mid);
        }

        if (pathPoints.Count < 2)
        {
            Debug.LogError("Not enough path points found from mesh! Try increasing sectionWidth or check mesh orientation.");
            return;
        }

        // Assign to spline
        spline.points = new Spline.Point[pathPoints.Count];
        for (int i = 0; i < pathPoints.Count; i++)
        {
            Spline.Point p = new Spline.Point();
            p.position = pathPoints[i];
            spline.points[i] = p;
        }
        spline.Refresh();
        Debug.Log($"Generated {pathPoints.Count} centerline points from mesh.");
    }
}
#endif