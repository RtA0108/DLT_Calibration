using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class EntropySaliency : MonoBehaviour
{
    public float neighborhoodRadius = 0.1f; // Radius for local neighborhood analysis
    public float visualizationSize = 10.0f; // Size of saliency markers

    private Mesh mesh;
    private Vector3[] vertices;
    private int[] triangles;
    private List<Vector3> saliencyPoints = new List<Vector3>();

    void Start()
    {
        // Get the mesh
        mesh = GetComponent<MeshFilter>().mesh;
        vertices = mesh.vertices.Distinct().ToArray(); // Remove duplicate vertices
        triangles = mesh.triangles;

        // Step 1: Compute saliency using entropy
        float[] saliency = ComputeEntropySaliency();

        // Step 2: Visualize saliency points
        saliencyPoints = SelectSalientPoints(saliency);
        VisualizeSaliencyPoints(saliencyPoints);
    }

    float[] ComputeEntropySaliency()
    {
        int vertexCount = vertices.Length;
        float[] saliency = new float[vertexCount];

        // Iterate through each vertex
        for (int i = 0; i < vertexCount; i++)
        {
            // Get the local neighborhood
            List<int> neighbors = GetNeighborhood(i);

            // Compute normal vectors for the neighborhood
            List<Vector3> normals = neighbors.Select(n => ComputeNormal(n)).ToList();

            // Create a histogram of normal vectors
            int binCount = 10; // Number of bins for the histogram
            float[] histogram = ComputeNormalHistogram(normals, binCount);

            // Compute entropy from the histogram
            saliency[i] = ComputeEntropy(histogram);
        }

        return NormalizeSaliency(saliency);
    }

    List<int> GetNeighborhood(int vertexIndex)
    {
        List<int> neighbors = new List<int>();
        Vector3 vertexPosition = vertices[vertexIndex];

        for (int i = 0; i < vertices.Length; i++)
        {
            if (i != vertexIndex && Vector3.Distance(vertexPosition, vertices[i]) <= neighborhoodRadius)
            {
                neighbors.Add(i);
            }
        }

        return neighbors;
    }

    Vector3 ComputeNormal(int vertexIndex)
    {
        List<int> connectedTriangles = new List<int>();

        for (int i = 0; i < triangles.Length; i += 3)
        {
            if (triangles[i] == vertexIndex || triangles[i + 1] == vertexIndex || triangles[i + 2] == vertexIndex)
            {
                connectedTriangles.Add(i);
            }
        }

        Vector3 normal = Vector3.zero;

        foreach (int triIndex in connectedTriangles)
        {
            Vector3 v1 = vertices[triangles[triIndex]];
            Vector3 v2 = vertices[triangles[triIndex + 1]];
            Vector3 v3 = vertices[triangles[triIndex + 2]];

            Vector3 triangleNormal = Vector3.Cross(v2 - v1, v3 - v1).normalized;
            normal += triangleNormal;
        }

        return normal.normalized;
    }

    float[] ComputeNormalHistogram(List<Vector3> normals, int binCount)
    {
        float[] histogram = new float[binCount];
        Vector3 referenceVector = Vector3.up;

        foreach (Vector3 normal in normals)
        {
            float angle = Vector3.Angle(referenceVector, normal);
            int binIndex = Mathf.FloorToInt((angle / 180f) * binCount);
            binIndex = Mathf.Clamp(binIndex, 0, binCount - 1);
            histogram[binIndex] += 1f;
        }

        // Normalize the histogram
        float total = histogram.Sum();
        for (int i = 0; i < histogram.Length; i++)
        {
            histogram[i] /= total;
        }

        return histogram;
    }

    float ComputeEntropy(float[] histogram)
    {
        float entropy = 0f;
        foreach (float bin in histogram)
        {
            if (bin > 0)
            {
                entropy -= bin * Mathf.Log(bin);
            }
        }
        return entropy;
    }

    float[] NormalizeSaliency(float[] saliency)
    {
        float maxVal = saliency.Max();
        return saliency.Select(s => s / maxVal).ToArray();
    }

    List<Vector3> SelectSalientPoints(float[] saliency)
    {
        // Select vertices with the highest saliency
        int topCount = 12;
        var topIndices = saliency
            .Select((value, index) => new { value, index })
            .OrderByDescending(x => x.value)
            .Take(topCount)
            .Select(x => x.index);

        return topIndices.Select(index => vertices[index]).ToList();
    }

    void VisualizeSaliencyPoints(List<Vector3> salientPoints)
    {
        foreach (var point in salientPoints)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.transform.position = transform.TransformPoint(point);
            marker.transform.localScale = Vector3.one * visualizationSize;
            marker.GetComponent<Renderer>().material.color = Color.blue;

            Destroy(marker.GetComponent<Collider>()); // Optional
        }

        Debug.Log("Salient Points Visualized:");
        foreach (var point in salientPoints)
        {
            Debug.Log(point);
        }
    }
}
