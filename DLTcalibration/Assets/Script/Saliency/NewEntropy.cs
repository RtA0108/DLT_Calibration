using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class NewEntropy : MonoBehaviour
{
    public MeshFilter meshFilter;
    public Camera mainCamera;
    public float[] sigmaScales = new float[] { 0.1f, 0.2f, 0.3f };
    public float topEntropyPercentage = 0.5f;
    public int bestDistributedVertices = 6;
    public Color visibilityColor = Color.blue;
    public Color highlightColor = Color.red;
    public float highlightSize = 0.05f;
    public LayerMask visibilityLayerMask;


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

        // Store vertex positions and normals
        for (int i = 0; i < positions.Length; i++)
        {
            vertexPositions[i] = positions[i];
            vertexNormals[i] = normals[i];
        }


        // Filter only vertices visible in the camera
        HighlightVisibleVertices();

        Dictionary<Vector3, float> entropyMap = ComputeEntropyForVisibleVertices(visibleVertices);

        //엔트로피가 상위 50%인 정점 선택
        List<Vector3> topEntropyVertices = SelectTopEntropyVertices(entropyMap);

        //상위 50% 정점에서 가장 분산된 6개 정점 선택
        List<Vector3> finalVertices = SelectMaxMinDistanceVertices(topEntropyVertices, entropyMap, 6);

        //최종 정점 사용자에게 추천
        Debug.Log("Projection Mapping Calibration을 위한 최적 정점 추천 완료!");

    }

    private void HighlightVisibleVertices()
    {
        visibleVertices.Clear();
        uniquePositions.Clear();

        MeshRenderer meshRenderer = GetComponentInChildren<MeshRenderer>();
        Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(mainCamera);
        Bounds bounds = meshRenderer.bounds;
        if (!GeometryUtility.TestPlanesAABB(frustumPlanes, bounds)) return;

        Vector3 camPos = mainCamera.transform.position;

        foreach (var kvp in vertexPositions)
        {
            int vertexIndex = kvp.Key;
            Vector3 vertexWorldPos = meshFilter.transform.TransformPoint(kvp.Value);
            Vector3 vertexNormal = vertexNormals[vertexIndex];
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
                    visibleVertices.Add(vertexWorldPos); // 보이는 정점을 리스트에 추가
                    uniquePositions[roundedVertex] = true;
                    //HighlightVertex(vertexWorldPos, visibilityColor, false);
                }
            }

            Debug.Log($"Total Unique Visible Vertices: {visibleVertices.Count}");
        }
    }


    private Dictionary<Vector3, float> ComputeEntropyForVisibleVertices(List<Vector3> visibleVertices)
    {
        Dictionary<Vector3, float> entropyMap = new Dictionary<Vector3, float>();
        float[] entropyValues = ComputeVertexEntropy(visibleVertices);

        Debug.Log("===== Entropy Values for Visible Vertices =====");

        for (int i = 0; i < visibleVertices.Count; i++)
        {
            entropyMap[visibleVertices[i]] = entropyValues[i];
            Debug.Log($"[BLUE] Vertex: {visibleVertices[i]}, Entropy: {entropyValues[i]}");
        }

        return entropyMap;
    }
    private List<Vector3> SelectTopEntropyVertices(Dictionary<Vector3, float> entropyMap)
    {
        int numTopEntropy = Mathf.CeilToInt(entropyMap.Count * topEntropyPercentage);
        List<Vector3> topEntropyVertices = entropyMap.OrderByDescending(item => item.Value)
                                               .Take(numTopEntropy)
                                               .Select(item => item.Key)
                                               .ToList();

        Debug.Log("===== Top 10% High-Entropy Vertices (Green) =====");
        foreach (Vector3 vertex in topEntropyVertices)
        {
            Debug.Log($"[GREEN] High Entropy Vertex: {vertex}, Entropy: {entropyMap[vertex]}");
            HighlightVertex(vertex, Color.green, false);
        }

        return topEntropyVertices;
    }
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
            HighlightVertex(vertex, Color.red, true);
        }

        return distributedVertices;
    }


    //entropy 계산 작업 수행
    private float[] ComputeVertexEntropy(List<Vector3> visibleVertices)
    {
        float[] entropyValues = new float[visibleVertices.Count];
        Vector3[] vertexArray = visibleVertices.ToArray();
        float L = ComputeMeshCharacteristicLength(vertexPositions.Values.ToArray());

        float sigma = 0.1f * L; // 논문에서는 5%l 사용 (기본적으로 5% 바운딩 박스 대각선)

        for (int i = 0; i < visibleVertices.Count; i++)
        {
            // 현재 정점의 이웃 정점 찾기
            List<Vector3> neighbors = GetNeighborsByEuclideanDistance(visibleVertices[i], sigma, vertexArray);

            neighbors = neighbors.Where(v => !Mathf.Approximately(Vector3.Distance(v, visibleVertices[i]), 0f)).ToList();

            Debug.Log($"[INFO] Vertex {i}: Neighbors Found = {neighbors.Count}");

            if (neighbors.Count == 0) 
            {
                entropyValues[i] = 0f; // 이웃이 없으면 엔트로피 0
                continue;
            }

            // 히스토그램 생성 (법선 벡터의 분포 계산)
            Dictionary<Vector3Int, int> normalHistogram = new Dictionary<Vector3Int, int>();

            foreach (Vector3 neighbor in neighbors)
            {
                Vector3 normal = GetVertexNormal(neighbor);
                Vector3Int quantizedNormal = QuantizeNormal(normal, 300);

                if (!normalHistogram.ContainsKey(quantizedNormal))
                {
                    normalHistogram[quantizedNormal] = 0;
                }
                normalHistogram[quantizedNormal]++;
            }

            // 샤논 엔트로피 계산
            int totalNormals = neighbors.Count;
            float entropy = 0f;

            foreach (var count in normalHistogram.Values)
            {
                float probability = (float)count / totalNormals;

                // 확률이 0이 아니어야 log 계산 가능
                if (probability > 0)
                {
                    entropy -= probability * Mathf.Log(probability + 1e-6f); // log(0) 방지
                }
            }

            float maxEntropy = Mathf.Log(totalNormals + 1e-6f);
            entropyValues[i] = entropy / maxEntropy;
        }

        return entropyValues;
    }
    private Vector3Int QuantizeNormal(Vector3 normal, int scale = 1000)
    {
        return new Vector3Int(
            Mathf.RoundToInt(normal.x * scale),
            Mathf.RoundToInt(normal.y * scale),
            Mathf.RoundToInt(normal.z * scale)
        );
    }
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
    private float ComputeMeshCharacteristicLength(Vector3[] vertices)
    {
        float totalDistance = 0f;
        int totalEdges = 0;

        for (int i = 0; i < vertices.Length; i++)
        {
            for (int j = i + 1; j < vertices.Length; j++)
            {
                totalDistance += Vector3.Distance(vertices[i], vertices[j]);
                totalEdges++;
            }
        }

        return totalEdges > 0 ? totalDistance / totalEdges : 1f;
    }

    private List<Vector3> GetNeighborsByEuclideanDistance(Vector3 position, float sigma, Vector3[] vertices, int maxNeighbors = 50)
    {
        List<Vector3> neighbors = new List<Vector3>();

        float adaptiveSigma = sigma;
        while (neighbors.Count < 5 && adaptiveSigma < sigma * 4) // sigma 최대 4배까지 증가 가능
        {
            neighbors.Clear(); 
            foreach (Vector3 vertex in vertices)
            {
                float distance = Vector3.Distance(position, vertex);
                if (distance > 0f && distance <= adaptiveSigma)
                {
                    neighbors.Add(vertex);
                }
            }

            if (neighbors.Count < 5)
                adaptiveSigma *= 1.5f; // sigma 값 증가
        }

        // 이웃 정점이 너무 많으면 일정 개수까지만 유지 (최대 50개)
        if (neighbors.Count > maxNeighbors)
        {
            neighbors = neighbors.OrderBy(v => Vector3.Distance(position, v)).Take(maxNeighbors).ToList();
        }

        return neighbors;
    }

    private void HighlightVertex(Vector3 position, Color color, bool isEntropyHighlight, int index = -1)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.position = position;
        sphere.transform.localScale = Vector3.one * highlightSize;
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
                sphere.transform.localScale = Vector3.one * 7;
            }
            else
            {
                sphereRenderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                sphereRenderer.sharedMaterial.color = color;
            }
        }
    }
}


//using UnityEngine;
//using System.Collections.Generic;
//using System.Linq;

//public class NewEntropy : MonoBehaviour
//{
//    public MeshFilter meshFilter;
//    public Camera mainCamera;
//    public float[] sigmaScales = new float[] { 0.1f, 0.2f, 0.3f };
//    public float topEntropyPercentage = 0.5f;
//    public int bestDistributedVertices = 6;
//    public Color visibilityColor = Color.blue;
//    public Color highlightColor = Color.red;
//    public float highlightSize = 0.05f;
//    public LayerMask visibilityLayerMask;


//    private Dictionary<int, Vector3> vertexPositions = new Dictionary<int, Vector3>();
//    private Dictionary<int, Vector3> vertexNormals = new Dictionary<int, Vector3>();
//    //이 부분 무조건 vector3
//    private List<Vector3> visibleVertices = new List<Vector3>();
//    private Dictionary<Vector3, bool> uniquePositions = new Dictionary<Vector3, bool>();
//    void Start()
//    {
//        //occlusion culling을 위한 사전 작업 (가시적으로 생성된 vertex raycasting 제외, mesh 관련 사전 처리)
//        int vertexLayer = LayerMask.NameToLayer("Vertex In 3D");
//        if (vertexLayer != -1)
//        {
//            visibilityLayerMask = ~(1 << vertexLayer); // Exclude from Raycasting
//        }
//        else
//        {
//            Debug.LogWarning("Layer 'Vertex In 3D' not found. Raycasting will include all layers.");
//            visibilityLayerMask = Physics.DefaultRaycastLayers; //If not found, include all layers
//        }
//        if (meshFilter == null || mainCamera == null)
//        {
//            Debug.LogError("MeshFilter or Main Camera is not assigned.");
//            return;
//        }

//        // Ensure the Mesh has a Collider for Raycasting
//        if (meshFilter.gameObject.GetComponent<MeshCollider>() == null)
//        {
//            meshFilter.gameObject.AddComponent<MeshCollider>();
//        }

//        Mesh mesh = meshFilter.mesh;
//        Vector3[] positions = mesh.vertices;
//        Vector3[] normals = mesh.normals;

//        // Store vertex positions and normals
//        for (int i = 0; i < positions.Length; i++)
//        {
//            vertexPositions[i] = positions[i];
//            vertexNormals[i] = normals[i];
//        }


//        // Filter only vertices visible in the camera
//        HighlightVisibleVertices();

//        Dictionary<Vector3, float> entropyMap = ComputeEntropyForVisibleVertices(visibleVertices);

//        // Select the top 10% highest entropy vertices
//        List<Vector3> topEntropyVertices = SelectTopEntropyVertices(entropyMap);

//        // Select 6 widely distributed vertices using Max-Min Distance
//        List<Vector3> distributedVertices = SelectMaxMinDistanceVertices(topEntropyVertices, entropyMap, 20);
//        // Highlight selected vertices
//        List<Vector3> refinedVertices = RefineByEntropy(distributedVertices, entropyMap);

//        // Final Selection Based on Total Entropy Sum
//        List<Vector3> finalSelectedVertices = SelectFinalSubsetByEntropySum(refinedVertices, entropyMap);

//    }

//    private void HighlightVisibleVertices()
//    {
//        visibleVertices.Clear();
//        uniquePositions.Clear();

//        MeshRenderer meshRenderer = GetComponentInChildren<MeshRenderer>();
//        Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(mainCamera);
//        Bounds bounds = meshRenderer.bounds;
//        if (!GeometryUtility.TestPlanesAABB(frustumPlanes, bounds)) return;

//        Vector3 camPos = mainCamera.transform.position;

//        foreach (var kvp in vertexPositions)
//        {
//            int vertexIndex = kvp.Key;
//            Vector3 vertexWorldPos = meshFilter.transform.TransformPoint(kvp.Value);
//            Vector3 vertexNormal = vertexNormals[vertexIndex];
//            Vector3 roundedVertex = new Vector3(
//                Mathf.Round(vertexWorldPos.x * 1000f) / 1000f,
//                Mathf.Round(vertexWorldPos.y * 1000f) / 1000f,
//                Mathf.Round(vertexWorldPos.z * 1000f) / 1000f
//            );
//            Vector3 worldNormal = meshFilter.transform.TransformDirection(vertexNormals[vertexIndex]);
//            Vector3 toCamera = (camPos - vertexWorldPos).normalized;

//            if (Vector3.Dot(worldNormal, toCamera) <= 0) continue;

//            Ray ray = new Ray(camPos, (vertexWorldPos - camPos).normalized);
//            if (Physics.Raycast(ray, out RaycastHit hit))
//            {
//                if (Vector3.Distance(hit.point, vertexWorldPos) < 0.01f)
//                {
//                    visibleVertices.Add(vertexWorldPos); // 보이는 정점을 리스트에 추가
//                    uniquePositions[roundedVertex] = true;
//                    //HighlightVertex(vertexWorldPos, visibilityColor, false);
//                }
//            }

//            Debug.Log($"Total Unique Visible Vertices: {visibleVertices.Count}");
//        }
//    }


//    private Dictionary<Vector3, float> ComputeEntropyForVisibleVertices(List<Vector3> visibleVertices)
//    {
//        Dictionary<Vector3, float> entropyMap = new Dictionary<Vector3, float>();
//        float[] entropyValues = ComputeVertexEntropy(visibleVertices);

//        Debug.Log("===== Entropy Values for Visible Vertices =====");

//        for (int i = 0; i < visibleVertices.Count; i++)
//        {
//            entropyMap[visibleVertices[i]] = entropyValues[i];
//            Debug.Log($"[BLUE] Vertex: {visibleVertices[i]}, Entropy: {entropyValues[i]}");
//        }

//        return entropyMap;
//    }
//    private List<Vector3> SelectTopEntropyVertices(Dictionary<Vector3, float> entropyMap)
//    {
//        int numTopEntropy = Mathf.CeilToInt(entropyMap.Count * topEntropyPercentage);
//        List<Vector3> topEntropyVertices = entropyMap.OrderByDescending(item => item.Value)
//                                               .Take(numTopEntropy)
//                                               .Select(item => item.Key)
//                                               .ToList();

//        Debug.Log("===== Top 10% High-Entropy Vertices (Green) =====");
//        foreach (Vector3 vertex in topEntropyVertices)
//        {
//            Debug.Log($"[GREEN] High Entropy Vertex: {vertex}, Entropy: {entropyMap[vertex]}");
//            HighlightVertex(vertex, Color.green, false);
//        }

//        return topEntropyVertices;
//    }
//    private List<Vector3> SelectMaxMinDistanceVertices(List<Vector3> topEntropyVertices, Dictionary<Vector3, float> entropyMap, int selectionCount)
//    {
//        List<Vector3> distributedVertices = new List<Vector3>();
//        HashSet<Vector3Int> usedGridCells = new HashSet<Vector3Int>();

//        distributedVertices.Add(topEntropyVertices[0]); // Start with the highest entropy vertex
//        usedGridCells.Add(QuantizePosition(topEntropyVertices[0]));
//        topEntropyVertices.RemoveAt(0);

//        while (distributedVertices.Count < selectionCount && topEntropyVertices.Count > 0)
//        {
//            float maxMinDist = float.MinValue;
//            Vector3 selectedPos = Vector3.zero;

//            foreach (var candidatePos in topEntropyVertices)
//            {
//                Vector3Int candidateGridCell = QuantizePosition(candidatePos);
//                if (usedGridCells.Contains(candidateGridCell))
//                    continue; // Skip this vertex if its grid cell is already occupied

//                float minDist = float.MaxValue;


//                foreach (Vector3 selPos in distributedVertices)
//                {
//                    float dist = Vector3.Distance(candidatePos, selPos);

//                    if (dist < minDist)
//                        minDist = dist;
//                }


//                float spreadPenalty = Mathf.Exp(-minDist * 3.0f);
//                float weightedScore = entropyMap[candidatePos] * (1 - spreadPenalty);

//                if (weightedScore > maxMinDist)
//                {
//                    maxMinDist = weightedScore;
//                    selectedPos = candidatePos;
//                }
//            }

//            if (selectedPos != Vector3.zero)
//            {
//                distributedVertices.Add(selectedPos);
//                usedGridCells.Add(QuantizePosition(selectedPos));
//                topEntropyVertices.Remove(selectedPos);
//            }
//            else
//            {
//                break;
//            }
//        }

//        return distributedVertices;
//    }
//    private Vector3Int QuantizePosition(Vector3 position, float gridSize = 0.5f)
//    {
//        return new Vector3Int(
//            Mathf.RoundToInt(position.x / gridSize),
//            Mathf.RoundToInt(position.y / gridSize),
//            Mathf.RoundToInt(position.z / gridSize)
//        );
//    }
//    private List<Vector3> RefineByEntropy(List<Vector3> distributedVertices, Dictionary<Vector3, float> entropyMap)
//    {
//        int numRefined = Mathf.CeilToInt(distributedVertices.Count * topEntropyPercentage); // Take top 50% again

//        List<Vector3> refinedVertices = distributedVertices.OrderByDescending(v => entropyMap[v])
//                                                           .Take(numRefined)
//                                                           .ToList();

//        Debug.Log("===== Refined Top 50% Distributed High-Entropy Vertices (YELLOW) =====");
//        foreach (Vector3 vertex in refinedVertices)
//        {
//            Debug.Log($"[YELLOW] Refined High Entropy Vertex: {vertex}, Entropy: {entropyMap[vertex]}");
//            HighlightVertex(vertex, Color.yellow, false);
//        }

//        return refinedVertices;
//    }
//    private List<Vector3> SelectFinalSubsetByEntropySum(List<Vector3> refinedVertices, Dictionary<Vector3, float> entropyMap)
//    {
//        List<Vector3> finalSelection = refinedVertices
//            .OrderByDescending(v => entropyMap[v])
//            .Take(bestDistributedVertices)
//            .ToList();

//        Debug.Log("===== Final Selected Vertices (RED) =====");
//        foreach (Vector3 vertex in finalSelection)
//        {
//            Debug.Log($"[RED] Final Selected Vertex: {vertex}, Entropy: {entropyMap[vertex]}");
//            HighlightVertex(vertex, highlightColor, true);
//        }

//        return finalSelection;
//    }


//    //entropy 계산 작업 수행
//    private float[] ComputeVertexEntropy(List<Vector3> visibleVertices)
//    {
//        float[] entropyValues = new float[visibleVertices.Count];
//        Vector3[] vertexArray = visibleVertices.ToArray();
//        float L = ComputeMeshCharacteristicLength(vertexPositions.Values.ToArray());

//        float sigma = 0.05f * L; // 논문에서는 5%l 사용 (기본적으로 5% 바운딩 박스 대각선)

//        for (int i = 0; i < visibleVertices.Count; i++)
//        {
//            // 현재 정점의 이웃 정점 찾기
//            List<Vector3> neighbors = GetNeighborsByEuclideanDistance(visibleVertices[i], sigma, vertexArray);
//            if (neighbors.Count == 0) continue;

//            // 히스토그램 생성 (법선 벡터의 분포 계산)
//            Dictionary<Vector3Int, int> normalHistogram = new Dictionary<Vector3Int, int>();
//            foreach (Vector3 neighbor in neighbors)
//            {
//                Vector3 normal = GetVertexNormal(neighbor);
//                Vector3Int quantizedNormal = QuantizeNormal(normal);

//                if (!normalHistogram.ContainsKey(quantizedNormal))
//                {
//                    normalHistogram[quantizedNormal] = 0;
//                }
//                normalHistogram[quantizedNormal]++;
//            }

//            // 샤논 엔트로피 계산
//            float entropy = 0f;
//            int totalNormals = neighbors.Count;
//            foreach (var count in normalHistogram.Values)
//            {
//                float probability = (float)count / totalNormals;
//                entropy -= probability * Mathf.Log(probability + 1e-6f); // log(0) 방지
//            }

//            entropyValues[i] = entropy; // 단일 sigma 엔트로피 값 저장
//        }

//        return entropyValues;
//    }
//    private Vector3Int QuantizeNormal(Vector3 normal, int scale = 5000)
//    {
//        return new Vector3Int(
//            Mathf.RoundToInt(normal.x * scale),
//            Mathf.RoundToInt(normal.y * scale),
//            Mathf.RoundToInt(normal.z * scale)
//        );
//    }
//    private Vector3 GetVertexNormal(Vector3 vertexPosition)
//    {
//        foreach (var kvp in vertexPositions)
//        {
//            if (Vector3.Distance(meshFilter.transform.TransformPoint(kvp.Value), vertexPosition) < 0.001f)
//            {
//                return vertexNormals[kvp.Key]; // Return the stored normal
//            }
//        }
//        return Vector3.zero; // Return zero vector if no normal is found
//    }
//    private float ComputeMeshCharacteristicLength(Vector3[] vertices)
//    {
//        float totalDistance = 0f;
//        int totalEdges = 0;

//        for (int i = 0; i < vertices.Length; i++)
//        {
//            for (int j = i + 1; j < vertices.Length; j++)
//            {
//                totalDistance += Vector3.Distance(vertices[i], vertices[j]);
//                totalEdges++;
//            }
//        }

//        return totalEdges > 0 ? totalDistance / totalEdges : 1f;
//    }

//    private List<Vector3> GetNeighborsByEuclideanDistance(Vector3 position, float sigma, Vector3[] vertices)
//    {
//        List<Vector3> neighbors = new List<Vector3>();

//        foreach (Vector3 vertex in vertices)
//        {
//            if (Vector3.Distance(position, vertex) <= sigma)
//            {
//                neighbors.Add(vertex);
//            }
//        }

//        return neighbors;
//    }

//    private void HighlightVertex(Vector3 position, Color color, bool isEntropyHighlight, int index = -1)
//    {
//        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
//        sphere.transform.position = position;
//        sphere.transform.localScale = Vector3.one * highlightSize;
//        if (isEntropyHighlight)
//        {
//            sphere.name = $"HighVertex_{index}";
//        }

//        Renderer sphereRenderer = sphere.GetComponent<Renderer>();
//        if (sphereRenderer != null)
//        {
//            // Use a separate material instance only if needed
//            if (isEntropyHighlight)
//            {
//                //entropy highlight

//                sphereRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
//                sphere.transform.localScale = Vector3.one * 7;
//            }
//            else
//            {
//                sphereRenderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
//                sphereRenderer.sharedMaterial.color = color;
//            }
//        }
//    }
//}

