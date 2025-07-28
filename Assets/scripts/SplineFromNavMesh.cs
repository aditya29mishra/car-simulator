#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using EdyCommonTools;
using UnityEngine.AI;
using System.Collections.Generic;

public class SplineFromNavMesh : MonoBehaviour
{
    public Transform roadStart;
    public Transform roadEnd;
    public Spline spline;

    [MenuItem("Tools/Extract Spline From NavMesh Path")]
    static void DoExtract()
    {
        var gen = Selection.activeGameObject?.GetComponent<SplineFromNavMesh>();
        if (!gen) { Debug.LogWarning("Attach SplineFromNavMesh to your object and assign start, end, and Spline."); return; }
        gen.GeneratePath();
    }

    public void GeneratePath()
    {
        if(!roadStart || !roadEnd || !spline)
        {
            Debug.LogError("Assign roadStart, roadEnd, and spline!");
            return;
        }
        NavMeshPath navPath = new NavMeshPath();
        if (NavMesh.CalculatePath(roadStart.position, roadEnd.position, NavMesh.AllAreas, navPath))
        {
            if (navPath.status != NavMeshPathStatus.PathComplete)
            {
                Debug.LogError("NavMesh path not complete.");
                return;
            }
            var corners = navPath.corners;
            spline.points = new Spline.Point[corners.Length];
            for (int i = 0; i < corners.Length; ++i)
            {
                var pt = new Spline.Point();
                pt.position = corners[i];
                spline.points[i] = pt;
            }
            spline.Refresh();
            Debug.Log($"Spline generated from NavMesh path with {corners.Length} points.");
        }
        else
        {
            Debug.LogError("Failed to find path on NavMesh.");
        }
    }
}
#endif