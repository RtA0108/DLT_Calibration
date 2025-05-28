using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public enum SaliencyMode { Entropy, Curvature }

public class ProjectionMappingCalibrator : MonoBehaviour
{
    public SaliencyMode saliencyMode = SaliencyMode.Entropy; // Default Entropy
    public Camera mainCamera;
    public MeshFilter meshFilter;
    public int recommendedVertexCount = 6;
    public bool visualized = true;
    public SilhouetteEdgeMaskRenderer silhouetteRenderer;
    public DebugEdgeMaskRenderer debugRenderer;
    [Range(0f, 1f)] public float topSaliencyPercentage = 0.5f;

    private Dictionary<int, Vector3> vertexPositions;
    private float l;
    private LayerMask visibilityLayerMask;

    void Start()
    {
        Prepare();
        silhouetteRenderer.Render();
        Run();
    }

    void Prepare()
    {
        // Layer 설정 복원
        int vertexLayer = LayerMask.NameToLayer("Vertex In 3D");
        if (vertexLayer != -1)
            visibilityLayerMask = ~(1 << vertexLayer);
        else
            visibilityLayerMask = Physics.DefaultRaycastLayers;

        vertexPositions = new Dictionary<int, Vector3>();
        Vector3[] positions = meshFilter.mesh.vertices;
        for (int i = 0; i < positions.Length; i++) vertexPositions[i] = positions[i];

        Bounds bounds = meshFilter.mesh.bounds;
        Vector3 scale = meshFilter.transform.lossyScale;
        Vector3 scaledMin = Vector3.Scale(bounds.min, scale);
        Vector3 scaledMax = Vector3.Scale(bounds.max, scale);
        l = Vector3.Distance(scaledMin, scaledMax);

        Debug.Log($"[Prepare] Computed l (world scale corrected): {l:F3}");
    }
    void Run()
    {
        var edgeMask = silhouetteRenderer.GetEdgeMask();
        silhouetteRenderer.SaveSilhouetteMaskToPNG();
        silhouetteRenderer.SaveEdgeMaskToPNG("SavedSilhouette.png");
        debugRenderer.edgeMask = edgeMask; // 디버그용 연결

        var visible = SaliencyUtils.GetVisibleVertices(mainCamera, meshFilter, vertexPositions, visibilityLayerMask);
        if (visualized)
        {
            foreach (var v in visible)
                SaliencyUtils.HighlightVertex(v, Color.blue, 5.5f, false);
        }

        var filtered = SaliencyUtils.FilterVerticesByEdge(edgeMask, visible, mainCamera, distanceThresholdPixels: 1.5f);


        //var filtered = SaliencyUtils.FilterVerticesForCalibration(mainCamera, meshFilter, visible);
        if (visualized) { 
            foreach (var vertex in filtered)
                SaliencyUtils.HighlightVertex(vertex, Color.green, 6f, false);
        }
        // Saliency 계산
        Dictionary<Vector3, float> saliencyMap = saliencyMode switch
        {
            SaliencyMode.Entropy => EntropySaliencyComputer.Compute(meshFilter, filtered, l),
            SaliencyMode.Curvature => MeshSaliencyComputer.Compute(meshFilter, filtered, l),
            _ => throw new System.Exception("Unknown mode")
        };

        // 여기 변경: TopCandidates = Filtered 전체로 사용
        var topCandidates = filtered;

        // (Optional) entropyMap과 filtered 매칭 체크 (디버깅용)
        EntropySaliencyComputer.CheckFilteredVerticesMatch(saliencyMap, filtered);

        var final = SaliencyUtils.SelectHybridDistributedVertices(topCandidates, saliencyMap, l, recommendedVertexCount, topSaliencyPercentage);

        Debug.Log($"[Final Recommended] {final.Count} vertices selected.");

        for (int i = 0; i < final.Count; i++)
            SaliencyUtils.HighlightVertex(final[i], Color.red, 7f, true, i);

        Debug.Log("Calibration Complete");
    }
}
