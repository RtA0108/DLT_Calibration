using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class NewEntropy : MonoBehaviour
{
    public MeshFilter meshFilter;
    public Camera mainCamera;
    public float topEntropyPercentage = 0.5f;
    public int bestDistributedVertices = 6;
    public LayerMask visibilityLayerMask;


    private float l = 0f;
    private float[] sigmaScales = new float[] { 0.05f, 0.1f, 0.2f };
    private Dictionary<int, Vector3> vertexPositions = new Dictionary<int, Vector3>();
    private Dictionary<int, Vector3> vertexNormals = new Dictionary<int, Vector3>();
    //이 부분 무조건 vector3
    private List<Vector3> visibleVertices = new List<Vector3>();
    void Start()
    {
        //occlusion culling을 위한 사전 작업 (가시적으로 생성된 vertex raycasting 제외, mesh 관련 사전 처리)
        int vertexLayer = LayerMask.NameToLayer("Vertex In 3D");
        if (vertexLayer != -1)
        {
            visibilityLayerMask = ~(1 << vertexLayer); // Exclude from Raycasting
        }
        else
        {
            Debug.LogWarning("Layer 'Vertex In 3D' not found. Raycasting will include all layers.");
            visibilityLayerMask = Physics.DefaultRaycastLayers; //If not found, include all layers
        }
        if (meshFilter == null || mainCamera == null)
        {
            Debug.LogError("MeshFilter or Main Camera is not assigned.");
            return;
        }
        // Ensure the Mesh has a Collider for Raycasting
        if (meshFilter.gameObject.GetComponent<MeshCollider>() == null)
        {
            meshFilter.gameObject.AddComponent<MeshCollider>();
        }

        Mesh mesh = meshFilter.mesh;
        Vector3[] positions = mesh.vertices;
        Vector3[] normals = mesh.normals;
        //sigmaScale setting
        l = ComputeMeshCharacteristicLength(meshFilter);
        for (int i = 0; i < sigmaScales.Length; i++) sigmaScales[i] *= l;
        // Store vertex positions and normals
        for (int i = 0; i < positions.Length; i++)
        {
            vertexPositions[i] = positions[i];
            vertexNormals[i] = normals[i];
        }


        //이걸로 사용하니까 entropy가 2배 가까이 상승하는데 원인 파악 필요//
        visibleVertices = SaliencyUtils.GetVisibleVertices(mainCamera, meshFilter, vertexPositions, visibilityLayerMask);
        //visibleVertices = SaliencyUtils.FilterSilhouetteVertices(mainCamera, meshFilter, visibleVertices, sigmaScales.Min(), 0.5f, 0.08f);

        //var grouped = visibleVertices.GroupBy(v => v);
        //Debug.Log($"중복을 포함한 정점 수: {visibleVertices.Count}");
        //Debug.Log($"고유 위치 수: {grouped.Count()}");

        //엔트로피 맵 저장
        Dictionary<Vector3, float> entropyMap = ComputeEntropyForVisibleVertices(visibleVertices);

        //엔트로피가 상위 50%인 정점 선택
        List<Vector3> topEntropyVertices = SelectTopEntropyVertices(entropyMap);

        //상위 50% 정점에서 가장 분산된 6개 정점 선택
        //(vertices, saliency map, highlight count, alpha)
        List<Vector3> finalVertices = SaliencyUtils.SelectHybridDistributedVertices(topEntropyVertices, entropyMap, l, bestDistributedVertices, alpha: 0.6f);

        for (int i = 0; i < finalVertices.Count; i++)
        {
            SaliencyUtils.HighlightVertex(finalVertices[i], Color.red, 7f, true, i);
        }
        //최종 정점 사용자에게 추천
        Debug.Log("Projection Mapping Calibration을 위한 최적 정점 추천 완료!");

    }


    //----------------------------------------------------------------------------------------------------------

    //entropy Map 저장 (보이는 vertex 한정)
    private Dictionary<Vector3, float> ComputeEntropyForVisibleVertices(List<Vector3> visibleVertices)
    {
        Dictionary<Vector3, float> entropyMap = new Dictionary<Vector3, float>();
        float[] entropyValues = ComputeVertexEntropy(visibleVertices, l);

        Debug.Log("===== Entropy Values for Visible Vertices =====");

        for (int i = 0; i < visibleVertices.Count; i++)
        {
            entropyMap[visibleVertices[i]] = entropyValues[i];
           //Debug.Log($"Visible Vertex {i}: {visibleVertices[i]}, Entropy: {entropyValues[i]}");
        }

        return entropyMap;
    }
    //Entropy 계산

    private float[] ComputeVertexEntropy(List<Vector3> visibleVertices, float l)
    {
        float[] entropyValues = new float[visibleVertices.Count];
        Vector3[] vertexArray = visibleVertices.ToArray();
        

        // Multi-Scale 적용 (작은 sigma부터 점진적으로 Neighbor 확장)
        //float[] sigmaScales = new float[] { 0.05f * l, 0.1f * l, 0.2f * l };
        float sigma_max = sigmaScales.Max(); // 기존 방식 유지
        Debug.Log("sigma_max: " + sigma_max);
        for (int i = 0; i < visibleVertices.Count; i++)
        {
            List<Vector3> allNeighbors = GetNeighborsByEuclideanDistance(visibleVertices[i], sigma_max, vertexArray);
            
            float aggregatedEntropy = 0f;
            float totalWeight = 0f;

            foreach (float sigma in sigmaScales)
            {
                // `sigma_max`에서 찾은 Neighbor 중 `sigma` 범위 내에서 필터링
                List<Vector3> neighbors = allNeighbors.Where(v => Vector3.Distance(v, visibleVertices[i]) <= sigma).ToList();
                if (neighbors.Count == 0) continue;

                float entropy = 0f;
                int totalNormals = neighbors.Count;

                Dictionary<Vector3Int, int> normalHistogram = new Dictionary<Vector3Int, int>();

                foreach (Vector3 neighbor in neighbors)
                {
                    Vector3 normal = GetVertexNormal(neighbor);
                    Vector3Int quantizedNormal = QuantizeNormal(normal, 1000); // 기존 scale 값 유지

                    if (!normalHistogram.ContainsKey(quantizedNormal))
                    {
                        normalHistogram[quantizedNormal] = 0;
                    }
                    normalHistogram[quantizedNormal]++;
                }

                foreach (var count in normalHistogram.Values)
                {
                    float probability = (float)count / (totalNormals + 1e-6f);
                    if (probability > 0)
                    {
                        entropy -= probability * Mathf.Log(probability + 1e-6f);
                    }
                }

                // 기존 방식과 동일한 엔트로피 정규화 적용
                float maxEntropy = Mathf.Log(neighbors.Count + 1e-6f);
                float normalizedEntropy = entropy / maxEntropy;

                float weight = 1.0f / sigma; // 작은 sigma가 더 큰 영향을 주도록 가중치 적용
                aggregatedEntropy += normalizedEntropy * weight;
                totalWeight += weight;
            }

            entropyValues[i] = aggregatedEntropy / totalWeight; // 가중 평균 적용
        }

        return entropyValues;
    }

    //논문에 있는 부분(추측). neighbor를 구하기 위한 bounding box 길이 계산
    private float ComputeMeshCharacteristicLength(MeshFilter meshFilter)
    {
        Bounds bounds = meshFilter.mesh.bounds;
        return Vector3.Distance(bounds.min, bounds.max); // 바운딩 박스 대각선 길이 반환
    }


    private List<Vector3> GetNeighborsByEuclideanDistance(Vector3 position, float sigma, Vector3[] vertices)
    {
        List<Vector3> neighbors = new List<Vector3>();

        float sigmaSquared = sigma * sigma; // 거리 계산을 제곱 비교로 최적화

        foreach (Vector3 vertex in vertices)
        {
            float distanceSquared = (position - vertex).sqrMagnitude; // 거리 제곱값 계산
            if (distanceSquared > 0f && distanceSquared <= sigmaSquared)
            {
                neighbors.Add(vertex);
            }
        }

        return neighbors;
    }

    //entropy 계산을 위해 normal 확보
    private Vector3 GetVertexNormal(Vector3 vertexPosition)
    {
        foreach (var kvp in vertexPositions)
        {
            if (Vector3.Distance(meshFilter.transform.TransformPoint(kvp.Value), vertexPosition) < 0.001f)
            {
                return vertexNormals[kvp.Key]; // Return the stored normal
            }
        }
        return Vector3.zero; // Return zero vector if no normal is found
    }
    //양자화
    private Vector3Int QuantizeNormal(Vector3 normal, int scale = 1000)
    {
        return new Vector3Int(
            Mathf.RoundToInt(normal.x * scale),
            Mathf.RoundToInt(normal.y * scale),
            Mathf.RoundToInt(normal.z * scale)
        );
    }


    //----------------------------------------------------------------------------------------------------------

    // entropy value가 높은 vertices n% 선정
    private List<Vector3> SelectTopEntropyVertices(Dictionary<Vector3, float> entropyMap)
    {
        int numTopEntropy = Mathf.CeilToInt(entropyMap.Count * topEntropyPercentage);
        List<Vector3> topEntropyVertices = entropyMap.OrderByDescending(item => item.Value)
                                               .Take(numTopEntropy)
                                               .Select(item => item.Key)
                                               .ToList();

        Debug.Log($"===== Top {topEntropyPercentage * 100}% High-Entropy Vertices (Green) =====");
        foreach (Vector3 vertex in topEntropyVertices)
        {
            Debug.Log($"[GREEN] High Entropy Vertex: {vertex}, Entropy: {entropyMap[vertex]}");
            //HighlightVertex(vertex, Color.green, 6f, false);
        }

        return topEntropyVertices;
    }
   
    //----------------------------------------------------------------------------------------------------------

    //하이라이트
    private void HighlightVertex(Vector3 position, Color color, float scale, bool isEntropyHighlight, int index = -1)
    {

        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.position = position;
        sphere.transform.localScale = Vector3.one * scale;
        if (isEntropyHighlight)
        {
            sphere.name = $"HighVertex_{index}";
        }

        Renderer sphereRenderer = sphere.GetComponent<Renderer>();
        if (sphereRenderer != null)
        {
            // Use a separate material instance only if needed
            if (isEntropyHighlight)
            {
                //entropy highlight

                sphereRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
                //sphere.transform.localScale = Vector3.one * 7;
            }
            else
            {
                sphereRenderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                sphereRenderer.sharedMaterial.color = color;
            }
        }
    }
}


