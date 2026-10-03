using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
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
    private bool dragging;

    // 패치: 마커 주변 모양(선 또는 텍스처). 중심이 대응점이고, 패치 어디를 잡아도 드래그된다.
    // 패치들은 마커들보다 아래 층(PatchLayer)에 모아 둔다. 패치끼리 겹쳐도 점과 번호가 가려지지 않게.
    private RawImage patchImage;
    private Texture2D linesPatch, texturePatch;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    // 캔버스가 카메라에 붙어 있어서(Screen Space - Camera), 캘리브레이션으로 카메라가 바뀌면
    // 그대로 둔 마커도 화면에서 위치가 바뀐다. 드래그 중이 아니면 항상 저장된 프로젝터 좌표에 다시 놓는다.
    void LateUpdate()
    {
        if (dragging || owner == null || canvasRect == null) return;
        if (ScreenToCanvas(owner.verticesStruct[IDX].screenCoordinate, out Vector2 local))
        {
            rectTransform.localPosition = new Vector3(local.x, local.y, 0f);
            SyncPatchPosition();
        }
    }

    void OnDestroy()
    {
        if (patchImage != null) Destroy(patchImage.gameObject);
        if (linesPatch != null) Destroy(linesPatch);
        if (texturePatch != null) Destroy(texturePatch);
    }

    public void SetPatches(Texture2D lines, Texture2D texture, PatchSnapshot.Mode mode, int size)
    {
        // 다시 호출되면(캘리브레이션 후 패치 갱신) 이전 텍스처는 정리
        if (linesPatch != null && linesPatch != lines) Destroy(linesPatch);
        if (texturePatch != null && texturePatch != texture) Destroy(texturePatch);
        linesPatch = lines;
        texturePatch = texture;

        if (patchImage == null)
        {
            var go = new GameObject($"Patch {IDX + 1}", typeof(RectTransform), typeof(RawImage), typeof(PatchDragForwarder));
            go.transform.SetParent(GetPatchLayer(), false);
            go.GetComponent<PatchDragForwarder>().marker = this;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            patchImage = go.GetComponent<RawImage>();
            SyncPatchPosition();

            // 패치 가운데를 가리지 않도록 빨간 점은 작게, 번호는 왼쪽 위 모서리로
            // (번호는 원래 빨간 점 위의 검정 글씨라, 검은 배경에서도 보이게 흰색으로)
            rectTransform.sizeDelta = new Vector2(5f, 5f);
            markerText.rectTransform.anchoredPosition = new Vector2(-size / 2f + 6f, size / 2f - 6f);
            markerText.color = Color.white;
        }
        SetPatchMode(mode);
    }

    // Canvas의 맨 첫 자식(= 가장 먼저 그려짐)으로 패치 전용 층을 둔다.
    private Transform GetPatchLayer()
    {
        Transform layer = canvasRect.Find("PatchLayer");
        if (layer == null)
        {
            var go = new GameObject("PatchLayer", typeof(RectTransform));
            layer = go.transform;
            layer.SetParent(canvasRect, false);
            var rt = (RectTransform)layer;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
        }
        layer.SetAsFirstSibling();
        return layer;
    }

    private void SyncPatchPosition()
    {
        if (patchImage != null) patchImage.transform.position = rectTransform.position;
    }

    public void SetPatchMode(PatchSnapshot.Mode mode)
    {
        if (patchImage != null) patchImage.texture = mode == PatchSnapshot.Mode.Lines ? linesPatch : texturePatch;
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
        dragging = true;
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
        SyncPatchPosition();

        // 마커가 실제로 그려지는 프로젝터 픽셀 좌표 (좌하단 원점)
        Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(projectCam, rectTransform.position);
        owner.verticesStruct[IDX].screenCoordinate = screenPosition;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        dragging = false;
        var v = owner.verticesStruct[IDX];
        float distance = Vector2.Distance(v.screenCoordinateGT, v.screenCoordinate);
        Debug.Log($"[Marker {IDX + 1}] {v.screenCoordinate} (GT에서 {distance:F2}px 이동)");
        owner.OnMarkerDragEnd(IDX);
    }

    private bool ScreenToCanvas(Vector2 screenPosition, out Vector2 local)
    {
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, projectCam, out local);
    }
}
