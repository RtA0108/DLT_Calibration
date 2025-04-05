using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum SaliencyMode { Entropy, Curvature }

public class ProjectionMappingCalibrator : MonoBehaviour
{
    public SaliencyMode saliencyMode = SaliencyMode.Entropy; // Default Entropy
    public Camera mainCamera;
    public MeshFilter meshFilter;
    public int recommendedVertexCount = 6;
    [Range(0f, 1f)] public float topSaliencyPercentage = 0.5f;

    private Dictionary<int, Vector3> vertexPositions;
    private float l;
    private LayerMask visibilityLayerMask;

    void Start()
    {
        Prepare();
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
        l = Vector3.Distance(meshFilter.mesh.bounds.min, meshFilter.mesh.bounds.max);
    }

    void Run()
    {
        var visible = SaliencyUtils.GetVisibleVertices(mainCamera, meshFilter, vertexPositions, visibilityLayerMask);

        var sigmaMin = 0.05f * l;
        var filtered = SaliencyUtils.FilterSilhouetteVertices(mainCamera, meshFilter, visible, sigmaMin, 0.8f, 0.05f); // ★ 수치 복원 (기존 0.5f, 0.08f)
        //var filtered = SaliencyUtils.FilterSilhouetteVertices(
//    mainCamera, meshFilter, visible, sigmaMin,
//    0.5f, 0.08f,          // 기본 실루엣 조건
//    0.8f, 1.0f, 0.01f      // 중심 제한, 측면/턱 제거
//);
        foreach (var vertex in filtered)
        {
            SaliencyUtils.HighlightVertex(vertex, Color.blue, 6f, false); // or 다른 색으로 구분
        }
        Dictionary<Vector3, float> saliencyMap = saliencyMode switch
        {
            SaliencyMode.Entropy => EntropySaliencyComputer.Compute(meshFilter, filtered, l),
            SaliencyMode.Curvature => MeshSaliencyComputer.Compute(meshFilter, filtered, l),
            _ => throw new System.Exception("Unknown mode")
        };

        var threshold = saliencyMap.Values.OrderByDescending(v => v).ElementAt((int)(saliencyMap.Count * topSaliencyPercentage));
        var topCandidates = saliencyMap.Where(kv => kv.Value >= threshold).Select(kv => kv.Key).ToList();

        var final = SaliencyUtils.SelectHybridDistributedVertices(topCandidates, saliencyMap, l, recommendedVertexCount, alpha: 0.6f); // ★ alpha 복원

        for (int i = 0; i < final.Count; i++)
            SaliencyUtils.HighlightVertex(final[i], Color.red, 7f, true, i); // ★ Highlight 크기 복원

        Debug.Log("Calibration Complete");
    }
}