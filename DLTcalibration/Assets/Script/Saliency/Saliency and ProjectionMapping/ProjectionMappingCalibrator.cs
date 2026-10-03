using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum SaliencyMode { Entropy, Curvature, Spectral, CFSCNN, TexMesh }

public class ProjectionMappingCalibrator : MonoBehaviour
{
    [Header("Settings")]
    public SaliencyMode saliencyMode = SaliencyMode.Entropy;

    [SerializeField] private bool _visualized = false;

    public bool visualized
    {
        get { return _visualized; }
        set
        {
            if (_visualized != value)
            {
                _visualized = value;
                if (this.enabled) { if (_visualized) Run(); else ClearMarkers(); }
            }
        }
    }

    public int recommendedVertexCount = 6;
    [Range(0.1f, 5.0f)] public float saliencyWeightAlpha = 1.0f;

    [Header("Visualization")]
    public float markerScale = 1.75f; // 화면에 보이는 실제 크기 (조절 가능)
    public GameObject markerPrefab;

    [Header("References")]
    public Camera targetCamera;
    public MeshFilter meshFilter;

    [HideInInspector]
    public SilhouetteEdgeMaskRenderer silhouetteRenderer;

    [Header("Paths")]
    public string objPath;
    public string cfsSaliencyPath;
    public string texSaliencyPath;

    public static List<Vector3> publicFilteredVertices;

    private Dictionary<int, Vector3> vertexPositions;
    private LayerMask visibilityLayerMask;
    private GameObject markerRoot; // GameObject 타입임
    private List<GameObject> activeMarkers = new List<GameObject>();
    private bool isInitialized = false;

    private Vector3 _lastLossyScale;

    private void OnValidate() { if (Application.isPlaying && isInitialized && this.enabled) { if (_visualized) Run(); else ClearMarkers(); } }
    private void OnEnable() { if (isInitialized && _visualized) Run(); }
    private void OnDisable() { ClearMarkers(); }

    public void SetPaths(string obj, string cfs, string tex) { this.objPath = obj; this.cfsSaliencyPath = cfs; this.texSaliencyPath = tex; }

    // ★ [수정됨] .transform을 통해 lossyScale 접근
    private void Update()
    {
        if (!visualized || activeMarkers.Count == 0 || markerRoot == null) return;

        // GameObject에는 lossyScale이 없으므로 transform.lossyScale 사용
        Vector3 currentScale = markerRoot.transform.lossyScale;

        if (Vector3.Distance(currentScale, _lastLossyScale) > 0.0001f)
        {
            UpdateMarkerScales();
            _lastLossyScale = currentScale;
        }
    }

    private void UpdateMarkerScales()
    {
        Vector3 newScale = CalculateInverseScale();
        foreach (var marker in activeMarkers)
        {
            if (marker != null) marker.transform.localScale = newScale;
        }
    }

    // ★ [수정됨] .transform을 통해 lossyScale 접근
    private Vector3 CalculateInverseScale()
    {
        if (markerRoot == null) return Vector3.one * markerScale;

        Vector3 parentScale = markerRoot.transform.lossyScale;

        float sx = Mathf.Abs(parentScale.x) < 0.0001f ? 1f : parentScale.x;
        float sy = Mathf.Abs(parentScale.y) < 0.0001f ? 1f : parentScale.y;
        float sz = Mathf.Abs(parentScale.z) < 0.0001f ? 1f : parentScale.z;

        return new Vector3(
            markerScale / sx,
            markerScale / sy,
            markerScale / sz
        );
    }

    public void Init()
    {
        if (meshFilter == null) meshFilter = GetComponentInChildren<MeshFilter>();

        if (targetCamera == null)
        {
            GameObject camObj = GameObject.FindWithTag("Project Camera");
            if (camObj != null) targetCamera = camObj.GetComponent<Camera>();
            else
            {
                if (targetCamera == null) targetCamera = Camera.main;
            }
        }

        if (silhouetteRenderer == null)
        {
            if (targetCamera != null) silhouetteRenderer = targetCamera.GetComponent<SilhouetteEdgeMaskRenderer>();
            if (silhouetteRenderer == null) silhouetteRenderer = FindObjectOfType<SilhouetteEdgeMaskRenderer>();
            if (silhouetteRenderer == null)
            {
                silhouetteRenderer = gameObject.AddComponent<SilhouetteEdgeMaskRenderer>();
                silhouetteRenderer.hideFlags = HideFlags.HideInInspector;
            }
        }

        if (meshFilter == null) return;
        if (visibilityLayerMask.value == 0) { int layer = LayerMask.NameToLayer("Meshes"); visibilityLayerMask = (layer != -1) ? (1 << layer) : -1; }

        PrepareData();
        isInitialized = true;

        if (this.enabled && _visualized) Run();
    }

    public void Run()
    {
        ClearMarkers();
        if (!this.enabled || !_visualized || meshFilter == null || targetCamera == null) return;

        // (A) 실루엣 렌더링
        RenderTexture rtMask = null;
        if (silhouetteRenderer != null)
        {
            silhouetteRenderer.Render(targetCamera, meshFilter);
            rtMask = silhouetteRenderer.GetEdgeMask();
        }

        // (B) 가시성 체크
        var visible = SaliencyUtils.GetVisibleVertices(targetCamera, meshFilter, vertexPositions, visibilityLayerMask);
        if (visible.Count == 0) return;

        List<Vector3> filtered = new List<Vector3>(visible);

        if (rtMask != null) filtered = SaliencyUtils.FilterVerticesByEdge(rtMask, filtered, targetCamera, 1.5f);
        filtered = SaliencyUtils.FilterVerticesByTriangleNormals(filtered, meshFilter, targetCamera, 0.3f);
        publicFilteredVertices = new List<Vector3>(filtered);

        if (filtered.Count == 0) return;

        // (C) Saliency 계산
        Dictionary<Vector3, float> localMap = new Dictionary<Vector3, float>();

        Vector3 originalPos = transform.localPosition;
        Quaternion originalRot = transform.localRotation;
        Vector3 originalScale = transform.localScale;

        List<Vector3> localFiltered = new List<Vector3>();
        foreach (var v in filtered) localFiltered.Add(transform.InverseTransformPoint(v));

        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        transform.localPosition = Vector3.zero;

        List<Vector3> proxyWorldVertices = new List<Vector3>();
        foreach (var lv in localFiltered) proxyWorldVertices.Add(transform.TransformPoint(lv));

        float normalizedL = Vector3.Distance(meshFilter.sharedMesh.bounds.min, meshFilter.sharedMesh.bounds.max);

        try
        {
            switch (saliencyMode)
            {
                case SaliencyMode.Entropy:
                    var entropyMap = EntropySaliencyComputer.Compute(meshFilter, proxyWorldVertices, normalizedL);
                    foreach (var kvp in entropyMap)
                    {
                        Vector3 localPos = transform.InverseTransformPoint(kvp.Key);
                        localMap[localPos] = kvp.Value;
                    }
                    break;
                case SaliencyMode.Curvature:
                    List<Vector3> allLocal = new List<Vector3>(meshFilter.sharedMesh.vertices);
                    localMap = MeshSaliencyComputer.Compute(meshFilter, allLocal, normalizedL);
                    break;
                case SaliencyMode.CFSCNN:
                case SaliencyMode.TexMesh:
                    List<Vector3> allLocal2 = new List<Vector3>(meshFilter.sharedMesh.vertices);
                    if (saliencyMode == SaliencyMode.CFSCNN)
                        localMap = SaliencyDataLoader.GetSaliencyMap(meshFilter, allLocal2, objPath, cfsSaliencyPath);
                    else
                        localMap = SaliencyDataLoader.GetSaliencyMap(meshFilter, allLocal2, objPath, texSaliencyPath);
                    break;
            }
        }
        catch { }
        finally
        {
            transform.localPosition = originalPos;
            transform.localRotation = originalRot;
            transform.localScale = originalScale;
        }

        if (localMap == null || localMap.Count == 0) return;

        Dictionary<Vector3, float> normalizedMap = new Dictionary<Vector3, float>();
        List<Vector3> validCandidates = new List<Vector3>();

        float maxVal = localMap.Values.Max();
        float minVal = localMap.Values.Min();
        float range = maxVal - minVal;

        if (range <= 1e-9) range = 1f;

        Dictionary<Vector3, float> roundedLocalMap = new Dictionary<Vector3, float>();
        foreach (var kvp in localMap)
        {
            Vector3 k = new Vector3(
                (float)System.Math.Round(kvp.Key.x, 3),
                (float)System.Math.Round(kvp.Key.y, 3),
                (float)System.Math.Round(kvp.Key.z, 3)
            );
            if (!roundedLocalMap.ContainsKey(k)) roundedLocalMap.Add(k, kvp.Value);
        }

        foreach (var worldPos in filtered)
        {
            Vector3 localPos = transform.InverseTransformPoint(worldPos);
            Vector3 key = new Vector3(
                (float)System.Math.Round(localPos.x, 3),
                (float)System.Math.Round(localPos.y, 3),
                (float)System.Math.Round(localPos.z, 3)
            );

            if (roundedLocalMap.TryGetValue(key, out float val))
            {
                float norm = (val - minVal) / range;
                if (saliencyWeightAlpha != 1.0f) norm = Mathf.Pow(norm, saliencyWeightAlpha);
                normalizedMap[worldPos] = norm;
                validCandidates.Add(worldPos);
            }
        }

        Vector3 localSize = meshFilter.sharedMesh.bounds.size;
        Vector3 worldScale = transform.lossyScale;
        Vector3 scaledSize = Vector3.Scale(localSize, new Vector3(Mathf.Abs(worldScale.x), Mathf.Abs(worldScale.y), Mathf.Abs(worldScale.z)));
        float correctWorldL = scaledSize.magnitude;

        var final = SaliencyUtils.SelectVerticesWithMultiplicativeFPS(
            validCandidates, normalizedMap, correctWorldL, recommendedVertexCount
        );

        CreateMarkerRoot();

        Vector3 initialScale = CalculateInverseScale();
        for (int i = 0; i < final.Count; i++) CreateMarker(final[i], initialScale);

        // ★ [수정됨] lossyScale 접근 시 .transform 사용
        if (markerRoot != null) _lastLossyScale = markerRoot.transform.lossyScale;
    }

    void PrepareData() { if (vertexPositions != null && vertexPositions.Count > 0) return; vertexPositions = new Dictionary<int, Vector3>(); Vector3[] positions = meshFilter.sharedMesh.vertices; for (int i = 0; i < positions.Length; i++) vertexPositions[i] = positions[i]; }
    void CreateMarkerRoot() { if (markerRoot == null) { markerRoot = new GameObject("MarkerHolder"); markerRoot.transform.SetParent(this.transform, false); markerRoot.transform.localPosition = Vector3.zero; } }

    // ★ [수정됨] 부모 설정 시 .transform 명시
    void CreateMarker(Vector3 position, Vector3 scale)
    {
        GameObject marker;
        if (markerPrefab != null) marker = Instantiate(markerPrefab);
        else { marker = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(marker.GetComponent<Collider>()); var r = marker.GetComponent<Renderer>(); if (r) { Shader s = Shader.Find("Universal Render Pipeline/Lit"); if (s == null) s = Shader.Find("Standard"); if (s != null) { r.material = new Material(s); r.material.color = Color.red; } else { r.material.color = Color.red; } } }
        marker.transform.position = position;
        marker.transform.SetParent(markerRoot.transform, true); // transform 명시

        marker.transform.localScale = scale;

        activeMarkers.Add(marker);
    }
    public void ClearMarkers() { foreach (var m in activeMarkers) if (m) Destroy(m); activeMarkers.Clear(); }
}
//using System.Collections.Generic;
//using System.Linq;
//using UnityEngine;

//public enum SaliencyMode { Entropy, Curvature, Spectral, CFSCNN, TexMesh }

//public class ProjectionMappingCalibrator : MonoBehaviour
//{
//    [Header("Settings")]
//    public SaliencyMode saliencyMode = SaliencyMode.Entropy;

//    [SerializeField] private bool _visualized = false;

//    public bool visualized
//    {
//        get { return _visualized; }
//        set
//        {
//            if (_visualized != value)
//            {
//                _visualized = value;
//                if (this.enabled) { if (_visualized) Run(); else ClearMarkers(); }
//            }
//        }
//    }

//    public int recommendedVertexCount = 6;
//    [Range(0.1f, 5.0f)] public float saliencyWeightAlpha = 1.0f;

//    [Header("Visualization")]
//    public float markerScale = 5.0f;
//    public GameObject markerPrefab;

//    [Header("References")]
//    public Camera targetCamera;
//    public MeshFilter meshFilter;

//    [HideInInspector]
//    public SilhouetteEdgeMaskRenderer silhouetteRenderer;

//    [Header("Paths")]
//    public string objPath;
//    public string cfsSaliencyPath;
//    public string texSaliencyPath;

//    public static List<Vector3> publicFilteredVertices;

//    private Dictionary<int, Vector3> vertexPositions;
//    private LayerMask visibilityLayerMask;
//    private GameObject markerRoot;
//    private List<GameObject> activeMarkers = new List<GameObject>();
//    private bool isInitialized = false;

//    private void OnValidate() { if (Application.isPlaying && isInitialized && this.enabled) { if (_visualized) Run(); else ClearMarkers(); } }
//    private void OnEnable() { if (isInitialized && _visualized) Run(); }
//    private void OnDisable() { ClearMarkers(); }

//    public void SetPaths(string obj, string cfs, string tex) { this.objPath = obj; this.cfsSaliencyPath = cfs; this.texSaliencyPath = tex; }

//    public void Init()
//    {
//        if (meshFilter == null) meshFilter = GetComponentInChildren<MeshFilter>();

//        if (targetCamera == null)
//        {
//            GameObject camObj = GameObject.FindWithTag("Project Camera");
//            if (camObj != null) targetCamera = camObj.GetComponent<Camera>();
//            else
//            {
//                if (targetCamera == null) targetCamera = Camera.main;
//            }
//        }

//        if (silhouetteRenderer == null)
//        {
//            if (targetCamera != null) silhouetteRenderer = targetCamera.GetComponent<SilhouetteEdgeMaskRenderer>();
//            if (silhouetteRenderer == null) silhouetteRenderer = FindObjectOfType<SilhouetteEdgeMaskRenderer>();
//            if (silhouetteRenderer == null)
//            {
//                silhouetteRenderer = gameObject.AddComponent<SilhouetteEdgeMaskRenderer>();
//                silhouetteRenderer.hideFlags = HideFlags.HideInInspector;
//            }
//        }

//        if (meshFilter == null) return;
//        if (visibilityLayerMask.value == 0) { int layer = LayerMask.NameToLayer("Meshes"); visibilityLayerMask = (layer != -1) ? (1 << layer) : -1; }

//        PrepareData();
//        isInitialized = true;

//        if (this.enabled && _visualized) Run();
//    }

//    public void Run()
//    {
//        ClearMarkers();
//        if (!this.enabled || !_visualized || meshFilter == null || targetCamera == null) return;

//        RenderTexture rtMask = null;
//        if (silhouetteRenderer != null)
//        {
//            silhouetteRenderer.Render(targetCamera, meshFilter);
//            rtMask = silhouetteRenderer.GetEdgeMask();
//        }

//        var visible = SaliencyUtils.GetVisibleVertices(targetCamera, meshFilter, vertexPositions, visibilityLayerMask);
//        if (visible.Count == 0) return;

//        List<Vector3> filtered = new List<Vector3>(visible);

//        if (rtMask != null) filtered = SaliencyUtils.FilterVerticesByEdge(rtMask, filtered, targetCamera, 1.5f);
//        filtered = SaliencyUtils.FilterVerticesByTriangleNormals(filtered, meshFilter, targetCamera, 0.3f);
//        publicFilteredVertices = new List<Vector3>(filtered);

//        if (filtered.Count == 0) return;

//        Dictionary<Vector3, float> localMap = new Dictionary<Vector3, float>();

//        Vector3 originalPos = transform.localPosition;
//        Quaternion originalRot = transform.localRotation;
//        Vector3 originalScale = transform.localScale;

//        List<Vector3> localFiltered = new List<Vector3>();
//        foreach (var v in filtered) localFiltered.Add(transform.InverseTransformPoint(v));

//        transform.localRotation = Quaternion.identity;
//        transform.localScale = Vector3.one;
//        transform.localPosition = Vector3.zero;

//        List<Vector3> proxyWorldVertices = new List<Vector3>();
//        foreach (var lv in localFiltered) proxyWorldVertices.Add(transform.TransformPoint(lv));

//        float normalizedL = Vector3.Distance(meshFilter.sharedMesh.bounds.min, meshFilter.sharedMesh.bounds.max);

//        try
//        {
//            switch (saliencyMode)
//            {
//                case SaliencyMode.Entropy:
//                    var entropyMap = EntropySaliencyComputer.Compute(meshFilter, proxyWorldVertices, normalizedL);
//                    foreach (var kvp in entropyMap)
//                    {
//                        Vector3 localPos = transform.InverseTransformPoint(kvp.Key);
//                        localMap[localPos] = kvp.Value;
//                    }
//                    break;

//                case SaliencyMode.Curvature:
//                    List<Vector3> allLocal = new List<Vector3>(meshFilter.sharedMesh.vertices);
//                    localMap = MeshSaliencyComputer.Compute(meshFilter, allLocal, normalizedL);
//                    break;

//                case SaliencyMode.CFSCNN:
//                case SaliencyMode.TexMesh:
//                    List<Vector3> allLocal2 = new List<Vector3>(meshFilter.sharedMesh.vertices);
//                    if (saliencyMode == SaliencyMode.CFSCNN)
//                        localMap = SaliencyDataLoader.GetSaliencyMap(meshFilter, allLocal2, objPath, cfsSaliencyPath);
//                    else
//                        localMap = SaliencyDataLoader.GetSaliencyMap(meshFilter, allLocal2, objPath, texSaliencyPath);
//                    break;
//            }
//        }
//        catch { }
//        finally
//        {
//            transform.localPosition = originalPos;
//            transform.localRotation = originalRot;
//            transform.localScale = originalScale;
//        }

//        if (localMap == null || localMap.Count == 0) return;

//        Dictionary<Vector3, float> normalizedMap = new Dictionary<Vector3, float>();
//        List<Vector3> validCandidates = new List<Vector3>();

//        float maxVal = localMap.Values.Max();
//        float minVal = localMap.Values.Min();
//        float range = maxVal - minVal;

//        if (range <= 1e-9) range = 1f;

//        Dictionary<Vector3, float> roundedLocalMap = new Dictionary<Vector3, float>();

//        // ★ [여기가 수정됨] 중복 키 경고 방지 로직
//        foreach (var kvp in localMap)
//        {
//            // 소수점 3자리 반올림 시 좌표 충돌 가능성 있음
//            Vector3 k = new Vector3(
//                (float)System.Math.Round(kvp.Key.x, 3),
//                (float)System.Math.Round(kvp.Key.y, 3),
//                (float)System.Math.Round(kvp.Key.z, 3)
//            );

//            // ContainsKey로 먼저 확인하여 경고/에러 방지
//            if (!roundedLocalMap.ContainsKey(k))
//            {
//                roundedLocalMap.Add(k, kvp.Value);
//            }
//            // 이미 있으면 그냥 무시 (첫 번째 값 사용)
//        }

//        foreach (var worldPos in filtered)
//        {
//            Vector3 localPos = transform.InverseTransformPoint(worldPos);
//            Vector3 key = new Vector3(
//                (float)System.Math.Round(localPos.x, 3),
//                (float)System.Math.Round(localPos.y, 3),
//                (float)System.Math.Round(localPos.z, 3)
//            );

//            // TryGetValue는 키가 없어도 에러 안 냄 (경고 안 뜸)
//            if (roundedLocalMap.TryGetValue(key, out float val))
//            {
//                float norm = (val - minVal) / range;
//                if (saliencyWeightAlpha != 1.0f) norm = Mathf.Pow(norm, saliencyWeightAlpha);
//                normalizedMap[worldPos] = norm;
//                validCandidates.Add(worldPos);
//            }
//        }

//        Vector3 localSize = meshFilter.sharedMesh.bounds.size;
//        Vector3 worldScale = transform.lossyScale;
//        Vector3 scaledSize = Vector3.Scale(localSize, new Vector3(Mathf.Abs(worldScale.x), Mathf.Abs(worldScale.y), Mathf.Abs(worldScale.z)));
//        float correctWorldL = scaledSize.magnitude;

//        var final = SaliencyUtils.SelectVerticesWithMultiplicativeFPS(
//            validCandidates, normalizedMap, correctWorldL, recommendedVertexCount
//        );

//        CreateMarkerRoot();
//        for (int i = 0; i < final.Count; i++) CreateMarker(final[i]);
//    }

//    void PrepareData() { if (vertexPositions != null && vertexPositions.Count > 0) return; vertexPositions = new Dictionary<int, Vector3>(); Vector3[] positions = meshFilter.sharedMesh.vertices; for (int i = 0; i < positions.Length; i++) vertexPositions[i] = positions[i]; }
//    void CreateMarkerRoot() { if (markerRoot == null) { markerRoot = new GameObject("MarkerHolder"); markerRoot.transform.SetParent(this.transform, false); markerRoot.transform.localPosition = Vector3.zero; } }
//    void CreateMarker(Vector3 position)
//    {
//        GameObject marker;
//        if (markerPrefab != null) marker = Instantiate(markerPrefab);
//        else { marker = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(marker.GetComponent<Collider>()); var r = marker.GetComponent<Renderer>(); if (r) { Shader s = Shader.Find("Universal Render Pipeline/Lit"); if (s == null) s = Shader.Find("Standard"); if (s != null) { r.material = new Material(s); r.material.color = Color.red; } else { r.material.color = Color.red; } } }
//        marker.transform.position = position; marker.transform.SetParent(markerRoot.transform, true);
//        Vector3 parentScale = transform.lossyScale; float sx = Mathf.Abs(parentScale.x) < 0.0001f ? 1f : parentScale.x; float sy = Mathf.Abs(parentScale.y) < 0.0001f ? 1f : parentScale.y; float sz = Mathf.Abs(parentScale.z) < 0.0001f ? 1f : parentScale.z;
//        marker.transform.localScale = new Vector3(markerScale / sx, markerScale / sy, markerScale / sz);
//        activeMarkers.Add(marker);
//    }
//    public void ClearMarkers() { foreach (var m in activeMarkers) if (m) Destroy(m); activeMarkers.Clear(); }
//}
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using UnityEngine;
//using UnityEditor;

//public enum SaliencyMode { Entropy, Curvature, Spectral, CFSCNN, TexMesh }

//public class ProjectionMappingCalibrator : MonoBehaviour
//{
//    public SaliencyMode saliencyMode = SaliencyMode.Entropy; // Default Entropy
//    public Camera mainCamera;
//    public MeshFilter meshFilter;
//    public int recommendedVertexCount = 6;
//    public bool visualized = true;
//    public SilhouetteEdgeMaskRenderer silhouetteRenderer;
//    public DebugEdgeMaskRenderer debugRenderer;
//    [Range(0f, 1f)] public float topSaliencyPercentage = 0.5f;

//    [수정] CFSCNN 모드일 때 사용할 원본 파일 경로
//   [Header("CFSCNN Source File Paths")]
//    public string cfsSaliencyObjPath = "Assets/Meshes/Chick_Tri.obj";
//    public string cfsSaliencyTxtPath = "Assets/Resources/CfSCNN/Chick_Tri_saliency.txt";

//    [Header("TexMesh Source File Paths")]
//    public string texSaliencyObjPath = "Assets/Meshes/Chick_Tri.obj";
//    public string texSaliencyTxtPath = "Assets/Resources/TexMesh/Chick_vertex_saliency.txt";
//    public static List<Vector3> publicFilteredVertices;

//    private Dictionary<int, Vector3> vertexPositions;
//    private float l;
//    private LayerMask visibilityLayerMask;

//    void Start()
//    {
//        Prepare();
//        silhouetteRenderer.Render();
//        Run();
//    }

//    void Prepare()
//    {
//        Layer 설정 복원
//        int vertexLayer = LayerMask.NameToLayer("Meshes");
//        if (vertexLayer != -1)
//            visibilityLayerMask = (1 << vertexLayer);
//        else
//            visibilityLayerMask = Physics.DefaultRaycastLayers;

//        vertexPositions = new Dictionary<int, Vector3>();
//        Vector3[] positions = meshFilter.sharedMesh.vertices;
//        for (int i = 0; i < positions.Length; i++) vertexPositions[i] = positions[i];
//        VerifyMeshAssetIdentity();
//        Bounds bounds = meshFilter.sharedMesh.bounds;
//        Vector3 scale = meshFilter.transform.lossyScale;
//        Vector3 scaledMin = Vector3.Scale(bounds.min, scale);
//        Vector3 scaledMax = Vector3.Scale(bounds.max, scale);
//        l = Vector3.Distance(scaledMin, scaledMax);

//        Debug.Log($"[Prepare] Computed l (world scale corrected): {l:F3}");
//    }
//    void Run()
//    {
//        var edgeMask = silhouetteRenderer.GetEdgeMask();
//        silhouetteRenderer.SaveSilhouetteMaskToPNG();
//        silhouetteRenderer.SaveEdgeMaskToPNG("SavedSilhouette.png");
//        debugRenderer.edgeMask = edgeMask; // 디버그용 연결

//        var visible = SaliencyUtils.GetVisibleVertices(mainCamera, meshFilter, vertexPositions, visibilityLayerMask);
//        if (visualized)
//        {
//            foreach (var v in visible)
//                SaliencyUtils.HighlightVertex(v, Color.blue, 3.0f, false);
//        }

//        var filtered = SaliencyUtils.FilterVerticesByEdge(edgeMask, visible, mainCamera, distanceThresholdPixels: 1.5f);
//        filtered = SaliencyUtils.FilterVerticesByTriangleNormals(filtered, meshFilter, mainCamera, dotThreshold: 0.3f);
//        publicFilteredVertices = new List<Vector3>(filtered);
//        Debug.Log(publicFilteredVertices);
//        var filtered = SaliencyUtils.FilterVerticesForCalibration(mainCamera, meshFilter, visible);
//        if (visualized)
//        {
//            foreach (var vertex in filtered)
//                SaliencyUtils.HighlightVertex(vertex, Color.green, 4.0f, false);
//        }
//        Saliency 계산
//        Dictionary<Vector3, float> saliencyMap = saliencyMode switch
//        {
//            SaliencyMode.Entropy => EntropySaliencyComputer.Compute(meshFilter, filtered, l),
//            SaliencyMode.Curvature => MeshSaliencyComputer.Compute(meshFilter, filtered, l),
//            SaliencyMode.Spectral => SpectralSaliencyComputer.Compute(meshFilter, filtered, l),
//            SaliencyMode.CFSCNN => SaliencyDataLoader.GetSaliencyMap(meshFilter, filtered, cfsSaliencyObjPath, cfsSaliencyTxtPath),
//            SaliencyMode.TexMesh => SaliencyDataLoader.GetSaliencyMap(meshFilter, filtered, texSaliencyObjPath, texSaliencyTxtPath),
//            _ => throw new System.Exception("Unknown mode")
//        };


//        var topCandidates = filtered;

//        (Optional)entropyMap과 filtered 매칭 체크(디버깅용)
//        EntropySaliencyComputer.CheckFilteredVerticesMatch(saliencyMap, filtered);

//        var final = SaliencyUtils.SelectHybridDistributedVertices(topCandidates, saliencyMap, l, recommendedVertexCount, topSaliencyPercentage); //1
//        var final = SaliencyUtils.SelectVerticesWithAdaptiveNMS(topCandidates, saliencyMap, l, recommendedVertexCount); //2
//        var final = SaliencyUtils.SelectVerticesWithMultiplicativeFPS(topCandidates, saliencyMap, l, recommendedVertexCount); //3

//        Debug.Log($"[Final Recommended] {final.Count} vertices selected.");

//        for (int i = 0; i < final.Count; i++)
//            SaliencyUtils.HighlightVertex(final[i], Color.red, 5.0f, true, i);

//        Debug.Log("Calibration Complete");
//    }
//    private void VerifyMeshAssetIdentity()
//    {
//        Debug.Log("========== FINAL ASSET IDENTITY CHECK ==========");

//        // 1. Inspector에 할당된 메쉬
//        Mesh inspectorMesh = meshFilter.sharedMesh;
//        Debug.Log($"[Inspector Mesh] Name: {inspectorMesh.name}, Instance ID: {inspectorMesh.GetInstanceID()}");
//        if (inspectorMesh.vertexCount > 0)
//        {
//            Debug.Log($"- Vertex 0: {inspectorMesh.vertices[0]:F6}");
//        }

//        // 2. 파일 경로에서 직접 에셋 로드
//        // AssetDatabase는 에디터에서만 동작하는 기능입니다.
//        var loadedObject = AssetDatabase.LoadAssetAtPath<GameObject>(cfsSaliencyObjPath);
//        if (loadedObject != null)
//        {
//            MeshFilter loadedMf = loadedObject.GetComponent<MeshFilter>();
//            if (loadedMf != null)
//            {
//                Mesh pathMesh = loadedMf.sharedMesh;
//                Debug.Log($"[Path-Loaded Mesh] Name: {pathMesh.name}, Instance ID: {pathMesh.GetInstanceID()}");
//                if (pathMesh.vertexCount > 0)
//                {
//                    Debug.Log($"- Vertex 0: {pathMesh.vertices[0]:F6}");
//                }

//                // 3. 두 에셋의 Instance ID 비교
//                if (inspectorMesh.GetInstanceID() == pathMesh.GetInstanceID())
//                {
//                    Debug.Log("[Check Result] The two meshes are the EXACT SAME asset.");
//                }
//                else
//                {
//                    Debug.LogWarning("[Check Result] The two meshes are DIFFERENT assets!");
//                }
//            }
//        }
//        else
//        {
//            Debug.LogError($"[Path Check] 이 경로에서 에셋을 로드할 수 없습니다: {cfsSaliencyObjPath}");
//        }
//        Debug.Log("================================================");
//    }
//}
