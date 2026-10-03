using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Globalization;

public class VertexOrderChecker : MonoBehaviour
{
    public string objFilePath = "Assets/Meshes/Chick_Tri.obj";
    public MeshFilter targetMeshFilter;

    void Start()
    {
        Vector3[] unityVertices = targetMeshFilter.sharedMesh.vertices;
        List<Vector3> objVertices = new List<Vector3>();

        foreach (var line in File.ReadLines(objFilePath))
        {
            if (line.StartsWith("v ") && !line.StartsWith("vn") && !line.StartsWith("vt"))
            {
                string[] tokens = line.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length != 4) continue;

                float x = float.Parse(tokens[1], CultureInfo.InvariantCulture);
                float y = float.Parse(tokens[2], CultureInfo.InvariantCulture);
                float z = float.Parse(tokens[3], CultureInfo.InvariantCulture);
                objVertices.Add(new Vector3(x, y, z));
            }
        }

        if (unityVertices.Length != objVertices.Count)
        {
            Debug.LogError($"Vertex count mismatch: Unity mesh has {unityVertices.Length}, OBJ file has {objVertices.Count}");
            return;
        }

        int mismatchCount = 0;
        for (int i = 0; i < unityVertices.Length; i++)
        {
            Vector3 u = unityVertices[i];
            Vector3 o = objVertices[i];

            // local space 기준 비교 (TransformPoint를 쓰면 안 됨)
            if (Vector3.Distance(u, o) > 1e-4f)
            {
                mismatchCount++;
                Debug.LogWarning($"[Mismatch] Index {i}: Unity = {u}, OBJ = {o}");
            }
        }

        if (mismatchCount == 0)
        {
            Debug.Log("Vertex order matches perfectly between Unity and OBJ.");
        }
        else
        {
            Debug.LogWarning($"{mismatchCount} mismatches found in vertex order.");
        }
    }
}