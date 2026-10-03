using System.Collections.Generic;
using UnityEngine; // [중요] Vector3를 쓰려면 이게 꼭 필요합니다!

[System.Serializable]
public class MeshEntry
{
    public string id;           // 고유 ID
    public string displayName;  // 이름
    public bool isBuiltIn;      // 내장 여부

    // ▼▼▼ [여기에 추가] 크기와 회전 변수 ▼▼▼
    public Vector3 initialScale = Vector3.one;      // 기본값 (1,1,1)
    public Vector3 initialRotation = Vector3.zero;  // 기본값 (0,0,0)
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    public string meshPath;     // 모델 경로
    public string cfsDataPath;  // 데이터 경로 1
    public string texDataPath;  // 데이터 경로 2
}

[System.Serializable]
public class MeshLibrary
{
    public List<MeshEntry> entries = new List<MeshEntry>();
}