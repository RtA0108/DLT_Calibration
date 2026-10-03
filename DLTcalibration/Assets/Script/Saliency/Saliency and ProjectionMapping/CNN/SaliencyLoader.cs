using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// CFSCNN saliency를 Resources에서 불러와 static saliencyMap으로 저장하는 간단한 버전
/// </summary>
public class SaliencyLoader : MonoBehaviour
{
    public string fileName = "CfSCNN/Chick_Tri_saliency";
    public MeshFilter targetMeshFilter;

    public static Dictionary<Vector3, float> saliencyMap;

    void Awake()
    {
        TextAsset txt = Resources.Load<TextAsset>(fileName);
        if (txt == null)
        {
            Debug.LogError($"[SaliencyLoader] {fileName} 파일을 찾을 수 없습니다.");
            return;
        }

        float[] scores = txt.text
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => float.Parse(s))
            .ToArray();

        SetSaliencyMap(targetMeshFilter, scores);
    }

    public static void SetSaliencyMap(MeshFilter meshFilter, float[] scores)
    {
        Mesh mesh = meshFilter.sharedMesh;
        Vector3[] vertices = mesh.vertices;

        if (scores.Length != vertices.Length)
        {
            Debug.LogError($"[SaliencyLoader] 점수 개수 {scores.Length} ≠ 정점 수 {vertices.Length}");
            return;
        }

        saliencyMap = new Dictionary<Vector3, float>();

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 world = meshFilter.transform.TransformPoint(vertices[i]);
            Vector3 rounded = new Vector3(
                Mathf.Round(world.x * 1000f) / 1000f,
                Mathf.Round(world.y * 1000f) / 1000f,
                Mathf.Round(world.z * 1000f) / 1000f
            );

            if (!saliencyMap.ContainsKey(rounded))
                saliencyMap[rounded] = scores[i];
        }

        Debug.Log($"[SaliencyLoader] saliencyMap 저장 완료 (Count = {saliencyMap.Count})");
    }
}