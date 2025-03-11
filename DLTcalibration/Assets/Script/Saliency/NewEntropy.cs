using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

public class NewEntropy : MonoBehaviour
{
    public MeshFilter meshFilter;
    public Camera mainCamera;
    public float[] sigmaScales = new float[] { 0.1f, 0.2f, 0.3f };
    public float topEntropyPercentage = 0.1f;
    public int bestDistributedVertices = 6;
    public Color highlightColor = Color.red;
    public float highlightSize = 0.05f;
    public LayerMask visibilityLayerMask;
    public Color visibilityColor = Color.blue;

    private Dictionary<int, Vector3> vertexPositions = new Dictionary<int, Vector3>();
    private Dictionary<int, Vector3> vertexNormals = new Dictionary<int, Vector3>();

    void Start()
    {
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

        // Step 1: Store vertex positions and normals
        for (int i = 0; i < positions.Length; i++)
        {
            vertexPositions[i] = positions[i];
            vertexNormals[i] = normals[i];
        }

        // Step 2: Filter only vertices visible in the camera
        List<int> visibleVertices = FilterVisibleVertices();
        if (visibleVertices.Count == 0)
        {
            Debug.LogError("No visible vertices found!");
            return;
        }

        foreach (int vertexIndex in visibleVertices)
        {
            Vector3 worldPosition = meshFilter.transform.TransformPoint(vertexPositions[vertexIndex]); // Convert to world space
            HighlightVertex(worldPosition, visibilityColor);
        }
        // Step 3: Compute entropy for only visible vertices
        Dictionary<int, float> entropyMap = ComputeEntropyForVisibleVertices(visibleVertices);

        // Step 4: Select the top 10% highest entropy vertices
        List<int> topEntropyVertices = SelectTopEntropyVertices(entropyMap);

        // Step 5: Select 6 widely distributed vertices using Max-Min Distance
        List<int> finalSelectedVertices = SelectMaxMinDistanceVertices(topEntropyVertices, entropyMap);

        // Step 6: Highlight selected vertices
        foreach (int vertexIndex in finalSelectedVertices)
        {
            Vector3 worldPosition = meshFilter.transform.TransformPoint(vertexPositions[vertexIndex]); // Convert to world space
            HighlightVertex(worldPosition, highlightColor);
        }
    }
    private List<int> FilterVisibleVertices()
    {
        List<int> visibleVertices = new List<int>();

        Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(mainCamera);
        Bounds bounds = meshFilter.GetComponent<MeshRenderer>().bounds;
        if (!GeometryUtility.TestPlanesAABB(frustumPlanes, bounds)) return visibleVertices; // Skip if completely outside

        foreach (var kvp in vertexPositions)
        {
            int vertexIndex = kvp.Key;
            Vector3 vertexWorldPos = meshFilter.transform.TransformPoint(kvp.Value);
            Vector3 vertexNormal = vertexNormals[vertexIndex];
            Vector3 camPos = mainCamera.transform.position;

            // Step 2: Ensure vertex normal is facing the camera
            Vector3 toCamera = (camPos - vertexWorldPos).normalized;
            if (Vector3.Dot(vertexNormal, toCamera) <= 0)
            {
                continue; // Ignore back-facing vertices
            }

            // Step 3: Perform occlusion check with raycasting (Same as `VisibleVerticesRaycast`)
            Ray ray = new Ray(camPos, (vertexWorldPos - camPos).normalized);
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, visibilityLayerMask))
            {
                if (Vector3.Distance(hit.point, vertexWorldPos) < 0.01f)
                {
                    visibleVertices.Add(vertexIndex);
                }
            }
        }

        Debug.Log($"Total Visible Vertices: {visibleVertices.Count}");
        return visibleVertices;
    }

    private Dictionary<int, float> ComputeEntropyForVisibleVertices(List<int> visibleVertices)
    {
        Dictionary<int, float> entropyMap = new Dictionary<int, float>();
        float[] entropyValues = ComputeVertexEntropy(visibleVertices);

        for (int i = 0; i < visibleVertices.Count; i++)
        {
            entropyMap[visibleVertices[i]] = entropyValues[i];
        }

        return entropyMap;
    }
    private List<int> SelectTopEntropyVertices(Dictionary<int, float> entropyMap)
    {
        int numTopEntropy = Mathf.CeilToInt(entropyMap.Count * topEntropyPercentage);
        return entropyMap.OrderByDescending(item => item.Value)
                         .Take(numTopEntropy)
                         .Select(item => item.Key)
                         .ToList();
    }
    private List<int> SelectMaxMinDistanceVertices(List<int> topEntropyVertices, Dictionary<int, float> entropyMap)
    {
        List<int> selectedVertices = new List<int>();
        selectedVertices.Add(topEntropyVertices[0]); // Start with highest entropy vertex
        topEntropyVertices.RemoveAt(0);

        while (selectedVertices.Count < bestDistributedVertices && topEntropyVertices.Count > 0)
        {
            float maxGeodesicSpread = float.MinValue;
            int selectedIdx = -1;

            foreach (var candidateIdx in topEntropyVertices)
            {
                float minDist = float.MaxValue;
                foreach (int selIdx in selectedVertices)
                {
                    float dist = Vector3.Distance(vertexPositions[candidateIdx], vertexPositions[selIdx]);
                    if (dist < minDist)
                        minDist = dist;
                }

                // Stronger **spread constraint**: Penalize close selections
                float geodesicPenalty = Mathf.Exp(-minDist * 0.5f);
                float weightedScore = entropyMap[candidateIdx] * (1 - geodesicPenalty);

                if (weightedScore > maxGeodesicSpread)
                {
                    maxGeodesicSpread = weightedScore;
                    selectedIdx = candidateIdx;
                }
            }

            if (selectedIdx != -1)
            {
                selectedVertices.Add(selectedIdx);
                topEntropyVertices.Remove(selectedIdx);
            }
            else
            {
                break;
            }
        }

        return selectedVertices;
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
    private List<int> GetNeighborsByEuclideanDistance(int index, float sigma, Vector3[] vertices)
    {
        List<int> neighbors = new List<int>();

        for (int i = 0; i < vertices.Length; i++)
        {
            if (i == index) continue;
            if (Vector3.Distance(vertices[index], vertices[i]) <= sigma)
            {
                neighbors.Add(i);
            } 
        }

        return neighbors;
    }

    private float[] ComputeVertexEntropy(List<int> visibleVertices)
    {
        float[] entropyValues = new float[visibleVertices.Count];
        float L = ComputeMeshCharacteristicLength(vertexPositions.Values.ToArray());

        for (int i = 0; i < visibleVertices.Count; i++)
        {
            float aggregatedEntropy = 0f;
            float totalWeight = 0f;

            foreach (float sigma in sigmaScales)
            {
                float scaledSigma = sigma * L;
                List<int> neighbors = GetNeighborsByEuclideanDistance(visibleVertices[i], scaledSigma, vertexPositions.Values.ToArray());

                if (neighbors.Count == 0) continue;

                float entropy = neighbors.Count * Mathf.Log(neighbors.Count);
                aggregatedEntropy += entropy;
                totalWeight += 1.0f / sigma;
            }

            entropyValues[i] = aggregatedEntropy / totalWeight;
        }

        return entropyValues;
    }
    private void HighlightVertex(Vector3 position)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.position = position;
        sphere.transform.localScale = Vector3.one * highlightSize;
        sphere.GetComponent<Renderer>().material.color = highlightColor;
    }
    private void HighlightVertex(Vector3 position, Color color)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.position = position;
        sphere.transform.localScale = Vector3.one * highlightSize;

        // Ensure unique material instances to avoid shared color issues
        Material highlightMaterial = new Material(Shader.Find("Standard"));
        highlightMaterial.color = color;
        sphere.GetComponent<Renderer>().material = highlightMaterial;
    }
}


//using UnityEngine;
//using System.Collections.Generic;
//using System.Linq;
//using System;

//public class NewEntropy : MonoBehaviour
//{
//    public MeshFilter meshFilter;
//    public Camera mainCamera; // Main camera reference
//    public float[] sigmaScales = new float[] { 0.1f, 0.2f, 0.3f };
//    public float topEntropyPercentage = 0.1f; // 10% of vertices
//    public int bestDistributedVertices = 6; // Number of best-spread vertices
//    public float humanFOVAngle = 60f; // Human Field of View (FOV) angle
//    public Color highlightColor = Color.red;
//    public float highlightSize = 0.05f;
//    public LayerMask visibilityLayerMask; // Defines which layers to check for occlusion

//    private Dictionary<int, Vector3> vertexPositions = new Dictionary<int, Vector3>();
//    private Dictionary<int, Vector3> vertexNormals = new Dictionary<int, Vector3>();

//    void Start()
//    {
//        if (meshFilter == null || mainCamera == null)
//        {
//            Debug.LogError("MeshFilter or Main Camera is not assigned.");
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

//        // Step 2: Compute entropy per vertex using its own normal
//        float[] entropyValues = ComputeVertexEntropy(positions, normals, sigmaScales);

//        // Step 3: Select the best distributed high-entropy vertices
//        SelectBestDistributedHumanVisibleVertices(entropyValues);
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

//    private float[] ComputeVertexEntropy(Vector3[] vertices, Vector3[] normals, float[] sigmaScales)
//    {
//        float[] entropyValues = new float[vertices.Length];
//        float L = ComputeMeshCharacteristicLength(vertices);

//        for (int i = 0; i < vertices.Length; i++)
//        {
//            float aggregatedEntropy = 0f;
//            float totalWeight = 0f;

//            foreach (float sigma in sigmaScales)
//            {
//                float scaledSigma = sigma * L;
//                List<int> neighbors = GetNeighborsByEuclideanDistance(i, scaledSigma, vertices);

//                if (neighbors.Count == 0) continue;

//                Dictionary<Vector3Int, int> histogram = new Dictionary<Vector3Int, int>();
//                foreach (int neighborIndex in neighbors)
//                {
//                    Vector3 normal = normals[neighborIndex];
//                    Vector3Int quantizedNormal = QuantizeNormal(normal, neighbors.Count);

//                    if (!histogram.ContainsKey(quantizedNormal))
//                    {
//                        histogram[quantizedNormal] = 0;
//                    }
//                    histogram[quantizedNormal]++;
//                }

//                float entropy = 0f;
//                int total = neighbors.Count;

//                foreach (var count in histogram.Values)
//                {
//                    float probability = (float)count / total;
//                    entropy -= probability * Mathf.Log(probability);
//                }

//                float weight = 1.0f / sigma;
//                aggregatedEntropy += weight * entropy;
//                totalWeight += weight;
//            }

//            entropyValues[i] = aggregatedEntropy / totalWeight;
//        }

//        return entropyValues;
//    }

//    private List<int> GetNeighborsByEuclideanDistance(int index, float sigma, Vector3[] vertices)
//    {
//        List<int> neighbors = new List<int>();

//        for (int i = 0; i < vertices.Length; i++)
//        {
//            if (i == index) continue;
//            if (Vector3.Distance(vertices[index], vertices[i]) <= sigma)
//            {
//                neighbors.Add(i);
//            }
//        }

//        return neighbors;
//    }

//    private Vector3Int QuantizeNormal(Vector3 normal, int binSize)
//    {
//        int scale = Mathf.Clamp(binSize, 10, 5000);
//        return new Vector3Int(
//            Mathf.RoundToInt(normal.x * scale),
//            Mathf.RoundToInt(normal.y * scale),
//            Mathf.RoundToInt(normal.z * scale)
//        );
//    }

//    private void SelectBestDistributedHumanVisibleVertices(float[] entropyValues)
//    {
//        int numTopEntropy = Mathf.CeilToInt(vertexPositions.Count * topEntropyPercentage);

//        // Step 1: Filter only vertices that are inside human vision FOV
//        List<int> humanVisibleVertices = new List<int>();
//        Dictionary<int, float> projectedAreaMap = new Dictionary<int, float>();

//        foreach (var kvp in vertexPositions)
//        {
//            int vertexIndex = kvp.Key;
//            Vector3 vertexWorldPos = kvp.Value;
//            Vector3 vertexNormal = vertexNormals[vertexIndex]; 

//            if (IsVertexInHumanVision(vertexWorldPos, vertexNormal)) 
//            {
//                humanVisibleVertices.Add(kvp.Key);
//                projectedAreaMap[kvp.Key] = ComputeProjectedArea(kvp.Value);
//            }
//        }

//        Debug.Log($"Total Human-Visible Vertices: {humanVisibleVertices.Count}");

//        // Step 2: Select top 10% highest entropy vertices within human-visible region
//        var topEntropyVisibleVertices = humanVisibleVertices
//            .Select(index => new { index, entropy = entropyValues[index], area = projectedAreaMap[index] })
//            .OrderByDescending(item => item.entropy) // Prioritize entropy first
//            .Take(numTopEntropy)
//            .Select(item => item.index)
//            .ToList();

//        Debug.Log($"Total Candidates in Top {topEntropyPercentage * 100}% (Human Visible Only): {topEntropyVisibleVertices.Count}");

//        // Step 3: Select widely distributed vertices using FPS, prioritizing entropy & projected area
//        List<int> selectedVertices = new List<int>();
//        selectedVertices.Add(topEntropyVisibleVertices[0]);
//        topEntropyVisibleVertices.RemoveAt(0);

//        while (selectedVertices.Count < bestDistributedVertices && topEntropyVisibleVertices.Count > 0)
//        {
//            float maxMinDist = float.MinValue;
//            int selectedIdx = -1;

//            foreach (var candidateIdx in topEntropyVisibleVertices)
//            {
//                float minDist = float.MaxValue;

//                foreach (int selIdx in selectedVertices)
//                {
//                    float dist = Vector3.Distance(vertexPositions[candidateIdx], vertexPositions[selIdx]);
//                    if (dist < minDist)
//                        minDist = dist;
//                }

//                float weightedScore = minDist * projectedAreaMap[candidateIdx];

//                if (weightedScore > maxMinDist)
//                {
//                    maxMinDist = weightedScore;
//                    selectedIdx = candidateIdx;
//                }
//            }

//            if (selectedIdx != -1)
//            {
//                selectedVertices.Add(selectedIdx);
//                topEntropyVisibleVertices.Remove(selectedIdx);
//            }
//            else
//            {
//                break;
//            }
//        }

//        Debug.Log("===== Best Distributed Human-Visible High-Entropy Vertices =====");
//        foreach (int vertexIndex in selectedVertices)
//        {
//            Debug.Log($"Vertex {vertexIndex} - Entropy: {entropyValues[vertexIndex]} - Projected Area: {projectedAreaMap[vertexIndex]}");

//            if (vertexPositions.TryGetValue(vertexIndex, out Vector3 position))
//            {
//                HighlightVertex(position);
//            }
//        }
//    }

//    private bool IsVertexInHumanVision(Vector3 vertexWorldPos, Vector3 vertexNormal)
//    {
//        Vector3 camToVertex = (vertexWorldPos - mainCamera.transform.position).normalized;
//        float angle = Vector3.Angle(mainCamera.transform.forward, camToVertex);

//        // Step 1: Ensure vertex is within the human's FOV
//        if (angle > humanFOVAngle / 2) return false;

//        // Step 2: Ensure vertex normal is facing the camera
//        float normalAngle = Vector3.Angle(mainCamera.transform.forward, vertexNormal);
//        if (normalAngle > 90f) return false; // Back-facing vertex, discard

//        // Step 3: Perform an accurate occlusion check using Raycasting
//        Ray ray = new Ray(mainCamera.transform.position, vertexWorldPos - mainCamera.transform.position);
//        if (Physics.Raycast(ray, out RaycastHit hit, Vector3.Distance(mainCamera.transform.position, vertexWorldPos), visibilityLayerMask))
//        {
//            // Ensure the vertex itself is directly visible (small margin for floating-point precision)
//            if (Vector3.Distance(hit.point, vertexWorldPos) > 0.0005f)
//            {
//                return false; // The vertex is blocked by another object
//            }
//        }

//        return true; // The vertex is fully visible
//    }

//    private float ComputeProjectedArea(Vector3 vertexWorldPos)
//    {
//        Vector3 screenPoint = mainCamera.WorldToScreenPoint(meshFilter.transform.TransformPoint(vertexWorldPos));
//        float screenArea = screenPoint.z / mainCamera.farClipPlane; // Normalize depth-based area scaling
//        return Mathf.Max(screenArea, 0.001f); // Avoid zero area
//    }

//    private void HighlightVertex(Vector3 position)
//    {
//        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
//        sphere.transform.position = meshFilter.transform.TransformPoint(position);
//        sphere.transform.localScale = Vector3.one * highlightSize;

//        Material highlightMaterial = new Material(Shader.Find("Standard"));
//        highlightMaterial.color = highlightColor;
//        sphere.GetComponent<Renderer>().material = highlightMaterial;
//    }

//}

//using UnityEngine;
//using System.Collections.Generic;
//using System.Linq;
//using System;
//public class NewEntropy : MonoBehaviour
//{

//    public MeshFilter meshFilter;
//    public float[] sigmaScales = new float[] { 0.1f, 0.2f, 0.3f }; // Multi-scale sigma values
//    public float saliencyThreshold = 0.75f; // Threshold for saliency-based selection
//    public int topVerticesToHighlight = 6; // Select top 6 vertices
//    public int debugUniquePositions = 20; // Print only the top 20 vertices for debugging
//    public Color highlightColor = Color.red;
//    public float highlightSize = 0.05f;

//    private Dictionary<int, Vector3> vertexPositions = new Dictionary<int, Vector3>();
//    private Dictionary<int, Vector3> vertexNormals = new Dictionary<int, Vector3>();


//    void Start()
//    {
//        if (meshFilter == null)
//        {
//            Debug.LogError("MeshFilter is not assigned.");
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



//        // Step 2: Calculate multi-scale entropy for all vertices
//        float[] entropyValues = CalculateMultiScaleEntropy(positions, normals, sigmaScales);

//        // Print entropy values for verification
//        Debug.Log("===== Entropy Values for Each Vertex =====");
//        for (int i = 0; i < entropyValues.Length; i++)
//        {
//            Debug.Log($"Vertex {i}: Entropy = {entropyValues[i]}");
//        }
//        // Step 3: Print 20 unique vertex positions for debugging
//        DebugTopUniqueVertices(entropyValues);

//        // Step 3: Highlight top saliency vertices based on threshold
//        HighlightTopUniqueVertices(entropyValues);


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

//        return totalEdges > 0 ? totalDistance / totalEdges : 1f; // Prevent division by zero
//    }


//    private float[] CalculateMultiScaleEntropy(Vector3[] vertices, Vector3[] normals, float[] sigmaScales)
//    {
//        float[] entropyValues = new float[vertices.Length];
//        float L = ComputeMeshCharacteristicLength(vertices); // Compute mesh scale once

//        for (int i = 0; i < vertices.Length; i++)
//        {
//            float aggregatedEntropy = 0f;
//            float totalWeight = 0f;

//            foreach (float sigma in sigmaScales)
//            {
//                float scaledSigma = sigma * L; // Scale sigma by L
//                List<int> neighbors = GetNeighborsByEuclideanDistance(i, scaledSigma, vertices);

//                if (neighbors.Count == 0) continue;

//                Dictionary<Vector3Int, int> histogram = new Dictionary<Vector3Int, int>();
//                foreach (int neighborIndex in neighbors)
//                {
//                    Vector3 normal = normals[neighborIndex];
//                    Vector3Int quantizedNormal = QuantizeNormal(normal, neighbors.Count); // Dynamic bin size

//                    if (!histogram.ContainsKey(quantizedNormal))
//                    {
//                        histogram[quantizedNormal] = 0;
//                    }
//                    histogram[quantizedNormal]++;
//                }

//                float entropy = 0f;
//                int total = neighbors.Count;

//                foreach (var count in histogram.Values)
//                {
//                    float probability = (float)count / total;
//                    entropy -= probability * Mathf.Log(probability);
//                }

//                float weight = 1.0f / sigma; // Weight smaller scales higher
//                aggregatedEntropy += weight * entropy;
//                totalWeight += weight;
//            }

//            entropyValues[i] = aggregatedEntropy / totalWeight; // Normalize across scales
//        }

//        return entropyValues;
//    }

//    private List<int> GetNeighborsByEuclideanDistance(int index, float sigma, Vector3[] vertices)
//    {
//        List<int> neighbors = new List<int>();

//        for (int i = 0; i < vertices.Length; i++)
//        {
//            if (i == index) continue; // Skip self-comparison
//            if (Vector3.Distance(vertices[index], vertices[i]) <= sigma)
//            {
//                neighbors.Add(i);
//            }
//        }

//        return neighbors;
//    }


//    private Vector3Int QuantizeNormal(Vector3 normal, int binSize)
//    {
//        int scale = Mathf.Clamp(binSize, 10, 5000); // Dynamic bin size
//        return new Vector3Int(
//            Mathf.RoundToInt(normal.x * scale),
//            Mathf.RoundToInt(normal.y * scale),
//            Mathf.RoundToInt(normal.z * scale)
//        );
//    }
//    private void DebugTopUniqueVertices(float[] entropyValues)
//    {
//        // Group vertices by unique positions
//        Dictionary<Vector3, List<int>> positionToVertexMap = new Dictionary<Vector3, List<int>>();
//        foreach (var kvp in vertexPositions)
//        {
//            if (!positionToVertexMap.ContainsKey(kvp.Value))
//                positionToVertexMap[kvp.Value] = new List<int>();

//            positionToVertexMap[kvp.Value].Add(kvp.Key);
//        }

//        // Select the vertex with the highest entropy for each unique position
//        Dictionary<Vector3, int> highestEntropyVertexMap = new Dictionary<Vector3, int>();
//        foreach (var kvp in positionToVertexMap)
//        {
//            int bestVertex = kvp.Value.OrderByDescending(v => entropyValues[v]).First();
//            highestEntropyVertexMap[kvp.Key] = bestVertex;
//        }

//        // Debugging: Print the top 20 unique positions by entropy
//        Debug.Log("===== Top 20 Unique Positions by Entropy =====");
//        var debugUniqueVertices = highestEntropyVertexMap
//            .OrderByDescending(kvp => entropyValues[kvp.Value])
//            .Take(debugUniquePositions)
//            .ToList();

//        foreach (var kvp in debugUniqueVertices)
//        {
//            Debug.Log($"Position {kvp.Key} - Vertex {kvp.Value} - Entropy: {entropyValues[kvp.Value]}");
//        }
//    }
//    private void HighlightTopUniqueVertices(float[] entropyValues)
//    {
//        // Step 1: Group vertices by unique positions
//        Dictionary<Vector3, List<int>> positionToVertexMap = new Dictionary<Vector3, List<int>>();
//        foreach (var kvp in vertexPositions)
//        {
//            if (!positionToVertexMap.ContainsKey(kvp.Value))
//                positionToVertexMap[kvp.Value] = new List<int>();

//            positionToVertexMap[kvp.Value].Add(kvp.Key);
//        }

//        // Step 2: Select the vertex with the highest entropy for each unique position
//        Dictionary<Vector3, int> highestEntropyVertexMap = new Dictionary<Vector3, int>();
//        foreach (var kvp in positionToVertexMap)
//        {
//            int bestVertex = kvp.Value.OrderByDescending(v => entropyValues[v]).First();
//            highestEntropyVertexMap[kvp.Key] = bestVertex;
//        }

//        // Step 3: Select the top 6 highest entropy unique vertices
//        var topUniqueVertices = highestEntropyVertexMap
//            .OrderByDescending(kvp => entropyValues[kvp.Value])
//            .Take(topVerticesToHighlight)
//            .Select(kvp => kvp.Value)
//            .ToList();

//        Debug.Log("===== Top 6 Unique Salient Vertices =====");
//        foreach (int vertexIndex in topUniqueVertices)
//        {
//            Debug.Log($"Vertex {vertexIndex} - Entropy: {entropyValues[vertexIndex]}");

//            if (vertexPositions.TryGetValue(vertexIndex, out Vector3 position))
//            {
//                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
//                sphere.transform.position = meshFilter.transform.TransformPoint(position);
//                sphere.transform.localScale = Vector3.one * highlightSize;
//                sphere.GetComponent<Renderer>().material.color = highlightColor;
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
//    public int neighborCount = 10; // For fixed neighbor mode (optional)
//    public float[] sigmaScales = new float[] { 0.1f, 0.2f, 0.3f }; // Multi-scale sigma values
//    public int topVerticesToHighlight = 6; // Final number of vertices to highlight
//    public Color highlightColor = Color.red;
//    public float highlightSize = 0.05f;

//    // Vertex data storage
//    private Dictionary<int, Vector3> vertexPositions = new Dictionary<int, Vector3>();
//    private Dictionary<int, Vector3> vertexNormals = new Dictionary<int, Vector3>();

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

//        // Step 2: Calculate multi-scale entropy for all vertices
//        float[] entropyValues = CalculateMultiScaleEntropy(positions, normals, sigmaScales);

//        // Step 3: Highlight top vertices based on entropy
//        HighlightTopVerticesWithMaxMin(entropyValues);
//    }

//    // Calculate multi-scale entropy
//    private float[] CalculateMultiScaleEntropy(Vector3[] vertices, Vector3[] normals, float[] sigmaScales)
//    {
//        float[] entropyValues = new float[vertices.Length];

//        for (int i = 0; i < vertices.Length; i++)
//        {
//            float aggregatedEntropy = 0f;

//            foreach (float sigma in sigmaScales)
//            {
//                List<int> neighbors = GetNeighborsByDistance(vertices, i, sigma);

//                // Step 1: Build histogram of neighbor normals
//                Dictionary<Vector3Int, int> histogram = new Dictionary<Vector3Int, int>();
//                foreach (int neighborIndex in neighbors)
//                {
//                    Vector3 normal = normals[neighborIndex];
//                    Vector3Int quantizedNormal = QuantizeNormal(normal);

//                    if (!histogram.ContainsKey(quantizedNormal))
//                    {
//                        histogram[quantizedNormal] = 0;
//                    }
//                    histogram[quantizedNormal]++;
//                }

//                // Step 2: Calculate entropy for this scale
//                float entropy = 0f;
//                int total = neighbors.Count;

//                foreach (var count in histogram.Values)
//                {
//                    float probability = (float)count / total;
//                    entropy -= probability * Mathf.Log(probability);
//                }

//                aggregatedEntropy += entropy; // Aggregate entropy across scales
//            }

//            // Average entropy over all scales
//            entropyValues[i] = aggregatedEntropy / sigmaScales.Length;
//            Debug.Log($"Vertex {i} - Multi-Scale Entropy: {entropyValues[i]}");
//        }

//        return entropyValues;
//    }

//    // Find neighbors within sigma distance
//    private List<int> GetNeighborsByDistance(Vector3[] vertices, int index, float sigma)
//    {
//        Vector3 centerVertex = vertices[index];

//        return vertices
//            .Select((v, idx) => new { idx, distance = Vector3.Distance(v, centerVertex) })
//            .Where(x => x.distance > 0 && x.distance <= sigma) // Exclude self and apply sigma threshold
//            .Select(x => x.idx)
//            .ToList();

//    }

//    // Quantize normals for histogram
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
//            .Take(topVerticesToHighlight * 3) // Take more candidates for Max-Min
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