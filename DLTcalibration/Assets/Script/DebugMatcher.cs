using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

public class DebugMatcher : MonoBehaviour
{
    [Header("기준 모델")]
    public Transform targetModelTransform;

    [Header("파일 경로 (Assets 폴더 기준)")]
    public string objFilePathFromAssets;

    [Header("매칭 설정")]
    public float matchThreshold = 0.001f;
    // Update() 대신 Start()를 사용
    void Start()
    {
        // 테스트를 바로 실행하는 대신, 코루틴을 시작시킵니다.
        StartCoroutine(RunTestAfterDelay());
    }

    IEnumerator RunTestAfterDelay()
    {
        Debug.Log("<color=orange>플레이 시작! Calibrator가 계산을 마칠 때까지 1초간 기다립니다...</color>");

        // 1초 동안 기다립니다.
        yield return new WaitForSeconds(1.0f);

        Debug.Log("<color=lime>1초 지연 완료! 이제 테스트를 실행합니다.</color>");
        TestFilteredMatching();
    }
    private void TestFilteredMatching()
    {
        // Calibrator가 계산한 filtered 리스트를 가져옵니다.
        List<Vector3> filteredWorldVertices = ProjectionMappingCalibrator.publicFilteredVertices;

        if (filteredWorldVertices == null || filteredWorldVertices.Count == 0)
        {
            Debug.LogError("Calibrator의 filtered 리스트를 찾을 수 없습니다. Calibrator를 먼저 실행해주세요.");
            return;
        }

        // .obj 파일에서 변환된 로컬 정점 목록을 읽어옵니다.
        var objLocalVertices = LoadObjVertices();
        if (objLocalVertices.Count == 0) return;

        // --- 1. 월드 공간에서 비교 ---
        Debug.Log("--- 1. 월드 공간(World Space) 비교 테스트 ---");
        int worldMatchCount = 0;
        List<Vector3> availableFilteredWorld = new List<Vector3>(filteredWorldVertices);

        foreach (var objLocalVert in objLocalVertices)
        {
            Vector3 objWorldVert = targetModelTransform.TransformPoint(objLocalVert);
            Vector3 closestFiltered = Vector3.zero;
            float minDistance = float.MaxValue;

            if (availableFilteredWorld.Count == 0) break;

            foreach (var filteredWorld in availableFilteredWorld)
            {
                float d = Vector3.Distance(objWorldVert, filteredWorld);
                if (d < minDistance)
                {
                    minDistance = d;
                    closestFiltered = filteredWorld;
                }
            }
            if (minDistance < matchThreshold)
            {
                worldMatchCount++;
                availableFilteredWorld.Remove(closestFiltered);
            }
        }
        float worldMatchRate = (float)worldMatchCount / filteredWorldVertices.Count * 100f;
        Debug.Log($"<b><color=yellow>월드 공간 매칭률: {worldMatchRate:F2}% ({worldMatchCount}/{filteredWorldVertices.Count})</color></b>");


        // --- 2. 로컬 공간에서 비교 ---
        Debug.Log("--- 2. 로컬 공간(Local Space) 비교 테스트 ---");
        List<Vector3> filteredLocalVertices = filteredWorldVertices.Select(v => targetModelTransform.InverseTransformPoint(v)).ToList();
        int localMatchCount = 0;
        List<Vector3> availableFilteredLocal = new List<Vector3>(filteredLocalVertices);

        foreach (var objLocalVert in objLocalVertices)
        {
            Vector3 closestFiltered = Vector3.zero;
            float minDistance = float.MaxValue;

            if (availableFilteredLocal.Count == 0) break;

            foreach (var filteredLocal in availableFilteredLocal)
            {
                float d = Vector3.Distance(objLocalVert, filteredLocal);
                if (d < minDistance)
                {
                    minDistance = d;
                    closestFiltered = filteredLocal;
                }
            }
            if (minDistance < matchThreshold)
            {
                localMatchCount++;
                availableFilteredLocal.Remove(closestFiltered);
            }
        }
        float localMatchRate = (float)localMatchCount / filteredLocalVertices.Count * 100f;
        Debug.Log($"<b><color=cyan>로컬 공간 매칭률: {localMatchRate:F2}% ({localMatchCount}/{filteredLocalVertices.Count})</color></b>");

        // --- 최종 결론 ---
        if (localMatchRate > worldMatchRate)
        {
            Debug.LogWarning("결론: 로컬 공간에서의 매칭률이 더 높습니다. 문제는 로컬 좌표 변환에 있습니다.");
        }
        else
        {
            Debug.LogWarning("결론: 월드 공간에서의 매칭률이 더 높거나 같습니다. 문제는 월드 변환(TransformPoint) 과정에 있을 수 있습니다.");
        }
    }

    // .obj 파일 로드 헬퍼 함수
    private List<Vector3> LoadObjVertices()
    {
        string fullPath = Path.Combine(Application.dataPath, objFilePathFromAssets);
        var vertices = new List<Vector3>();
        if (!File.Exists(fullPath))
        {
            Debug.LogError($"파일을 찾을 수 없습니다: {fullPath}");
            return vertices;
        }
        foreach (string line in File.ReadLines(fullPath))
        {
            if (line.StartsWith("v "))
            {
                var p = line.Split(' ');
                float x = float.Parse(p[1], CultureInfo.InvariantCulture);
                float y = float.Parse(p[2], CultureInfo.InvariantCulture);
                float z = float.Parse(p[3], CultureInfo.InvariantCulture);
                vertices.Add(new Vector3(-x, y, z)); //여기서 변환 공식 테스트
            }
        }
        return vertices;
    }
}