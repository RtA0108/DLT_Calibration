using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class NewEntropy : MonoBehaviour
{
    public MeshFilter meshFilter;
    public Camera mainCamera;
    //public float[] sigmaScales = new float[] { 0.1f, 0.2f, 0.3f };
    public float topEntropyPercentage = 0.5f;
    public int bestDistributedVertices = 6;
    public Color visibilityColor = Color.blue;
    public Color highlightColor = Color.red;
    public float highlightSize = 0.05f;
    public LayerMask visibilityLayerMask;


    private float l = 0f;
    private float[] sigmaScales = new float[] { 0.05f, 0.075f, 0.1f };
    private Dictionary<int, Vector3> vertexPositions = new Dictionary<int, Vector3>();
    private Dictionary<int, Vector3> vertexNormals = new Dictionary<int, Vector3>();
    //이 부분 무조건 vector3
    private List<Vector3> visibleVertices = new List<Vector3>();
    private Dictionary<Vector3, bool> uniquePositions = new Dictionary<Vector3, bool>();
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
        for (int i = 0; i < sigmaScales.Length; i++)
        {
            sigmaScales[i] *= l;
        }
        // Store vertex positions and normals
        for (int i = 0; i < positions.Length; i++)
        {
            vertexPositions[i] = positions[i];
            vertexNormals[i] = normals[i];
        }
        
      

        //이걸로 사용하니까 entropy가 2배 가까이 상승하는데 원인 파악 필요//
        visibleVertices = OcclusionCulling.GetVisibleVertices(mainCamera, meshFilter, vertexPositions, visibilityLayerMask);
        visibleVertices = FilterSilhouetteVertices(visibleVertices, 0.5f, 0.08f); 
        // Filter only vertices visible in the camera

        //HighlightVisibleVertices();

        var grouped = visibleVertices.GroupBy(v => v);
        Debug.Log($"중복을 포함한 정점 수: {visibleVertices.Count}");
        Debug.Log($"고유 위치 수: {grouped.Count()}");

        //엔트로피 맵 저장
        Dictionary<Vector3, float> entropyMap = ComputeEntropyForVisibleVertices(visibleVertices);

        //엔트로피가 상위 50%인 정점 선택
        List<Vector3> topEntropyVertices = SelectTopEntropyVertices(entropyMap);

        //상위 50% 정점에서 가장 분산된 6개 정점 선택
        //(vertices, saliency map, highlight count, alpha)
        List<Vector3> finalVertices = SelectHybridDistributedVertices(topEntropyVertices, entropyMap, 6, alpha: 0.6f);


        //최종 정점 사용자에게 추천
        Debug.Log("Projection Mapping Calibration을 위한 최적 정점 추천 완료!");

    }
    private List<Vector3> FilterSilhouetteVertices(List<Vector3> vertices, float dotThreshold = 0.3f, float varianceThreshold = 0.15f)
    {
        List<Vector3> filtered = new List<Vector3>();
        Vector3 cameraPos = mainCamera.transform.position;

        foreach (Vector3 v in vertices)
        {
            Vector3 normal = GetVertexNormal(v).normalized;
            Vector3 toCamera = (cameraPos - v).normalized;

            //cameraDot이 높을수록 정면, 즉 dotThreshold가 높을수록 꼼꼼히 검토하는 것
            float cameraDot = Mathf.Abs(Vector3.Dot(normal, toCamera));
           // Debug.Log($"Vertex {v} → cameraDot: {cameraDot}");
            // 1단계: normal과 카메라 시선이 수직에 가까운가?
            if (cameraDot > dotThreshold)
            {
                filtered.Add(v); // 정면을 향하고 있음 → 실루엣 아님
                
                continue;
               
            }

            // 2단계: 주변 normal과의 평균 편차가 작은가?
            float sigma = sigmaScales.Min();
            List<Vector3> neighbors = GetNeighborsByEuclideanDistance(v, sigma, vertices.ToArray()); // sigma 조절 가능
            if (neighbors.Count == 0)
            {
                filtered.Add(v); // 주변 없으면 유지
                //Debug.Log("Hi----------------------------------");
                continue; 
            }

            float variance = 0f;
            foreach (Vector3 n in neighbors)
            {
                Vector3 neighborNormal = GetVertexNormal(n).normalized;
                float dot = Vector3.Dot(normal, neighborNormal);
                variance += (1f - dot); // 방향 차이 누적
                //Debug.Log($"Vertex: {v}, Dot: {cameraDot:F2}, Var: {variance:F2}");
            }

            variance /= neighbors.Count;

            if (variance < varianceThreshold)
            {
                filtered.Add(v); // 곡률이 급하지 않음 → 유지
            }
            else
            {
                // 실루엣 정점으로 판단 → 제거
                //Debug.Log($"[Silhouette Removed] {v}, Dot: {cameraDot:F2}, Var: {variance:F2}");
            }

        }

        Debug.Log($"[Silhouette Filter] Before: {vertices.Count}, After: {filtered.Count}");
        return filtered;
    }
    //필터링 / mesh의 center를 기준으로 판단. 하지만 지금은 mesh의 턱부분만 필터링됌.
    private List<Vector3> FilterEdgeVertices(List<Vector3> vertices)
    {
        Vector3 cameraForward = mainCamera.transform.forward;
        Vector3 cameraPosition = mainCamera.transform.position;

        int beforeFiltering = vertices.Count; // 필터링 전 정점 개수

        List<Vector3> filteredVertices = vertices.Where(v =>
        {
            Vector3 toVertex = (v - cameraPosition).normalized;
            float angle = Vector3.Angle(cameraForward, toVertex);

            //  시야각이 50도 이상이면 제외 (카메라 기준 끝에 있는 정점 제거 - 기존 60도에서 50도로 조정)
            if (angle > 50.0f) return false;

            // Mesh의 중앙부에 가까운 Vertex 선호 (끝부분 제거)
            float distanceFromCenter = Vector3.Distance(v, meshFilter.mesh.bounds.center);
            float maxAllowedDistance = meshFilter.mesh.bounds.extents.magnitude * 0.8f; // 90% → 80%로 조정하여 더 많은 정점 제거

            return distanceFromCenter < maxAllowedDistance;
        }).ToList();

        int afterFiltering = filteredVertices.Count; // 필터링 후 정점 개수
        Debug.Log($"FilterEdgeVertices: Removed {beforeFiltering - afterFiltering} edge vertices.");

        return filteredVertices;
    }

        //----------------------------------------------------------------------------------------------------------

        //보이는 vertex 선정
    private void HighlightVisibleVertices()
    {
        visibleVertices.Clear();
        uniquePositions.Clear();

        MeshRenderer meshRenderer = GetComponentInChildren<MeshRenderer>();
        Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(mainCamera);
        Bounds bounds = meshRenderer.bounds;
        if (!GeometryUtility.TestPlanesAABB(frustumPlanes, bounds)) return;

        Vector3 camPos = mainCamera.transform.position;
        //Dictionary<Vector3, bool> uniqueCheck = new Dictionary<Vector3, bool>(); // 중복 제거용
        Dictionary<Vector3, Vector3> uniqueCheck = new Dictionary<Vector3, Vector3>(); // 중복 제거 + Highlight 좌표 저장
        foreach (var kvp in vertexPositions)
        {
            int vertexIndex = kvp.Key;
            Vector3 vertexWorldPos = meshFilter.transform.TransformPoint(kvp.Value);
            

            Vector3 roundedVertex = new Vector3(
                Mathf.Round(vertexWorldPos.x * 1000f) / 1000f,
                Mathf.Round(vertexWorldPos.y * 1000f) / 1000f,
                Mathf.Round(vertexWorldPos.z * 1000f) / 1000f
            );

            Vector3 worldNormal = meshFilter.transform.TransformDirection(vertexNormals[vertexIndex]);
            Vector3 toCamera = (camPos - vertexWorldPos).normalized;

            if (Vector3.Dot(worldNormal, toCamera) <= 0) continue;

            Ray ray = new Ray(camPos, (vertexWorldPos - camPos).normalized);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (Vector3.Distance(hit.point, vertexWorldPos) < 0.01f)
                {
                    if (!uniqueCheck.ContainsKey(roundedVertex))
                    {
                        uniqueCheck[roundedVertex] = vertexWorldPos;
                        HighlightVertex(vertexWorldPos, Color.blue, 5.5f, false); // 파란색으로 Highlight
                    }

                    visibleVertices.Add(vertexWorldPos); // 중복 제거 후 보이는 정점만 추가
                    uniquePositions[roundedVertex] = true;
                }
            }
            
            //Debug.Log($"Total Unique Visible Vertices: {visibleVertices.Count}");
        }

        //int beforeFiltering = visibleVertices.Count;
        //visibleVertices = FilterEdgeVertices(visibleVertices);
        //int afterFiltering = visibleVertices.Count; ;
        //Debug.Log($"Before Filtering: {beforeFiltering} vertices, After Filtering: {afterFiltering} vertices");
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
        float[] sigmaScales = new float[] { 0.05f * l, 0.1f * l, 0.2f * l };
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

    //Neighbor 탐지 (euclidean distance 기반)
    //private List<Vector3> GetNeighborsByEuclideanDistance(Vector3 position, float sigma, Vector3[] vertices, int maxNeighbors = 50)
    //{


    //    List<Vector3> neighbors = new List<Vector3>();

    //    float adaptiveSigma = sigma;
    //    while (neighbors.Count < 5 && adaptiveSigma < sigma * 4) // sigma 최대 4배까지 증가 가능
    //    {
    //        neighbors.Clear();
    //        foreach (Vector3 vertex in vertices)
    //        {
    //            float distance = Vector3.Distance(position, vertex);

    //            if (distance > 0f && distance <= adaptiveSigma)
    //            {
    //                neighbors.Add(vertex);
    //            }
    //        }

    //        if (neighbors.Count < 5)
    //            adaptiveSigma *= 1.2f; // sigma 값 증가
    //    }

    //    return neighbors;
    //}

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

    //distance 기반으로 추천되는 vertex의 분산 보장
    private List<Vector3> SelectMaxMinDistanceVertices(List<Vector3> topEntropyVertices, Dictionary<Vector3, float> entropyMap, int selectionCount)
    {
        List<Vector3> distributedVertices = new List<Vector3>();

        if (topEntropyVertices.Count == 0)
            return distributedVertices;

        // 가장 높은 엔트로피 값을 가진 정점을 첫 번째로 선택
        distributedVertices.Add(topEntropyVertices[0]);
        topEntropyVertices.RemoveAt(0);

        while (distributedVertices.Count < selectionCount && topEntropyVertices.Count > 0)
        {
            float bestScore = float.MinValue;
            Vector3 selectedPos = Vector3.zero;

            foreach (var candidatePos in topEntropyVertices)
            {
                float minDist = float.MaxValue;

                // 현재 후보 정점과 기존에 선택된 정점들 간의 최소 거리 계산
                foreach (Vector3 selPos in distributedVertices)
                {
                    float dist = Vector3.Distance(candidatePos, selPos);
                    if (dist < minDist)
                        minDist = dist;
                }

                // 거리와 엔트로피를 조합한 점수 계산
                float entropyValue = entropyMap.ContainsKey(candidatePos) ? entropyMap[candidatePos] : 0f;
                float weightedScore = minDist * entropyValue; // 거리 x 엔트로피

                // 가장 높은 점수를 가진 정점을 선택
                if (weightedScore > bestScore)
                {
                    bestScore = weightedScore;
                    selectedPos = candidatePos;
                }
            }

            if (selectedPos != Vector3.zero)
            {
                distributedVertices.Add(selectedPos);
                topEntropyVertices.Remove(selectedPos);
            }
            else
            {
                break;
            }
        }

        Debug.Log("===== 최종 선택된 6개 정점 (RED) =====");
        foreach (Vector3 vertex in distributedVertices)
        {
            Debug.Log($"[RED] 최종 선택된 정점: {vertex}, 엔트로피: {entropyMap[vertex]}");
            HighlightVertex(vertex, Color.red, 7f, true);
        }

        return distributedVertices;
    }

    //private List<Vector3> SelectHybridDistributedVertices(List<Vector3> candidates, Dictionary<Vector3, float> entropyMap, int k)
    //{
    //    List<Vector3> selected = new List<Vector3>();

    //    if (candidates.Count < k) return new List<Vector3>(candidates);

    //    // ----------------------------
    //    // Step 1: 첫 번째 vertex 선정 (entropy + viewport proximity)
    //    // ----------------------------
    //    float alpha = 0.5f;  // entropy vs viewport proximity 비율
    //    Vector3 firstPoint = Vector3.zero;
    //    float firstScore = float.MinValue;

    //    foreach (var v in candidates)
    //    {
    //        float entropy = entropyMap[v];

    //        // Viewport proximity 계산
    //        Vector3 viewportPos = mainCamera.WorldToViewportPoint(v);
    //        float viewportProximity = 1f - Vector2.Distance(new Vector2(viewportPos.x, viewportPos.y), new Vector2(0.5f, 0.5f)) * 2f;
    //        viewportProximity = Mathf.Clamp01(viewportProximity);

    //        // Hybrid score
    //        float score = alpha * entropy + (1 - alpha) * viewportProximity;

    //        if (score > firstScore)
    //        {
    //            firstScore = score;
    //            firstPoint = v;
    //        }
    //    }

    //    selected.Add(firstPoint);
    //    Debug.Log($"[Selected First] {firstPoint}, score: {firstScore:F3}, entropy: {entropyMap[firstPoint]:F3}");

    //    // ----------------------------
    //    // Step 2: max-min + entropy 하이브리드 분산
    //    // ----------------------------
    //    while (selected.Count < k)
    //    {
    //        Vector3 bestPoint = Vector3.zero;
    //        float bestScore = float.MinValue;

    //        foreach (var v in candidates)
    //        {
    //            if (selected.Contains(v)) continue;

    //            float minDist = selected.Min(s => Vector3.Distance(s, v));
    //            float score = minDist * entropyMap[v];  // 여기는 그대로 하이브리드 분산

    //            if (score > bestScore)
    //            {
    //                bestScore = score;
    //                bestPoint = v;
    //            }
    //        }

    //        if (bestPoint != Vector3.zero)
    //        {
    //            selected.Add(bestPoint);
    //            Debug.Log($"[Selected] {bestPoint}, score: {bestScore:F3}, entropy: {entropyMap[bestPoint]:F3}");
    //        }
    //        else
    //        {
    //            break;
    //        }
    //    }

    //    // ----------------------------
    //    // Step 3: 하이라이트
    //    // ----------------------------
    //    for (int i = 0; i < selected.Count; i++)
    //    {
    //        HighlightVertex(selected[i], Color.red, 7f, true, i);
    //    }

    //    return selected;
    //}
    private List<Vector3> SelectHybridDistributedVertices(List<Vector3> candidates, Dictionary<Vector3, float> entropyMap, int selectionCount, float alpha = 0.5f)
    {
        List<Vector3> selected = new List<Vector3>();

        if (candidates.Count <= selectionCount)
            return new List<Vector3>(candidates);

        // --- Step 1: 가장 높은 엔트로피를 가진 정점으로 첫 정점 선택 ---
        Vector3 first = candidates.OrderByDescending(v => entropyMap[v]).First();
        selected.Add(first);
        candidates.Remove(first);

        // --- Step 2: Hybrid 분산 선택 ---
        while (selected.Count < selectionCount)
        {
            Vector3 bestVertex = Vector3.zero;
            float bestScore = float.MinValue;

            foreach (var candidate in candidates)
            {
                // 최소 거리 계산
                float minDist = float.MaxValue;
                foreach (var s in selected)
                {
                    float dist = Vector3.Distance(candidate, s);
                    if (dist < minDist) minDist = dist;
                }

                // 하이브리드 점수 (alpha 조절)
                float score = alpha * entropyMap[candidate] + (1 - alpha) * (minDist / l); // l로 정규화된 거리 사용
                if (score > bestScore)
                {
                    bestScore = score;
                    bestVertex = candidate;
                }
            }

            if (bestVertex != Vector3.zero)
            {
                selected.Add(bestVertex);
                candidates.Remove(bestVertex);
            }
            else
            {
                break;
            }
        }

        // --- 하이라이트 ---
        for (int i = 0; i < selected.Count; i++)
        {
            var vertex = selected[i];
            Debug.Log($"[RED] 추천된 정점: {vertex}, Entropy: {entropyMap[vertex]}");
            HighlightVertex(vertex, Color.red, 7f, true, i);
        }

        return selected;
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


