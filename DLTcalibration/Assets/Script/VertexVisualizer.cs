using UnityEngine;
using System.IO;
using System.Globalization;

public class VertexVisualizer : MonoBehaviour
{
    [Header("파일 경로 (Assets 폴더 기준)")]
    [Tooltip("예시: MyFolder/Models/your_model.obj")]
    public string objFilePathFromAssets; // 여기에 파일 경로를 직접 입력합니다.

    [Header("기준 모델")]
    public Transform targetModelTransform;

    [Header("시각화 설정")]
    public float highlightScale = 0.05f;
    public Color highlightColor = Color.red;

    [ContextMenu("OBJ 파일 정점 시각화")]
    private void VisualizeObjVertices()
    {
        if (string.IsNullOrEmpty(objFilePathFromAssets))
        {
            Debug.LogError("OBJ 파일 경로를 입력해주세요!");
            return;
        }
        if (targetModelTransform == null)
        {
            Debug.LogError("기준 모델의 Transform을 연결해주세요!");
            return;
        }

        // Assets 폴더 경로와 사용자가 입력한 경로를 조합
        string fullPath = Path.Combine(Application.dataPath, objFilePathFromAssets);

        if (!File.Exists(fullPath))
        {
            Debug.LogError($"파일을 찾을 수 없습니다: {fullPath}");
            return;
        }

        int index = 0;


        foreach (string line in File.ReadLines(fullPath))
        {
            if (line.StartsWith("v "))
            {
                var p = line.Split(' ');
                float x = float.Parse(p[1], CultureInfo.InvariantCulture);
                float y = float.Parse(p[2], CultureInfo.InvariantCulture);
                float z = float.Parse(p[3], CultureInfo.InvariantCulture);

                // 수동 스케일 계산 제거! 오직 축 변환만 담당합니다.
                Vector3 localPos = new Vector3(-x*150, y*150, z * 150);

                // 모든 변환(위치, 회전, 스케일)은 이 함수가 알아서 처리합니다.
                //Vector3 worldPos = targetModelTransform.TransformPoint(localPos);
                Vector3 worldPos = targetModelTransform.TransformPoint(new Vector3(x, y, z));
                SaliencyUtils.HighlightVertex(localPos, highlightColor, highlightScale, false, index++);
            }
        }
        Debug.Log("OBJ 파일 정점 시각화 완료.");
    }
}