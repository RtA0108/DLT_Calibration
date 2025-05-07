using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SaliencyMapVisualizer : MonoBehaviour
{
    public MeshFilter meshFilter;
    public Camera mainCamera;
    public LayerMask visibilityLayerMask;
    public bool runOnStart = true;

    private void Start()
    {
        if (runOnStart)
        {
            ApplyVertexColorMaterial();
            StartCoroutine(VisualizePerSigmaCoroutine());
        }
    }
    private void LogSaliencyStatistics(Dictionary<Vector3, float> saliencyMap)
    {
        float[] values = saliencyMap.Values.ToArray();
        float min = values.Min();
        float max = values.Max();
        float avg = values.Average();
        float median = values.OrderBy(v => v).ElementAt(values.Length / 2);
        float stdDev = Mathf.Sqrt(values.Select(v => Mathf.Pow(v - avg, 2)).Average());

        Debug.Log($"[Saliency Stats] Count: {values.Length}, Min: {min:F4}, Max: {max:F4}, Avg: {avg:F4}, Median: {median:F4}, StdDev: {stdDev:F4}");
    }
    void ApplyVertexColorMaterial()
    {
        var renderer = meshFilter.GetComponent<Renderer>();
        if (renderer == null)
        {
            Debug.LogError("[SaliencyMapVisualizer] MeshRenderer를 찾을 수 없습니다.");
            return;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/VertexColorLitS");
        if (shader == null)
        {
            Debug.LogError("[SaliencyMapVisualizer] 셰이더를 찾을 수 없습니다. 이름을 확인하세요.");
            return;
        }

        Material material = new Material(shader);
        material.enableInstancing = false;

        int slotCount = renderer.materials.Length;
        Material[] materials = Enumerable.Repeat(material, slotCount).ToArray();
        renderer.materials = materials;

        Debug.Log("[SaliencyMapVisualizer] 런타임에 VertexColorLitS 머티리얼 적용 완료");
    }
    IEnumerator VisualizePerSigmaCoroutine()
    {
        if (meshFilter == null || mainCamera == null)
        {
            Debug.LogError("[SaliencyMapVisualizer] MeshFilter 또는 Camera가 설정되지 않았습니다.");
            yield break;
        }

        // 1. 전체 정점 가져오기
        Vector3[] allVertices = SaliencyUtils.GetUniqueWorldVertices(meshFilter);
        List<Vector3> allVertexList = new List<Vector3>(allVertices);

        // 2. 특징 길이 l 계산
        Bounds bounds = meshFilter.mesh.bounds;
        Vector3 scaled = Vector3.Scale(bounds.size, meshFilter.transform.lossyScale);
        float l = scaled.magnitude;

        // 3. 여러 sigma 설정
        float[] sigmas = new float[] { 0.07f * l, 0.075f * l, 0.08f * l };

        for (int i = 0; i < sigmas.Length; i++)
        {
            float sigma = sigmas[i];
            Debug.Log($"[SaliencyMapVisualizer] Sigma {i} (σ = {sigma:F4}) 에 대해 saliency 계산 중...");

            // 4. sigma별 entropy 계산
            Dictionary<Vector3, float> saliencyMap = EntropySaliencyComputer.ComputeAtSigma(meshFilter, allVertexList, sigma);

            LogSaliencyStatistics(saliencyMap);
            Debug.Log($"[SaliencyMapVisualizer] saliencyMap.Count = {saliencyMap.Count}");
            // 5. vertex color로 시각화
            //ApplyVertexColors(meshFilter.mesh, saliencyMap);
            ApplyVertexColorsWithStretch(meshFilter.mesh, saliencyMap);
            Debug.Log($"[SaliencyMapVisualizer] Sigma {i} 시각화 완료. 2초간 대기 중...");
            yield return new WaitForSeconds(2.0f); // 다음 sigma로 넘어가기 전 대기
        }

        Debug.Log("[SaliencyMapVisualizer] 모든 sigma에 대한 시각화 완료.");
    }

    private void ApplyVertexColors(Mesh mesh, Dictionary<Vector3, float> saliencyMap)
    {
        Vector3[] vertices = mesh.vertices;
        Color[] colors = new Color[vertices.Length];

        float min = saliencyMap.Values.Min();
        float max = saliencyMap.Values.Max();

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 worldPos = meshFilter.transform.TransformPoint(vertices[i]);

            Vector3 nearest = saliencyMap.Keys
                .OrderBy(v => Vector3.Distance(v, worldPos))
                .FirstOrDefault();

            if (saliencyMap.TryGetValue(nearest, out float saliency))
            {
                float t = Mathf.Clamp01((saliency - min) / (max - min));
                colors[i] = Color.Lerp(Color.blue, Color.red, t);
            }
            else
            {
                colors[i] = Color.black;
            }
        }

        mesh.colors = colors;
    }
    private void ApplyVertexColorsWithStretch(Mesh mesh, Dictionary<Vector3, float> saliencyMap)
    {
        Vector3[] vertices = mesh.vertices;
        Color[] colors = new Color[vertices.Length];

        var values = saliencyMap.Values.OrderBy(v => v).ToList();
        float min = values[values.Count / 20];             // 하위 5% 컷
        float max = values[values.Count * 19 / 20];         // 상위 95% 컷

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 worldPos = meshFilter.transform.TransformPoint(vertices[i]);
            Vector3 nearest = saliencyMap.Keys.OrderBy(v => Vector3.Distance(v, worldPos)).FirstOrDefault();

            if (saliencyMap.TryGetValue(nearest, out float saliency))
            {
                float t = Mathf.Clamp01((saliency - min) / (max - min));
                colors[i] = Color.Lerp(Color.blue, Color.red, t);
            }
            else
            {
                colors[i] = Color.black;
            }
        }

        mesh.colors = colors;
    }
}

//using System.Collections.Generic;
//using System.Linq;
//using UnityEngine;

//public enum SaliencyModeChanger
//{
//    Entropy,
//    // 추후: Curvature 등 추가 가능
//}

//public class SaliencyMapVisualizer : MonoBehaviour
//{
//    public SaliencyModeChanger saliencyMode = SaliencyModeChanger.Entropy;
//    public MeshFilter meshFilter;
//    public Camera mainCamera;
//    public LayerMask visibilityLayerMask;

//    public bool visualizeOnStart = true;

//    void Start()
//    {
//        if (visualizeOnStart)
//            GenerateAndVisualizeSaliency();
//    }

//    public void GenerateAndVisualizeSaliency()
//    {
//        if (meshFilter == null || mainCamera == null)
//        {
//            Debug.LogError("[SaliencyMapVisualizer] MeshFilter 또는 Camera가 설정되지 않았습니다.");
//            return;
//        }

//        Mesh mesh = meshFilter.mesh;

//        // [1] Get unique visible vertices
//        Vector3[] allVertices = SaliencyUtils.GetUniqueWorldVertices(meshFilter);
//        List<Vector3> allVertexList = new List<Vector3>(allVertices);

//        if (allVertexList.Count == 0)
//        {
//            Debug.LogWarning("[SaliencyMapVisualizer] 카메라에 보이는 정점이 없습니다.");
//            return;
//        }

//        // [2] 자동으로 characteristic length 계산
//        float l = ComputeCharacteristicLength();

//        // [3] Saliency 계산
//        Dictionary<Vector3, float> saliencyMap = ComputeSaliency(meshFilter, allVertexList, l);

//        // [4] 시각화 (vertex color)
//        ApplyVertexColors(meshFilter.mesh, saliencyMap);
//    }

//    private float ComputeCharacteristicLength()
//    {
//        Bounds bounds = meshFilter.mesh.bounds;
//        return bounds.size.magnitude; // 또는 평균/최댓값으로 교체 가능
//    }

//    private Dictionary<Vector3, float> ComputeSaliency(MeshFilter meshFilter, List<Vector3> visibleVertices, float l)
//    {
//        switch (saliencyMode)
//        {
//            case SaliencyModeChanger.Entropy:
//                return EntropySaliencyComputer.Compute(meshFilter, visibleVertices, l);
//            default:
//                Debug.LogError("Saliency mode not implemented.");
//                return new Dictionary<Vector3, float>();
//        }
//    }

//    private void ApplyVertexColors(Mesh mesh, Dictionary<Vector3, float> saliencyMap)
//    {
//        Vector3[] vertices = mesh.vertices;
//        Color[] colors = new Color[vertices.Length];

//        float min = saliencyMap.Values.Min();
//        float max = saliencyMap.Values.Max();

//        for (int i = 0; i < vertices.Length; i++)
//        {
//            Vector3 worldPos = meshFilter.transform.TransformPoint(vertices[i]);

//            // 가장 가까운 키 매칭 (distance 기준)
//            Vector3 matched = saliencyMap.Keys
//                .OrderBy(v => Vector3.Distance(v, worldPos))
//                .FirstOrDefault();

//            if (saliencyMap.TryGetValue(matched, out float saliency))
//            {
//                float t = Mathf.Clamp01((saliency - min) / (max - min));
//                colors[i] = Color.Lerp(Color.blue, Color.red, t);
//            }
//            else
//            {
//                colors[i] = Color.black;
//            }
//        }

//        mesh.colors = colors;
//    }
//}