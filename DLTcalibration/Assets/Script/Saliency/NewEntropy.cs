using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class NewEntropy : MonoBehaviour
{
    public MeshFilter meshFilter;
    public int neighborCount = 10;
    public int topVerticesToHighlight = 6;
    public Color highlightColor = Color.red;
    public float highlightSize = 0.05f;

    // 정점 정보 저장 (위치와 노멀)
    private Dictionary<int, Vector3> vertexPositions = new Dictionary<int, Vector3>();
    private Dictionary<int, Vector3> vertexNormals = new Dictionary<int, Vector3>();
    private Dictionary<int, List<int>> vertexNeighbors = new Dictionary<int, List<int>>();

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

        // Step 1: 정점의 위치와 노멀 저장
        for (int i = 0; i < positions.Length; i++)
        {
            vertexPositions[i] = positions[i];
            vertexNormals[i] = normals[i];
        }

        // Step 2: 각 정점의 이웃 정점을 탐색하여 저장
        for (int i = 0; i < positions.Length; i++)
        {
            List<int> neighbors = GetNeighbors(positions, i, neighborCount);
            vertexNeighbors[i] = neighbors;
        }

        // Step 3: 각 정점에 대한 엔트로피 계산 및 상위 엔트로피 정점 강조 표시
        float[] entropyValues = CalculateEntropyForAllVertices();
        HighlightTopVertices(entropyValues);
    }

    // 이웃 정점을 찾는 함수
    private List<int> GetNeighbors(Vector3[] vertices, int index, int count)
    {
        return vertices
            .Select((v, idx) => new { idx, distance = Vector3.Distance(v, vertices[index]) })
            .OrderBy(x => x.distance)
            .Skip(1)  // 자기 자신은 제외
            .Take(count)
            .Select(x => x.idx)
            .ToList();
    }

    // 모든 정점에 대해 엔트로피 계산
    private float[] CalculateEntropyForAllVertices()
    {
        float[] entropyValues = new float[vertexPositions.Count];

        foreach (var entry in vertexPositions)
        {
            int vertexIndex = entry.Key;
            List<int> neighbors = vertexNeighbors[vertexIndex];

            // Step 1: 이웃 정점들의 노멀을 기반으로 히스토그램 생성
            Dictionary<Vector3Int, int> histogram = new Dictionary<Vector3Int, int>();
            foreach (int neighborIndex in neighbors)
            {
                Vector3 normal = vertexNormals[neighborIndex];
                Vector3Int quantizedNormal = QuantizeNormal(normal);

                if (!histogram.ContainsKey(quantizedNormal))
                {
                    histogram[quantizedNormal] = 0;
                }
                histogram[quantizedNormal]++;
            }

            // Step 2: 엔트로피 계산
            float entropy = 0f;
            int total = neighbors.Count;

            foreach (var count in histogram.Values)
            {
                float probability = (float)count / total;
                entropy -= probability * Mathf.Log(probability);
            }

            entropyValues[vertexIndex] = entropy;
            Debug.Log($"Vertex {vertexIndex} - Entropy: {entropy}");
        }

        return entropyValues;
    }

    // 노멀 값을 양자화(quantize)하여 정수 벡터로 변환
    private Vector3Int QuantizeNormal(Vector3 normal, int scale = 100)
    {
        return new Vector3Int(
            Mathf.RoundToInt(normal.x * scale),
            Mathf.RoundToInt(normal.y * scale),
            Mathf.RoundToInt(normal.z * scale)
        );
    }

    // 엔트로피가 높은 정점을 강조 표시하는 함수
    private void HighlightTopVertices(float[] entropyValues)
    {
        // Step 1: Sort vertices by entropy (descending order), breaking ties randomly
        System.Random random = new System.Random();
        var topVertices = entropyValues
            .Select((value, index) => new { index, value, tieBreaker = random.NextDouble() })
            .OrderByDescending(item => item.value)
            .ThenBy(item => item.tieBreaker)  // Use random tie-breaker
            .Take(topVerticesToHighlight)
            .ToList();

        // Step 2: Position offset for visual sorting
        float offsetStep = 0.2f;
        Vector3 visualOffset = Vector3.right * offsetStep;

        // Step 3: Highlight each top vertex and output detailed information
        for (int i = 0; i < topVertices.Count; i++)
        {
            int vertexIndex = topVertices[i].index;

            if (vertexPositions.TryGetValue(vertexIndex, out Vector3 position) &&
                vertexNormals.TryGetValue(vertexIndex, out Vector3 normal))
            {
                // Apply an offset for visual sorting (e.g., along the X-axis)
                Vector3 offsetPosition = position + (visualOffset * i);

                // Create a sphere to highlight the vertex
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.position = meshFilter.transform.TransformPoint(offsetPosition);
                sphere.transform.localScale = Vector3.one * highlightSize;
                sphere.GetComponent<Renderer>().material.color = highlightColor;

                // Output detailed information
                Debug.Log($"Highlighted Vertex {vertexIndex}");
                Debug.Log($"  Entropy: {topVertices[i].value}");
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