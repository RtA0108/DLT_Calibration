using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class Marker : MonoBehaviour, IDragHandler
{
    public TextMeshProUGUI markerText;
    private RectTransform rectTransform;
    private GameObject canvas;
    
    public int IDX;
    public Camera projectCam;
    private GameObject LVManger;
    void Awake()
    {
        canvas = GameObject.FindGameObjectWithTag("Canvas");
        if (canvas == null) Debug.LogError("No Canvas Here Fuck");

        rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null) Debug.LogError("No Rect Here Fuck");

        Debug.Log(canvas);


        Camera cam = GameObject.FindGameObjectWithTag("Project Camera").gameObject.GetComponent<Camera>();
        projectCam = cam;
        LVManger = GameObject.Find("LevelManager");
        //rectTransform.pivot = new Vector2(0.5f, 0.5f);
        //rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        //rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
    }

    public void SetMarker(int index, Vector2 screenPosition, RectTransform canvasRectTransform)
    {
        if (canvasRectTransform == null)
        {
            Debug.LogError("canvasRectTransform is null");
            return;
        }
        // ���� ����
        markerText.text = index.ToString();
        IDX = index-1;
        // ��ũ�� ��ǥ�� Canvas ��ǥ�� ��ȯ
        Vector2 canvasPosition;
        Debug.Log("screen Position: " + screenPosition + "canvas Rect Transform: " +  canvasRectTransform);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRectTransform, screenPosition, null, out canvasPosition);
        Debug.Log("canvas Position: " + canvasPosition);
        // ��Ŀ ��ġ ����
        canvasRectTransform.anchoredPosition = canvasPosition;
        Debug.Log("Marker Creat" + canvasPosition);
    }

    public void OnDrag(PointerEventData eventData)
    {
        rectTransform.anchoredPosition += eventData.delta;


        Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(projectCam, rectTransform.position);
        //���߿� ���� �´��� �𸣰���
        //LVManger.GetComponent<VertexClickTest>().verticesStruct[IDX].screenCoordinate = new Vector2(screenPosition.x, projectCam.pixelHeight - screenPosition.y);
        LVManger.GetComponent<VertexClickTest>().verticesStruct[IDX].screenCoordinate = new Vector2(screenPosition.x, screenPosition.y);
        //���콺 ��ġ�� �ƴ� ��Ŀ ��ġ�� �����ν�
        Debug.Log("screen Coord: " + IDX + " " + LVManger.GetComponent<VertexClickTest>().verticesStruct[IDX].screenCoordinate);
        
    }
}