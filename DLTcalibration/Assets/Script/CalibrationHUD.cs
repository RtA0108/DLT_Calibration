using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 조작 화면(Display 1)의 보조 UI. 실행할 때 Canvas 아래에 만든다.
//  - 오른쪽 패널: 추천점 개수 [-] n [+]
//  - 왼쪽 아래: 키로 켜고 끄는 기능들의 현재 상태 + 버튼 (버튼과 키는 똑같이 동작)
//  - 도움말 창 (H 키 또는 버튼)
// 한글 표시를 위해 Windows 기본 글꼴(맑은 고딕)을 실행 시 불러온다. 없으면 Arial로 표시된다.
public class CalibrationHUD : MonoBehaviour
{
    [Header("References (비워 두면 자동으로 찾음)")]
    public RectTransform controlPanel;   // 오른쪽 기존 패널
    public VertexClickTest clickTest;
    public DLT_solve dltSolver;

    [Header("Layout")]
    public float countRowY = -190f;      // 오른쪽 패널 안에서 개수 줄의 세로 위치
    public float statusPanelWidth = 230f;
    public int fontSize = 12;

    private const float RowHeight = 20f, RowGap = 3f;
    private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.392f); // 기존 오른쪽 패널과 같은 색
    private static readonly Color ButtonColor = new Color(1f, 1f, 1f, 0.15f);

    private Font font;
    private Text countText;
    private GameObject helpPanel, statusPanel;
    private readonly List<(Text label, Func<string> text)> liveLabels = new List<(Text, Func<string>)>();

    private const string HelpText =
        "<b>사용 순서</b>\n" +
        "1. 오른쪽 목록에서 모델 선택 (프로젝터 화면에 맞게 크기가 자동 조정됨)\n" +
        "2. <b>1</b> 캘리브레이션 모드 켜기 → <b>3</b> 추천점(빨간 구) 표시\n" +
        "3. <b>- / =</b> 추천점 개수 조절 (6~20) → <b>R</b> 추천점을 대응점으로 선택\n" +
        "     (Display 1에서 버텍스 구를 직접 클릭해 고르거나 해제할 수도 있음)\n" +
        "4. 프로젝터 화면에서 각 마커(패치)를 실물의 같은 위치로 드래그\n" +
        "     <b>V</b> 정렬 보기: 모델 없이 마커와 패치만 투사\n" +
        "     <b>T</b> 패치 전환: 선(외곽선·모서리) ↔ 텍스처\n" +
        "5. 마커를 6개 이상 옮기면, 놓을 때마다 옮긴 마커들로 자동 보정됨\n" +
        "     <b>L</b> 자동 보정 켜기/끄기, <b>F</b> 선택된 점 전부로 보정 계산\n" +
        "\n" +
        "<b>기타</b>\n" +
        "<b>2</b> 히트맵 (추천 근거인 saliency 표시)      <b>P</b> 4D 텍스처 재생/정지\n" +
        "<b>F5</b> saliency 다시 계산      <b>H</b> 이 도움말 열기/닫기\n" +
        "\n" +
        "보정 후 프로젝터 위치·줌·키스톤이나 모델 크기·회전을 바꾸면 다시 보정해야 합니다.\n" +
        "(창을 클릭하면 닫힙니다)";

    void Start()
    {
        font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, fontSize);
        if (controlPanel == null) controlPanel = transform.Find("ControlPanel") as RectTransform;
        if (clickTest == null) clickTest = FindObjectOfType<VertexClickTest>();
        if (dltSolver == null) dltSolver = FindObjectOfType<DLT_solve>();

        if (controlPanel != null) BuildCountRow();
        BuildStatusPanel();
        BuildHelpPanel();
    }

    void Update()
    {
        if (!HotkeyGuard.Blocked && Input.GetKeyDown(KeyCode.H)) ToggleHelp();

        if (countText != null && MainController.Instance != null)
            SetIfChanged(countText, MainController.Instance.recommendedVertexCount.ToString());
        foreach (var (label, text) in liveLabels) SetIfChanged(label, text());
    }

    public void ToggleHelp()
    {
        helpPanel.SetActive(!helpPanel.activeSelf);
        statusPanel.SetActive(!helpPanel.activeSelf); // 도움말과 겹치지 않게
    }

    // 같은 글자를 매 프레임 다시 넣으면 UI 레이아웃을 매번 다시 계산하므로, 바뀔 때만 넣는다.
    private static void SetIfChanged(Text label, string value)
    {
        if (label.text != value) label.text = value;
    }

    // ---------------------------------------------------------------- 오른쪽: 추천점 개수
    private void BuildCountRow()
    {
        NewText(controlPanel, "추천점 개수", TextAnchor.MiddleLeft, new Vector2(-30f, countRowY), new Vector2(84f, RowHeight));
        NewButton(controlPanel, "-", new Vector2(20f, countRowY), new Vector2(18f, 18f), () => ChangeCount(-1));
        countText = NewText(controlPanel, "6", TextAnchor.MiddleCenter, new Vector2(42f, countRowY), new Vector2(24f, RowHeight));
        NewButton(controlPanel, "+", new Vector2(64f, countRowY), new Vector2(18f, 18f), () => ChangeCount(+1));
    }

    private static void ChangeCount(int delta)
    {
        MainController main = MainController.Instance;
        if (main != null) main.SetRecommendedVertexCount(main.recommendedVertexCount + delta);
    }

    // ---------------------------------------------------------------- 왼쪽 아래: 기능 상태 + 버튼
    private void BuildStatusPanel()
    {
        var rows = new List<(Func<string> text, UnityAction action)>
        {
            (() => "추천점을 대응점으로 선택 (R)", () => clickTest.SelectRecommendedVertices()),
            (() => $"정렬 보기 (V): {(clickTest.AlignmentView ? "켜짐" : "꺼짐")}", () => clickTest.ToggleAlignmentView()),
            (() => $"패치 (T): {(clickTest.patchMode == PatchSnapshot.Mode.Lines ? "선" : "텍스처")}", () => clickTest.TogglePatchMode()),
            (() => $"자동 보정 (L): {(clickTest.liveSolve ? "켜짐" : "꺼짐")}", () => clickTest.ToggleLiveSolve()),
            (() => "보정 계산 (F)", () => dltSolver.PerformDLT()),
            (() => Animator4D == null || !Animator4D.HasSequence ? "4D 텍스처: 없음"
                                                                : $"4D 텍스처 (P): {(Animator4D.playing ? "재생 중" : "정지")}",
             () => { if (Animator4D != null) Animator4D.TogglePlaying(); }),
            (() => "도움말 (H)", ToggleHelp),
        };

        float height = 8f + RowHeight + (rows.Count + 1) * (RowHeight + RowGap) + 6f;
        RectTransform panel = NewRect("CalibrationStatusPanel", transform, Vector2.zero, Vector2.zero, new Vector2(8f, 8f), new Vector2(statusPanelWidth, height));
        panel.gameObject.AddComponent<Image>().color = PanelColor;
        statusPanel = panel.gameObject;

        float y = -8f - RowHeight / 2f;
        Text title = NewText(panel, "<b>캘리브레이션</b>", TextAnchor.MiddleCenter, Vector2.zero, new Vector2(statusPanelWidth - 12f, RowHeight), topAnchored: true, y: y);
        title.fontSize = fontSize + 2;

        foreach (var (text, action) in rows)
        {
            y -= RowHeight + RowGap;
            Button button = NewButton(panel, text(), Vector2.zero, new Vector2(statusPanelWidth - 12f, RowHeight), action, topAnchored: true, y: y);
            Text label = button.GetComponentInChildren<Text>();
            label.alignment = TextAnchor.MiddleLeft;
            label.rectTransform.offsetMin = new Vector2(6f, 0f);
            liveLabels.Add((label, text));
        }

        y -= RowHeight + RowGap;
        Text status = NewText(panel, "", TextAnchor.MiddleLeft, Vector2.zero, new Vector2(statusPanelWidth - 18f, RowHeight), topAnchored: true, y: y);
        liveLabels.Add((status, () =>
        {
            MainController main = MainController.Instance;
            if (main == null || main.currentCalibrator == null) return "모델을 먼저 선택하세요";
            if (main.IsSaliencyPending)
                return $"saliency: {main.CurrentSaliencyJob.Stage}... {main.CurrentSaliencyJob.ElapsedSeconds:F0}초";
            if (!main.IsCalibrationActive) return "캘리브레이션 모드 꺼짐 (1 키)";
            return $"맞춘 대응점: {clickTest.PlacedCount()} / {clickTest.SelectedCount()}개";
        }));
    }

    private static TextureSequenceAnimator Animator4D => TextureSequenceAnimator.Instance;

    // ---------------------------------------------------------------- 도움말 창
    private void BuildHelpPanel()
    {
        RectTransform panel = NewRect("HelpPanel", transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(540f, 370f));
        panel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.96f);
        panel.gameObject.AddComponent<Button>().onClick.AddListener(ToggleHelp); // 아무 곳이나 클릭하면 닫힘

        Text text = NewText(panel, HelpText, TextAnchor.UpperLeft, Vector2.zero, Vector2.zero);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(16f, 12f);
        text.rectTransform.offsetMax = new Vector2(-16f, -12f);
        text.fontSize = fontSize + 1;
        text.lineSpacing = 1.15f;

        helpPanel = panel.gameObject;
        helpPanel.SetActive(false);
    }

    // ---------------------------------------------------------------- UI 생성 도우미
    private RectTransform NewRect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    // topAnchored = 부모 위쪽 가운데 기준으로 y만큼 내려서 배치 (세로로 줄을 쌓을 때)
    private Text NewText(Transform parent, string content, TextAnchor align, Vector2 pos, Vector2 size, bool topAnchored = false, float y = 0f)
    {
        RectTransform rt = topAnchored
            ? NewRect("Text", parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), size)
            : NewRect("Text", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
        var text = rt.gameObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = align;
        text.supportRichText = true;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.text = content;
        text.raycastTarget = false;
        return text;
    }

    private Button NewButton(Transform parent, string label, Vector2 pos, Vector2 size, UnityAction onClick, bool topAnchored = false, float y = 0f)
    {
        RectTransform rt = topAnchored
            ? NewRect("Button", parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), size)
            : NewRect("Button", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
        rt.gameObject.AddComponent<Image>().color = ButtonColor;
        var button = rt.gameObject.AddComponent<Button>();
        button.onClick.AddListener(onClick);

        Text text = NewText(rt, label, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.sizeDelta = Vector2.zero;
        return button;
    }
}
