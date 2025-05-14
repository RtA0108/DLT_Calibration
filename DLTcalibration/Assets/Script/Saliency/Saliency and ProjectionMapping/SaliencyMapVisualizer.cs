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
    Material[] originalMaterials;

    private void Start()
    {
        // 자동으로 하위에서 MeshFilter를 가져오도록 보정
        if (meshFilter == null)
        {
            meshFilter = GetComponentInChildren<MeshFilter>();
            if (meshFilter == null)
            {
                Debug.LogError("[SaliencyMapVisualizer] 하위에서 MeshFilter를 찾을 수 없습니다.");
                return;
            }
        }

        if (runOnStart)
        {
            StoreOriginalMaterials();
            ApplyVertexColorMaterial();
            StartCoroutine(VisualizeOnce());
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
    void OnDisable()
    {
        RestoreOriginalMaterials();
    }
    void StoreOriginalMaterials()
    {
        var renderer = meshFilter.GetComponent<Renderer>() ?? meshFilter.GetComponentInChildren<Renderer>();
        if (renderer != null)
            originalMaterials = renderer.sharedMaterials;
    }

    void RestoreOriginalMaterials()
    {
        var renderer = meshFilter.GetComponent<Renderer>() ?? meshFilter.GetComponentInChildren<Renderer>();
        if (renderer != null && originalMaterials != null)
        {
            renderer.materials = originalMaterials;
            Debug.Log("[SaliencyMapVisualizer] 원래 머티리얼로 복원됨");
        }
    }

    void ApplyVertexColorMaterial()
    {
        if (!Application.isPlaying) return;

        var renderer = meshFilter.GetComponent<Renderer>() ?? meshFilter.GetComponentInChildren<Renderer>();
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

        int slotCount = renderer.sharedMaterials.Length;
        Material[] materials = Enumerable.Repeat(material, slotCount).ToArray();
        renderer.materials = materials;

        Debug.Log("[SaliencyMapVisualizer] 런타임에 VertexColorLitS 셰이더 적용 완료");

        // 디버깅: vertex color 존재 여부 확인
        var mesh = meshFilter.sharedMesh;
        int colorCount = mesh.colors?.Length ?? 0;
        Debug.Log($"[VC Check] VertexCount: {mesh.vertexCount}, ColorCount: {colorCount}, VC 존재 여부: {(colorCount > 0 ? "있음" : "없음")}");

        // vertex color가 없으면 임의 색상이라도 채워야 셰이더가 동작함
        if (colorCount == 0)
        {
            Color[] colors = new Color[mesh.vertexCount];
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = Color.gray; // 또는 테스트용 단색
            }
            mesh.colors = colors;
            Debug.Log("[SaliencyMapVisualizer] vertex color가 비어있어 기본 회색으로 채움");
        }
        else if (colorCount > 0)
        {
            var distinctColors = mesh.colors.Distinct().ToList();
            Debug.Log($"[VC Check] Color Count: {colorCount}, Distinct Colors: {distinctColors.Count}");
            foreach (var c in distinctColors)
                Debug.Log($"[VC Sample] {c}");
        }
    }

    //void ApplyVertexColorMaterial()
    //{
    //    var renderer = meshFilter.GetComponent<Renderer>();
    //    if (renderer == null)
    //    {
    //        Debug.LogError("[SaliencyMapVisualizer] MeshRenderer를 찾을 수 없습니다.");
    //        return;
    //    }

    //    Shader shader = Shader.Find("Universal Render Pipeline/VertexColorLitS");
    //    if (shader == null)
    //    {
    //        Debug.LogError("[SaliencyMapVisualizer] 셰이더를 찾을 수 없습니다. 이름을 확인하세요.");
    //        return;
    //    }

    //    Material material = new Material(shader);
    //    material.enableInstancing = false;

    //    int slotCount = renderer.materials.Length;
    //    Material[] materials = Enumerable.Repeat(material, slotCount).ToArray();
    //    renderer.materials = materials;

    //    Debug.Log("[SaliencyMapVisualizer] 런타임에 VertexColorLitS 머티리얼 적용 완료");
    //}
    IEnumerator VisualizeOnce()
    {
        if (meshFilter == null || mainCamera == null)
        {
            Debug.LogError("[SaliencyMapVisualizer] MeshFilter 또는 Camera가 설정되지 않았습니다.");
            yield break;
        }

        Vector3[] allVertices = SaliencyUtils.GetUniqueWorldVertices(meshFilter);
        List<Vector3> allVertexList = new List<Vector3>(allVertices);

        Bounds bounds = meshFilter.mesh.bounds;
        Vector3 scaled = Vector3.Scale(bounds.size, meshFilter.transform.lossyScale);
        float l = scaled.magnitude;

        Debug.Log($"[SaliencyMapVisualizer] σ = multi-scale 기반 saliency 계산 시작 (l = {l:F4})");

        Dictionary<Vector3, float> saliencyMap = EntropySaliencyComputer.Compute(meshFilter, allVertexList, l);

        LogSaliencyStatistics(saliencyMap);
        ApplyVertexColorsWithStretch(meshFilter.mesh, saliencyMap);

        Debug.Log("[SaliencyMapVisualizer] 시각화 완료.");
        yield return null;
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
        float min = values[values.Count / 10];             // 하위 10% 컷
        float max = values[values.Count * 18 / 20];         // 상위 90% 컷

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 worldPos = meshFilter.transform.TransformPoint(vertices[i]);
            Vector3 nearest = saliencyMap.Keys.OrderBy(v => Vector3.Distance(v, worldPos)).FirstOrDefault();

            if (saliencyMap.TryGetValue(nearest, out float saliency))
            {
                float t = Mathf.Clamp01((saliency - min) / (max - min));
                t = Mathf.Sqrt(t); // 밝은 영역 덜 침투하게
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