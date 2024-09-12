using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MarkerManager : MonoBehaviour
{
    
    public RectTransform canvasRectTransform; // Canvas의 RectTransform
    public GameObject markerPrefab; // 마커로 사용할 프리팹

    private int markerCount = 0;
    //Canvas Plane Distance를 object와 Camera의 거리 중간값으로 설정?
    public void Start()
    {
        if (canvasRectTransform == null) Debug.LogError("No Rect Transform");
    }

    public void CreateMarker(Vector2 screenPosition)
    {
        if (markerPrefab == null)
        {
            Debug.LogError("Marker prefab is not assigned.");
            return;
        }

        if (canvasRectTransform == null) Debug.LogError("No Rect Transform");

        // 마커 생성
        GameObject newMarker = Instantiate(markerPrefab, canvasRectTransform);
        newMarker.name = "MarkerUI " + markerCount.ToString();
        // 마커 설정
        Marker markerScript = newMarker.GetComponent<Marker>();
        markerScript.SetMarker(++markerCount, screenPosition, canvasRectTransform);
    }
}