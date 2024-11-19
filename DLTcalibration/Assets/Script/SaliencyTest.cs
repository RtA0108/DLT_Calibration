using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SaliencyTest : MonoBehaviour
{

    public int featurePointCount = 6;  // Number of feature points to select
    public float visualizationSize = 10.0f;  // Size of feature point markers

    private Mesh mesh;
    private Vector3[] vertices;
    private Vector3[] normals;
    private int[] triangles;
    private List<Vector3> featurePoints = new List<Vector3>();

    void Start()
    {
        // Get the mesh data
        mesh = GetComponent<MeshFilter>().mesh;
        vertices = mesh.vertices;
        normals = mesh.normals;
        triangles = mesh.triangles;

        // Compute feature points
        featurePoints = ComputeFeaturePoints();
        
        // Visualize feature points
        VisualizeFeaturePoints();
    }

    List<Vector3> ComputeFeaturePoints()
    {
        // Calculate saliency for each vertex
        float[] saliency = new float[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            saliency[i] = ComputeCurvature(i);
        }

        // Normalize saliency values
        float minSaliency = saliency.Min();
        float maxSaliency = saliency.Max();
        for (int i = 0; i < saliency.Length; i++)
        {
            saliency[i] = (saliency[i] - minSaliency) / (maxSaliency - minSaliency);
        }

        // Select top feature points based on saliency
        List<int> topIndices = saliency
            .Select((value, index) => new { Value = value, Index = index })
            .OrderByDescending(x => x.Value) //돌출값이 높은 순서로 정렬
            .Take(featurePointCount)
            .Select(x => x.Index)
            .ToList();

        // Convert selected indices to world-space coordinates
        List<Vector3> selectedPoints = new List<Vector3>();
        foreach (int index in topIndices)
        {
            selectedPoints.Add(transform.TransformPoint(vertices[index]));
        }
        foreach (Vector3 point in selectedPoints)
        {
            Debug.Log(point);
        }
        return selectedPoints;
    }

    float ComputeCurvature(int vertexIndex)
    {
        // Find neighboring vertices
        List<int> neighbors = GetNeighbors(vertexIndex);

        // Compute mean curvature based on normals
        Vector3 vertexNormal = normals[vertexIndex];
        float curvature = 0f;

        foreach (int neighborIndex in neighbors)
        {
            Vector3 edge = vertices[neighborIndex] - vertices[vertexIndex];
            curvature += Vector3.Dot(vertexNormal, edge.normalized);
        }

        return Mathf.Abs(curvature / neighbors.Count);
    }

    List<int> GetNeighbors(int vertexIndex)
    {
        // Find all vertices connected to the given vertex by edges
        List<int> neighbors = new List<int>();
        for (int i = 0; i < triangles.Length; i += 3)
        {
            if (triangles[i] == vertexIndex || triangles[i + 1] == vertexIndex || triangles[i + 2] == vertexIndex)
            {
                neighbors.Add(triangles[i]);
                neighbors.Add(triangles[i + 1]);
                neighbors.Add(triangles[i + 2]);
            }
        }
        neighbors = neighbors.Distinct().Where(index => index != vertexIndex).ToList();
        return neighbors;
    }

    void VisualizeFeaturePoints()
    {
        foreach (Vector3 point in featurePoints)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.transform.position = point;
            marker.transform.localScale = Vector3.one * visualizationSize;
            marker.GetComponent<Renderer>().material.color = Color.red;

            // Optionally, make the markers not interact with physics
            Destroy(marker.GetComponent<Collider>());
        }
    }
}
//using System.Collections;
//using System.Collections.Generic;
//using System.Linq;
//using UnityEngine;

//public class SaliencyTest : MonoBehaviour
//{

//    public int featurePointCount = 6;  // Number of feature points to select
//    public float visualizationSize = 10.0f;  // Size of feature point markers
//    public int smoothingIterations = 3;  // Number of smoothing levels (scales)

//    private Mesh mesh;
//    private Vector3[] vertices;
//    private Vector3[] normals;
//    private int[] triangles;
//    private List<Vector3> featurePoints = new List<Vector3>();

//    void Start()
//    {
//        // Get the mesh data
//        mesh = GetComponent<MeshFilter>().mesh;
//        vertices = mesh.vertices;
//        normals = mesh.normals;
//        triangles = mesh.triangles;

//        // Compute feature points
//        featurePoints = ComputeFeaturePoints();

//        // Visualize feature points
//        VisualizeFeaturePoints();
//    }

//    List<Vector3> ComputeFeaturePoints()
//    {
//        // Step 1: Compute curvature at the original scale
//        float[] originalCurvature = ComputeCurvature(vertices);

//        // Step 2: Compute curvature differences across multiple scales
//        List<float[]> curvatureDifferences = new List<float[]>();
//        Vector3[] currentVertices = (Vector3[])vertices.Clone();

//        for (int i = 0; i < smoothingIterations; i++)
//        {
//            currentVertices = SmoothMesh(currentVertices); // Smooth mesh
//            float[] smoothedCurvature = ComputeCurvature(currentVertices);

//            // Calculate curvature differences
//            float[] curvatureDifference = new float[vertices.Length];
//            for (int j = 0; j < vertices.Length; j++)
//            {
//                curvatureDifference[j] = Mathf.Abs(originalCurvature[j] - smoothedCurvature[j]);
//            }
//            curvatureDifferences.Add(curvatureDifference);
//        }

//        // Step 3: Aggregate saliency across scales
//        float[] saliency = new float[vertices.Length];
//        foreach (float[] curvatureDifference in curvatureDifferences)
//        {
//            for (int i = 0; i < vertices.Length; i++)
//            {
//                saliency[i] += curvatureDifference[i];
//            }
//        }

//        // Step 4: Normalize saliency
//        float maxSaliency = saliency.Max();
//        for (int i = 0; i < saliency.Length; i++)
//        {
//            saliency[i] /= maxSaliency; // Normalize to [0, 1]
//        }

//        // Step 5: Select top feature points
//        List<int> topIndices = saliency
//            .Select((value, index) => new { Value = value, Index = index })
//            .OrderByDescending(x => x.Value)
//            .Take(featurePointCount)
//            .Select(x => x.Index)
//            .ToList();

//        // Convert indices to world-space coordinates
//        List<Vector3> selectedPoints = new List<Vector3>();
//        foreach (int index in topIndices)
//        {
//            selectedPoints.Add(transform.TransformPoint(vertices[index]));
//        }

//        return selectedPoints;
//    }

//    float[] ComputeCurvature(Vector3[] meshVertices)
//    {
//        float[] curvature = new float[meshVertices.Length];

//        for (int i = 0; i < meshVertices.Length; i++)
//        {
//            List<int> neighbors = GetNeighbors(i);
//            Vector3 vertex = meshVertices[i];

//            // Calculate mean curvature approximation
//            float meanCurvature = 0f;
//            foreach (int neighborIndex in neighbors)
//            {
//                Vector3 neighbor = meshVertices[neighborIndex];
//                meanCurvature += Vector3.Distance(vertex, neighbor);
//            }
//            curvature[i] = meanCurvature / neighbors.Count; // Average distance
//        }

//        return curvature;
//    }

//    Vector3[] SmoothMesh(Vector3[] meshVertices)
//    {
//        Vector3[] smoothedVertices = (Vector3[])meshVertices.Clone();

//        for (int i = 0; i < meshVertices.Length; i++)
//        {
//            List<int> neighbors = GetNeighbors(i);
//            Vector3 averageNeighbor = Vector3.zero;

//            foreach (int neighborIndex in neighbors)
//            {
//                averageNeighbor += meshVertices[neighborIndex];
//            }
//            averageNeighbor /= neighbors.Count;
//            smoothedVertices[i] = Vector3.Lerp(meshVertices[i], averageNeighbor, 0.5f); // Weighted smoothing
//        }

//        return smoothedVertices;
//    }

//    List<int> GetNeighbors(int vertexIndex)
//    {
//        List<int> neighbors = new List<int>();
//        for (int i = 0; i < triangles.Length; i += 3)
//        {
//            if (triangles[i] == vertexIndex || triangles[i + 1] == vertexIndex || triangles[i + 2] == vertexIndex)
//            {
//                neighbors.Add(triangles[i]);
//                neighbors.Add(triangles[i + 1]);
//                neighbors.Add(triangles[i + 2]);
//            }
//        }
//        neighbors = neighbors.Distinct().Where(index => index != vertexIndex).ToList();
//        return neighbors;
//    }

//    void VisualizeFeaturePoints()
//    {
//        foreach (Vector3 point in featurePoints)
//        {
//            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
//            marker.transform.position = point;
//            marker.transform.localScale = Vector3.one * visualizationSize;
//            marker.GetComponent<Renderer>().material.color = Color.red;

//            // Remove physics interaction
//            Destroy(marker.GetComponent<Collider>());
//        }
//    }
//}