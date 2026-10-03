using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

// 프로젝터 화면(CanvasUI)에 뜨는 대응점 마커.
// 드래그한 위치를 프로젝터 카메라의 스크린 픽셀 좌표로 VertexClickTest에 기록한다.
public class Marker : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public TextMeshProUGUI markerText;

    public int IDX;            // VertexClickTest 슬롯 인덱스
    public Camera projectCam;  // 이 마커를 그리는 카메라 (CanvasUI의 worldCamera)

    private RectTransform rectTransform;
    private RectTransform canvasRect;
    private VertexClickTest owner;
    private Vector2 dragOffset;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    public void SetMarker(int slot, Vector2 screenPosition, RectTransform canvasRectTransform, Camera cam, VertexClickTest clickTest)
    {
        IDX = slot;
        canvasRect = canvasRectTransform;
        projectCam = cam;
        owner = clickTest;

        markerText.text = (slot + 1).ToString();

        // 스크린 좌표 -> Canvas 로컬 좌표. Screen Space - Camera 캔버스라서 카메라를 넘겨야 한다.
        // (예전에는 null 카메라로 변환한 뒤 마커가 아니라 Canvas 자체를 옮기고 있었음)
        if (ScreenToCanvas(screenPosition, out Vector2 local))
            rectTransform.localPosition = new Vector3(local.x, local.y, 0f);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // 마커를 잡은 지점과 마커 중심의 차이를 기억해서, 드래그 시작 때 마커가 튀지 않게 한다.
        if (ScreenToCanvas(eventData.position, out Vector2 local))
            dragOffset = (Vector2)rectTransform.localPosition - local;
    }

    public void OnDrag(PointerEventData eventData)
    {
        // delta를 더하는 대신 포인터 위치를 직접 변환한다. 캘리브레이션 후 카메라에
        // 커스텀 투영 행렬이 들어가면 캔버스 1단위가 1픽셀이 아닐 수 있기 때문.
        if (!ScreenToCanvas(eventData.position, out Vector2 local)) return;

        Vector2 p = local + dragOffset;
        rectTransform.localPosition = new Vector3(p.x, p.y, 0f);

        // 마커가 실제로 그려지는 프로젝터 픽셀 좌표 (좌하단 원점)
        Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(projectCam, rectTransform.position);
        owner.verticesStruct[IDX].screenCoordinate = screenPosition;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        var v = owner.verticesStruct[IDX];
        float distance = Vector2.Distance(v.screenCoordinateGT, v.screenCoordinate);
        Debug.Log($"[Marker {IDX + 1}] {v.screenCoordinate} (GT에서 {distance:F2}px 이동)");
    }

    private bool ScreenToCanvas(Vector2 screenPosition, out Vector2 local)
    {
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, projectCam, out local);
    }
}
