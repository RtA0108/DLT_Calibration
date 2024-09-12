using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MarkerManager : MonoBehaviour
{
    
    public RectTransform canvasRectTransform; // Canvas�� RectTransform
    public GameObject markerPrefab; // ��Ŀ�� ����� ������

    private int markerCount = 0;
    //Canvas Plane Distance�� object�� Camera�� �Ÿ� �߰������� ����?
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

        // ��Ŀ ����
        GameObject newMarker = Instantiate(markerPrefab, canvasRectTransform);
        newMarker.name = "MarkerUI " + markerCount.ToString();
        // ��Ŀ ����
        Marker markerScript = newMarker.GetComponent<Marker>();
        markerScript.SetMarker(++markerCount, screenPosition, canvasRectTransform);
    }
}