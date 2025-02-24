using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class EnhancedEntropySaliency : MonoBehaviour
{
    public MeshFilter meshFilter;
    public int neighborCount = 10; // For fixed neighbor mode (optional)
    public float[] sigmaScales = new float[] { 0.1f, 0.2f, 0.3f }; // Multi-scale sigma values
    public int topVerticesToHighlight = 6; // Final number of vertices to highlight
    public Color highlightColor = Color.red;
    public float highlightSize = 0.05f;

    // Vertex data storage
    private Dictionary<int, Vector3> vertexPositions = new Dictionary<int, Vector3>();
    private Dictionary<int, Vector3> vertexNormals = new Dictionary<int, Vector3>();

    void Start()
    {
        if (meshFilter == null)
        {
            Debug.LogError("MeshFilter가 할당되지 않았습니다.");
            return;
        }

        Mesh mesh = meshFilter.mesh;
        Vector3[] positions = mesh.vertices;
        Vector3[] normals = mesh.normals;

        // Step 1: Store vertex positions and normals
        for (int i = 0; i < positions.Length; i++)
        {
            vertexPositions[i] = positions[i];
            vertexNormals[i] = normals[i];
        }

        // Step 2: Calculate multi-scale entropy for all vertices
        float[] entropyValues = CalculateMultiScaleEntropy(positions, normals, sigmaScales);

        // Step 3: Highlight top vertices based on entropy
        HighlightTopVerticesWithMaxMin(entropyValues);
    }

    // Calculate multi-scale entropy
    private float[] CalculateMultiScaleEntropy(Vector3[] vertices, Vector3[] normals, float[] sigmaScales)
    {
        float[] entropyValues = new float[vertices.Length];

        for (int i = 0; i < vertices.Length; i++)
        {
            float aggregatedEntropy = 0f;

            foreach (float sigma in sigmaScales)
            {
                List<int> neighbors = GetNeighborsByDistance(vertices, i, sigma);

                // Step 1: Build histogram of neighbor normals
                Dictionary<Vector3Int, int> histogram = new Dictionary<Vector3Int, int>();
                foreach (int neighborIndex in neighbors)
                {
                    Vector3 normal = normals[neighborIndex];
                    Vector3Int quantizedNormal = QuantizeNormal(normal);

                    if (!histogram.ContainsKey(quantizedNormal))
                    {
                        histogram[quantizedNormal] = 0;
                    }
                    histogram[quantizedNormal]++;
                }

                // Step 2: Calculate entropy for this scale
                float entropy = 0f;
                int total = neighbors.Count;

                foreach (var count in histogram.Values)
                {
                    float probability = (float)count / total;
                    entropy -= probability * Mathf.Log(probability);
                }

                aggregatedEntropy += entropy; // Aggregate entropy across scales
            }

            // Average entropy over all scales
            entropyValues[i] = aggregatedEntropy / sigmaScales.Length;
            Debug.Log($"Vertex {i} - Multi-Scale Entropy: {entropyValues[i]}");
        }

        return entropyValues;
    }

    // Find neighbors within sigma distance
    private List<int> GetNeighborsByDistance(Vector3[] vertices, int index, float sigma)
    {
        Vector3 centerVertex = vertices[index];

        return vertices
            .Select((v, idx) => new { idx, distance = Vector3.Distance(v, centerVertex) })
            .Where(x => x.distance > 0 && x.distance <= sigma) // Exclude self and apply sigma threshold
            .Select(x => x.idx)
            .ToList();
    }

    // Quantize normals for histogram
    private Vector3Int QuantizeNormal(Vector3 normal, int scale = 100)
    {
        return new Vector3Int(
            Mathf.RoundToInt(normal.x * scale),
            Mathf.RoundToInt(normal.y * scale),
            Mathf.RoundToInt(normal.z * scale)
        );
    }

    // Highlight top vertices using Max-Min Distance strategy
    private void HighlightTopVerticesWithMaxMin(float[] entropyValues)
    {
        // Step 1: Select top entropy candidates
        var topCandidatesList = entropyValues
            .Select((value, index) => new { index, value })
            .OrderByDescending(item => item.value)
            .Take(topVerticesToHighlight * 3) // Take more candidates for Max-Min
            .ToList();

        // Step 2: Max-Min Distance selection
        List<int> selectedIndices = new List<int>();

        // Start with the vertex with the highest entropy
        selectedIndices.Add(topCandidatesList[0].index);
        topCandidatesList.RemoveAt(0);

        while (selectedIndices.Count < topVerticesToHighlight && topCandidatesList.Count > 0)
        {
            float maxMinDist = float.MinValue;
            int selectedIdx = -1;

            foreach (var candidate in topCandidatesList)
            {
                float minDist = float.MaxValue;

                foreach (int selIdx in selectedIndices)
                {
                    float dist = Vector3.Distance(vertexPositions[candidate.index], vertexPositions[selIdx]);
                    if (dist < minDist)
                        minDist = dist;
                }

                if (minDist > maxMinDist)
                {
                    maxMinDist = minDist;
                    selectedIdx = candidate.index;
                }
            }

            if (selectedIdx != -1)
            {
                selectedIndices.Add(selectedIdx);
                topCandidatesList.RemoveAll(c => c.index == selectedIdx);
            }
            else
            {
                break; // No more suitable candidates
            }
        }

        // Step 3: Visualize selected vertices
        foreach (int vertexIndex in selectedIndices)
        {
            if (vertexPositions.TryGetValue(vertexIndex, out Vector3 position) &&
                vertexNormals.TryGetValue(vertexIndex, out Vector3 normal))
            {
                // Create a sphere to highlight the vertex
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.position = meshFilter.transform.TransformPoint(position);
                sphere.transform.localScale = Vector3.one * highlightSize;
                sphere.GetComponent<Renderer>().material.color = highlightColor;

                // Output detailed information
                Debug.Log($"Highlighted Vertex {vertexIndex}");
                Debug.Log($"  Entropy: {entropyValues[vertexIndex]}");
                Debug.Log($"  Position: {position}");
                Debug.Log($"  Normal: {normal}");
            }
            else
            {
                Debug.LogWarning($"Vertex {vertexIndex} data not found in vertexPositions or vertexNormals");
            }
        }
    }
}


////거리기반(max-min distance) 카메라 무관
//using UnityEngine;
//using System.Collections.Generic;
//using System.Linq;

//public class NewEntropy : MonoBehaviour
//{
//    public MeshFilter meshFilter;
//    public int neighborCount = 10;
//    public int topCandidates = 20; // Number of top entropy candidates to consider
//    public int topVerticesToHighlight = 6; // Final number of vertices to highlight
//    public Color highlightColor = Color.red;
//    public float highlightSize = 0.05f;

//    // Vertex data storage
//    private Dictionary<int, Vector3> vertexPositions = new Dictionary<int, Vector3>();
//    private Dictionary<int, Vector3> vertexNormals = new Dictionary<int, Vector3>();
//    private Dictionary<int, List<int>> vertexNeighbors = new Dictionary<int, List<int>>();

//    void Start()
//    {
//        if (meshFilter == null)
//        {
//            Debug.LogError("MeshFilter가 할당되지 않았습니다.");
//            return;
//        }

//        Mesh mesh = meshFilter.mesh;
//        Vector3[] positions = mesh.vertices;
//        Vector3[] normals = mesh.normals;

//        // Step 1: Store vertex positions and normals
//        for (int i = 0; i < positions.Length; i++)
//        {
//            vertexPositions[i] = positions[i];
//            vertexNormals[i] = normals[i];
//        }

//        // Step 2: Find neighbors for each vertex
//        for (int i = 0; i < positions.Length; i++)
//        {
//            List<int> neighbors = GetNeighbors(positions, i, neighborCount);
//            vertexNeighbors[i] = neighbors;
//        }

//        // Step 3: Calculate entropy for all vertices and highlight top ones
//        float[] entropyValues = CalculateEntropyForAllVertices();
//        HighlightTopVerticesWithMaxMin(entropyValues);
//    }

//    // Find nearest neighbors
//    private List<int> GetNeighbors(Vector3[] vertices, int index, int count)
//    {
//        return vertices
//            .Select((v, idx) => new { idx, distance = Vector3.Distance(v, vertices[index]) })
//            .OrderBy(x => x.distance)
//            .Skip(1) // Exclude the vertex itself
//            .Take(count)
//            .Select(x => x.idx)
//            .ToList();
//    }

//    // Calculate entropy for all vertices
//    private float[] CalculateEntropyForAllVertices()
//    {
//        float[] entropyValues = new float[vertexPositions.Count];

//        foreach (var entry in vertexPositions)
//        {
//            int vertexIndex = entry.Key;
//            List<int> neighbors = vertexNeighbors[vertexIndex];

//            // Step 1: Build histogram of neighbor normals / 양자화 진행(QuantizeNormal), 수식 2 / 확인 필
//            Dictionary<Vector3Int, int> histogram = new Dictionary<Vector3Int, int>();
//            foreach (int neighborIndex in neighbors)
//            {
//                Vector3 normal = vertexNormals[neighborIndex];
//                Vector3Int quantizedNormal = QuantizeNormal(normal);

//                if (!histogram.ContainsKey(quantizedNormal))
//                {
//                    histogram[quantizedNormal] = 0;
//                }
//                histogram[quantizedNormal]++;
//            }

//            // Step 2: Calculate entropy / 샤논 엔트로피 사용 / 논문 수식과 동일
//            float entropy = 0f;
//            int total = neighbors.Count;

//            foreach (var count in histogram.Values)
//            {
//                float probability = (float)count / total;
//                entropy -= probability * Mathf.Log(probability);
//            }

//            entropyValues[vertexIndex] = entropy;
//            Debug.Log($"Vertex {vertexIndex} - Entropy: {entropy}");
//        }

//        return entropyValues;
//    }

//    // Quantize normal vectors for histogram, 
//    private Vector3Int QuantizeNormal(Vector3 normal, int scale = 100)
//    {
//        return new Vector3Int(
//            Mathf.RoundToInt(normal.x * scale),
//            Mathf.RoundToInt(normal.y * scale),
//            Mathf.RoundToInt(normal.z * scale)
//        );
//    }

//    // Highlight top vertices using Max-Min Distance strategy
//    private void HighlightTopVerticesWithMaxMin(float[] entropyValues)
//    {
//        // Step 1: Select top entropy candidates
//        var topCandidatesList = entropyValues
//            .Select((value, index) => new { index, value })
//            .OrderByDescending(item => item.value)
//            .Take(topCandidates)
//            .ToList();

//        // Step 2: Max-Min Distance selection
//        List<int> selectedIndices = new List<int>();

//        // Start with the vertex with the highest entropy
//        selectedIndices.Add(topCandidatesList[0].index);
//        topCandidatesList.RemoveAt(0);

//        while (selectedIndices.Count < topVerticesToHighlight && topCandidatesList.Count > 0)
//        {
//            float maxMinDist = float.MinValue;
//            int selectedIdx = -1;

//            foreach (var candidate in topCandidatesList)
//            {
//                float minDist = float.MaxValue;

//                foreach (int selIdx in selectedIndices)
//                {
//                    float dist = Vector3.Distance(vertexPositions[candidate.index], vertexPositions[selIdx]);
//                    if (dist < minDist)
//                        minDist = dist;
//                }

//                if (minDist > maxMinDist)
//                {
//                    maxMinDist = minDist;
//                    selectedIdx = candidate.index;
//                }
//            }

//            if (selectedIdx != -1)
//            {
//                selectedIndices.Add(selectedIdx);
//                topCandidatesList.RemoveAll(c => c.index == selectedIdx);
//            }
//            else
//            {
//                break; // No more suitable candidates
//            }
//        }

//        // Step 3: Visualize selected vertices
//        foreach (int vertexIndex in selectedIndices)
//        {
//            if (vertexPositions.TryGetValue(vertexIndex, out Vector3 position) &&
//                vertexNormals.TryGetValue(vertexIndex, out Vector3 normal))
//            {
//                // Create a sphere to highlight the vertex
//                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
//                sphere.transform.position = meshFilter.transform.TransformPoint(position);
//                sphere.transform.localScale = Vector3.one * highlightSize;
//                sphere.GetComponent<Renderer>().material.color = highlightColor;

//                // Output detailed information
//                Debug.Log($"Highlighted Vertex {vertexIndex}");
//                Debug.Log($"  Entropy: {entropyValues[vertexIndex]}");
//                Debug.Log($"  Position: {position}");
//                Debug.Log($"  Normal: {normal}");
//            }
//            else
//            {
//                Debug.LogWarning($"Vertex {vertexIndex} data not found in vertexPositions or vertexNormals");
//            }
//        }
//    }
//}

//using UnityEngine;
//using System.Collections.Generic;
//using System.Linq;

//public class NewEntropy : MonoBehaviour
//{
//    public MeshFilter meshFilter;
//    public int neighborCount = 10;
//    public int topVerticesToHighlight = 6;
//    public Color highlightColor = Color.red;
//    public float highlightSize = 0.05f;

//    // 정점 정보 저장 (위치와 노멀)
//    private Dictionary<int, Vector3> vertexPositions = new Dictionary<int, Vector3>();
//    private Dictionary<int, Vector3> vertexNormals = new Dictionary<int, Vector3>();
//    private Dictionary<int, List<int>> vertexNeighbors = new Dictionary<int, List<int>>();

//    void Start()
//    {
//        if (meshFilter == null)
//        {
//            Debug.LogError("MeshFilter가 할당되지 않았습니다.");
//            return;
//        }

//        Mesh mesh = meshFilter.mesh;
//        Vector3[] positions = mesh.vertices;
//        Vector3[] normals = mesh.normals;

//        // Step 1: 정점의 위치와 노멀 저장
//        for (int i = 0; i < positions.Length; i++)
//        {
//            vertexPositions[i] = positions[i];
//            vertexNormals[i] = normals[i];
//        }

//        // Step 2: 각 정점의 이웃 정점을 탐색하여 저장
//        for (int i = 0; i < positions.Length; i++)
//        {
//            List<int> neighbors = GetNeighbors(positions, i, neighborCount);
//            vertexNeighbors[i] = neighbors;
//        }

//        // Step 3: 각 정점에 대한 엔트로피 계산 및 상위 엔트로피 정점 강조 표시
//        float[] entropyValues = CalculateEntropyForAllVertices();
//        HighlightTopVertices(entropyValues);
//    }

//    // 이웃 정점을 찾는 함수
//    private List<int> GetNeighbors(Vector3[] vertices, int index, int count)
//    {
//        return vertices
//            .Select((v, idx) => new { idx, distance = Vector3.Distance(v, vertices[index]) })
//            .OrderBy(x => x.distance)
//            .Skip(1)  // 자기 자신은 제외
//            .Take(count)
//            .Select(x => x.idx)
//            .ToList();
//    }

//    // 모든 정점에 대해 엔트로피 계산
//    private float[] CalculateEntropyForAllVertices()
//    {
//        float[] entropyValues = new float[vertexPositions.Count];

//        foreach (var entry in vertexPositions)
//        {
//            int vertexIndex = entry.Key;
//            List<int> neighbors = vertexNeighbors[vertexIndex];

//            // Step 1: 이웃 정점들의 노멀을 기반으로 히스토그램 생성
//            Dictionary<Vector3Int, int> histogram = new Dictionary<Vector3Int, int>();
//            foreach (int neighborIndex in neighbors)
//            {
//                Vector3 normal = vertexNormals[neighborIndex];
//                Vector3Int quantizedNormal = QuantizeNormal(normal);

//                if (!histogram.ContainsKey(quantizedNormal))
//                {
//                    histogram[quantizedNormal] = 0;
//                }
//                histogram[quantizedNormal]++;
//            }

//            // Step 2: 엔트로피 계산
//            float entropy = 0f;
//            int total = neighbors.Count;

//            foreach (var count in histogram.Values)
//            {
//                float probability = (float)count / total;
//                entropy -= probability * Mathf.Log(probability);
//            }

//            entropyValues[vertexIndex] = entropy;
//            Debug.Log($"Vertex {vertexIndex} - Entropy: {entropy}");
//        }

//        return entropyValues;
//    }

//    // 노멀 값을 양자화(quantize)하여 정수 벡터로 변환
//    private Vector3Int QuantizeNormal(Vector3 normal, int scale = 100)
//    {
//        return new Vector3Int(
//            Mathf.RoundToInt(normal.x * scale),
//            Mathf.RoundToInt(normal.y * scale),
//            Mathf.RoundToInt(normal.z * scale)
//        );
//    }

//    // 엔트로피가 높은 정점을 강조 표시하는 함수
//    private void HighlightTopVertices(float[] entropyValues)
//    {
//        // Step 1: Sort vertices by entropy (descending order), breaking ties randomly
//        System.Random random = new System.Random();
//        var topVertices = entropyValues
//            .Select((value, index) => new { index, value, tieBreaker = random.NextDouble() })
//            .OrderByDescending(item => item.value)
//            .ThenBy(item => item.tieBreaker)  // Use random tie-breaker
//            .Take(topVerticesToHighlight)
//            .ToList();

//        // Step 2: Position offset for visual sorting
//        float offsetStep = 0.2f;
//        Vector3 visualOffset = Vector3.right * offsetStep;

//        // Step 3: Highlight each top vertex and output detailed information
//        for (int i = 0; i < topVertices.Count; i++)
//        {
//            int vertexIndex = topVertices[i].index;

//            if (vertexPositions.TryGetValue(vertexIndex, out Vector3 position) &&
//                vertexNormals.TryGetValue(vertexIndex, out Vector3 normal))
//            {
//                // Apply an offset for visual sorting (e.g., along the X-axis)
//                Vector3 offsetPosition = position + (visualOffset * i);

//                // Create a sphere to highlight the vertex
//                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
//                sphere.transform.position = meshFilter.transform.TransformPoint(offsetPosition);
//                sphere.transform.localScale = Vector3.one * highlightSize;
//                sphere.GetComponent<Renderer>().material.color = highlightColor;

//                // Output detailed information
//                Debug.Log($"Highlighted Vertex {vertexIndex}");
//                Debug.Log($"  Entropy: {topVertices[i].value}");
//                Debug.Log($"  Position: {position}");
//                Debug.Log($"  Normal: {normal}");
//            }
//            else
//            {
//                Debug.LogWarning($"Vertex {vertexIndex} data not found in vertexPositions or vertexNormals");
//            }
//        }
//    }
//}