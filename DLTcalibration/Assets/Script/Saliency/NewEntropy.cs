using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class NewEntropy : MonoBehaviour
{ 
    public MeshFilter meshFilter;
    public int neighborCount = 10;
    public int topVerticesToHighlight = 6;
    public Color highlightColor = Color.red;
    public float highlightSize = 0.05f;

    private Dictionary<int, float> vertexEntropy = new Dictionary<int, float>();
    private Dictionary<int, List<int>> vertexNeighbors = new Dictionary<int, List<int>>();

    void Start()
    {
        if (meshFilter == null)
        {
            Debug.LogError("No mesh filter assigned.");
            return;
        }

        Mesh mesh = meshFilter.mesh;
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = CalculateVertexNormals(mesh);

        // Compute entropy for each vertex
        for (int i = 0; i < vertices.Length; i++)
        {
            List<int> neighbors = GetNeighbors(vertices, i, neighborCount);
            vertexNeighbors[i] = neighbors;
            float entropy = CalculateEntropy(normals, neighbors);
            vertexEntropy[i] = entropy;

            Debug.Log($"Vertex {i} - Entropy: {entropy}, Neighbors: {string.Join(",", neighbors)}");
        }

        HighlightTopVertices(vertices);
    }

    private Vector3[] CalculateVertexNormals(Mesh mesh)
    {
        Vector3[] normals = new Vector3[mesh.vertexCount];
        int[] triangles = mesh.triangles;
        Vector3[] vertices = mesh.vertices;

        for (int i = 0; i < triangles.Length; i += 3)
        {
            int v0 = triangles[i];
            int v1 = triangles[i + 1];
            int v2 = triangles[i + 2];

            Vector3 normal = Vector3.Cross(vertices[v1] - vertices[v0], vertices[v2] - vertices[v0]).normalized;
            normals[v0] += normal;
            normals[v1] += normal;
            normals[v2] += normal;
        }

        for (int i = 0; i < normals.Length; i++)
        {
            normals[i].Normalize();
        }

        return normals;
    }

    private List<int> GetNeighbors(Vector3[] vertices, int index, int count)
    {
        return vertices
            .Select((v, idx) => new { v, idx, distance = Vector3.Distance(v, vertices[index]) })
            .OrderBy(x => x.distance)
            .Skip(1)  // Skip itself
            .Take(count)
            .Select(x => x.idx)
            .ToList();
    }

    private float CalculateEntropy(Vector3[] normals, List<int> neighbors)
    {
        Dictionary<Vector3, int> histogram = new Dictionary<Vector3, int>();

        foreach (var neighbor in neighbors)
        {
            Vector3 normal = normals[neighbor];
            if (!histogram.ContainsKey(normal))
            {
                histogram[normal] = 0;
            }
            histogram[normal]++;
        }

        float entropy = 0f;
        int total = neighbors.Count;

        foreach (var count in histogram.Values)
        {
            float probability = (float)count / total;
            entropy -= probability * Mathf.Log(probability);
        }

        return entropy;
    }

    private void HighlightTopVertices(Vector3[] vertices)
    {
        var topIndices = vertexEntropy
            .OrderByDescending(pair => pair.Value)
            .Take(topVerticesToHighlight)
            .Select(pair => pair.Key)
            .ToList();

        foreach (int idx in topIndices)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.position = meshFilter.transform.TransformPoint(vertices[idx]);
            sphere.transform.localScale = Vector3.one * highlightSize;
            sphere.GetComponent<Renderer>().material.color = highlightColor;

            Debug.Log($"Highlighted Vertex {idx} with Entropy: {vertexEntropy[idx]}");
        }
    }

    public float GetVertexEntropy(int vertexIndex)
    {
        return vertexEntropy.ContainsKey(vertexIndex) ? vertexEntropy[vertexIndex] : 0f;
    }

    public List<int> GetVertexNeighbors(int vertexIndex)
    {
        return vertexNeighbors.ContainsKey(vertexIndex) ? vertexNeighbors[vertexIndex] : new List<int>();
    }
}
