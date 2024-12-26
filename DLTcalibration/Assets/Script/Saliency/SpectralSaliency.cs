//using UnityEngine;
//using System.Collections.Generic;
//using System.Linq;

//public class SpectralSaliency : MonoBehaviour
//{
//    public int kMin = 2; // Minimum eigenfunction index to use
//    public int kMax = 10; // Maximum eigenfunction index to use
//    public float visualizationSize = 10.0f; // Size of saliency markers

//    private Mesh mesh;
//    private Vector3[] vertices;
//    private int[] triangles;
//    private List<Vector3> saliencyPoints = new List<Vector3>();

//    void Start()
//    {
//        // Get the mesh
//        mesh = GetComponent<MeshFilter>().mesh;
//        vertices = mesh.vertices;
//        triangles = mesh.triangles;

//        // Step 1: Compute the Laplace-Beltrami Operator (LBO)
//        var LBO = ComputeLaplaceBeltramiOperator();

//        // Step 2: Perform Spectral Decomposition
//        var (eigenvalues, eigenfunctions) = SpectralDecomposition(LBO);
//        kMax = Mathf.Min(kMax, eigenfunctions.Length - 1);
//        // Step 3: Compute Saliency
//        float[] saliency = ComputeSaliency(eigenvalues, eigenfunctions);

//        // Step 4: Visualize Salient Points
//        saliencyPoints = SelectSalientPoints(saliency);
//        VisualizeSaliencyPoints(saliencyPoints);
//    }

//    // Sparse matrix implementation using a dictionary
//    private class SparseMatrix
//    {
//        private Dictionary<(int, int), float> data = new Dictionary<(int, int), float>();
//        private int size;

//        public SparseMatrix(int size)
//        {
//            this.size = size;
//        }

//        public float this[int row, int col]
//        {
//            get
//            {
//                if (data.ContainsKey((row, col)))
//                    return data[(row, col)];
//                return 0f; // Default value for sparse matrix
//            }
//            set
//            {
//                data[(row, col)] = value;
//            }
//        }

//        public int Size => size;

//        public Dictionary<(int, int), float> GetData()
//        {
//            return data;
//        }
//    }

//    SparseMatrix ComputeLaplaceBeltramiOperator()
//    {
//        SparseMatrix LBO = new SparseMatrix(vertices.Length);

//        for (int i = 0; i < triangles.Length; i += 3)
//        {
//            int v1 = triangles[i];
//            int v2 = triangles[i + 1];
//            int v3 = triangles[i + 2];

//            AddCotangentWeight(LBO, v1, v2, v3);
//            AddCotangentWeight(LBO, v2, v3, v1);
//            AddCotangentWeight(LBO, v3, v1, v2);
//        }

//        return LBO;
//    }

//    void AddCotangentWeight(SparseMatrix LBO, int vi, int vj, int vk)
//    {
//        Vector3 a = vertices[vi];
//        Vector3 b = vertices[vj];
//        Vector3 c = vertices[vk];

//        float cotAlpha = Cotangent(a, b, c);
//        LBO[vi, vj] -= cotAlpha;
//        LBO[vj, vi] -= cotAlpha;

//        LBO[vi, vi] += cotAlpha;
//        LBO[vj, vj] += cotAlpha;
//    }

//    float Cotangent(Vector3 a, Vector3 b, Vector3 c)
//    {
//        Vector3 ab = b - a;
//        Vector3 ac = c - a;
//        return Vector3.Dot(ab, ac) / Vector3.Cross(ab, ac).magnitude;
//    }

//    (float[], float[][]) SpectralDecomposition(SparseMatrix LBO)
//    {
//        int vertexCount = vertices.Length;
//        int eigenCount = kMax - kMin + 1;

//        float[] eigenvalues = new float[eigenCount];
//        float[][] eigenfunctions = new float[eigenCount][];

//        for (int k = 0; k < eigenCount; k++)
//        {
//            float[] eigenvector = PowerIteration(LBO, vertexCount, 100, 1e-6f);
//            float eigenvalue = RayleighQuotient(LBO, eigenvector);

//            eigenvalues[k] = eigenvalue;
//            eigenfunctions[k] = eigenvector;

//            // Deflate the matrix to find the next eigenvalue/vector
//            DeflateMatrix(LBO, eigenvector, eigenvalue);
//        }

//        return (eigenvalues, eigenfunctions);
//    }

//    float[] PowerIteration(SparseMatrix LBO, int size, int maxIterations, float tolerance)
//    {
//        float[] b = new float[size];
//        float[] nextB = new float[size];

//        // Initialize b with random values
//        for (int i = 0; i < size; i++)
//        {
//            b[i] = Random.Range(-1f, 1f);
//        }

//        // Normalize b
//        Normalize(b);

//        for (int iter = 0; iter < maxIterations; iter++)
//        {
//            // Multiply by LBO
//            for (int i = 0; i < size; i++)
//            {
//                nextB[i] = 0f;
//                foreach (var kvp in LBO.GetData().Where(kv => kv.Key.Item1 == i))
//                {
//                    nextB[i] += kvp.Value * b[kvp.Key.Item2];
//                }
//            }

//            // Normalize nextB
//            Normalize(nextB);

//            // Check for convergence
//            if (VectorDifference(nextB, b) < tolerance)
//                break;

//            // Swap b and nextB
//            var temp = b;
//            b = nextB;
//            nextB = temp;
//        }

//        return b;
//    }

//    float RayleighQuotient(SparseMatrix LBO, float[] eigenvector)
//    {
//        float numerator = 0f;
//        float denominator = 0f;

//        for (int i = 0; i < eigenvector.Length; i++)
//        {
//            float rowSum = 0f;
//            foreach (var kvp in LBO.GetData().Where(kv => kv.Key.Item1 == i))
//            {
//                rowSum += kvp.Value * eigenvector[kvp.Key.Item2];
//            }
//            numerator += eigenvector[i] * rowSum;
//            denominator += eigenvector[i] * eigenvector[i];
//        }

//        return numerator / denominator;
//    }

//    void DeflateMatrix(SparseMatrix LBO, float[] eigenvector, float eigenvalue)
//    {
//        for (int i = 0; i < eigenvector.Length; i++)
//        {
//            for (int j = 0; j < eigenvector.Length; j++)
//            {
//                LBO[i, j] -= eigenvalue * eigenvector[i] * eigenvector[j];
//            }
//        }
//    }

//    void Normalize(float[] vector)
//    {
//        float norm = Mathf.Sqrt(vector.Sum(v => v * v));
//        for (int i = 0; i < vector.Length; i++)
//        {
//            vector[i] /= norm;
//        }
//    }

//    float VectorDifference(float[] v1, float[] v2)
//    {
//        float diff = 0f;
//        for (int i = 0; i < v1.Length; i++)
//        {
//            diff += Mathf.Abs(v1[i] - v2[i]);
//        }
//        return diff;
//    }

//    float[] ComputeSaliency(float[] eigenvalues, float[][] eigenfunctions)
//    {
//        kMax = Mathf.Min(kMax, eigenfunctions.Length - 1);

//        float[] saliency = new float[vertices.Length];
//        for (int i = kMin; i <= kMax; i++)
//        {
//            for (int v = 0; v < vertices.Length; v++)
//            {
//                saliency[v] += Mathf.Abs(eigenfunctions[i][v]);
//            }
//        }
//        return NormalizeSaliency(saliency);
//    }

//    float[] NormalizeSaliency(float[] saliency)
//    {
//        float maxVal = saliency.Max();
//        return saliency.Select(s => s / maxVal).ToArray();
//    }

//    List<Vector3> SelectSalientPoints(float[] saliency)
//    {
//        // Select vertices with the highest saliency
//        int topCount = 6;
//        var topIndices = saliency
//            .Select((value, index) => new { value, index })
//            .OrderByDescending(x => x.value)
//            .Take(topCount)
//            .Select(x => x.index);

//        return topIndices.Select(index => vertices[index]).ToList();
//    }

//    void VisualizeSaliencyPoints(List<Vector3> salientPoints)
//    {
//        foreach (var point in salientPoints)
//        {
//            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
//            marker.transform.position = transform.TransformPoint(point);
//            marker.transform.localScale = Vector3.one * visualizationSize;
//            marker.GetComponent<Renderer>().material.color = Color.red;

//            Destroy(marker.GetComponent<Collider>()); // Optional
//        }

//        Debug.Log("Salient Points Visualized:");
//        foreach (var point in salientPoints)
//        {
//            Debug.Log(point);
//        }
//    }
//}


using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class SpectralSaliency : MonoBehaviour
{
    public int kMin = 2; // Minimum eigenfunction index to use
    public int kMax = 10; // Maximum eigenfunction index to use
    public float visualizationSize = 10.0f; // Size of saliency markers

    private Mesh mesh;
    private Vector3[] vertices;
    private int[] triangles;
    private List<Vector3> saliencyPoints = new List<Vector3>();

    void Start()
    {
        // Get the mesh
        mesh = GetComponent<MeshFilter>().mesh;
        vertices = mesh.vertices;
        triangles = mesh.triangles;

        // Step 1: Compute the Laplace-Beltrami Operator (LBO)
        var LBO = ComputeLaplaceBeltramiOperator();

        // Step 2: Perform Spectral Decomposition
        var (eigenvalues, eigenfunctions) = SpectralDecomposition(LBO);
        kMax = Mathf.Min(kMax, eigenfunctions.Length - 1);
        // Step 3: Compute Saliency
        float[] saliency = ComputeSaliency(eigenvalues, eigenfunctions);

        // Step 4: Visualize Salient Points
        saliencyPoints = SelectSalientPoints(saliency);
        VisualizeSaliencyPoints(saliencyPoints);
    }

    // Sparse matrix implementation using a dictionary
    private class SparseMatrix
    {
        private Dictionary<(int, int), float> data = new Dictionary<(int, int), float>();
        private int size;

        public SparseMatrix(int size)
        {
            this.size = size;
        }

        public float this[int row, int col]
        {
            get
            {
                if (data.ContainsKey((row, col)))
                    return data[(row, col)];
                return 0f; // Default value for sparse matrix
            }
            set
            {
                data[(row, col)] = value;
            }
        }

        public int Size => size;

        public Dictionary<(int, int), float> GetData()
        {
            return data;
        }
    }

    SparseMatrix ComputeLaplaceBeltramiOperator()
    {
        SparseMatrix LBO = new SparseMatrix(vertices.Length);

        for (int i = 0; i < triangles.Length; i += 3)
        {
            int v1 = triangles[i];
            int v2 = triangles[i + 1];
            int v3 = triangles[i + 2];

            AddCotangentWeight(LBO, v1, v2, v3);
            AddCotangentWeight(LBO, v2, v3, v1);
            AddCotangentWeight(LBO, v3, v1, v2);
        }

        return LBO;
    }

    void AddCotangentWeight(SparseMatrix LBO, int vi, int vj, int vk)
    {
        Vector3 a = vertices[vi];
        Vector3 b = vertices[vj];
        Vector3 c = vertices[vk];

        float cotAlpha = Cotangent(a, b, c);
        LBO[vi, vj] -= cotAlpha;
        LBO[vj, vi] -= cotAlpha;

        LBO[vi, vi] += cotAlpha;
        LBO[vj, vj] += cotAlpha;
    }

    float Cotangent(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ab = b - a;
        Vector3 ac = c - a;
        return Vector3.Dot(ab, ac) / Vector3.Cross(ab, ac).magnitude;
    }

    (float[], float[][]) SpectralDecomposition(SparseMatrix LBO)
    {
        // Placeholder for eigenvalue decomposition
        // Use external libraries or implement an eigenvalue solver
        Debug.LogError("Spectral decomposition not implemented. Use a library for eigenvalue solving.");
        return (new float[0], new float[0][]);
    }

    float[] ComputeSaliency(float[] eigenvalues, float[][] eigenfunctions)
    {
        float[] saliency = new float[vertices.Length];
        for (int i = kMin; i <= kMax; i++)
        {
            for (int v = 0; v < vertices.Length; v++)
            {
                saliency[v] += Mathf.Abs(eigenfunctions[i][v]);
            }
        }
        return NormalizeSaliency(saliency);
    }

    float[] NormalizeSaliency(float[] saliency)
    {
        float maxVal = saliency.Max();
        return saliency.Select(s => s / maxVal).ToArray();
    }

    List<Vector3> SelectSalientPoints(float[] saliency)
    {
        // Select vertices with the highest saliency
        // 선택되는 vertex 수 
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
            marker.GetComponent<Renderer>().material.color = Color.red;

            Destroy(marker.GetComponent<Collider>()); // Optional
        }

        Debug.Log("Salient Points Visualized:");
        foreach (var point in salientPoints)
        {
            Debug.Log(point);
        }
    }
}
