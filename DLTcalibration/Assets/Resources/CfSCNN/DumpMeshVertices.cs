// Unity C#
using System.IO;
using UnityEngine;

public class DumpMeshVertices : MonoBehaviour
{
    public MeshFilter mf;
    void Start()
    {
        var verts = mf.sharedMesh.vertices;
        using (StreamWriter sw = new StreamWriter("unity_vertices.txt"))
        {
            foreach (var v in verts)
            {
                sw.WriteLine($"{v.x:F7} {v.y:F7} {v.z:F7}");
            }
        }
        Debug.Log($"[dbg] dumped {verts.Length} vertices");
    }
}
