using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    // ▼▼▼ [핵심] 이 부분이 없어서 에러가 난 것입니다! ▼▼▼
    // MainController가 이 변수를 통해 UIManager에게 말을 겁니다.
    public static UIManager Instance;
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    [Header("UI References")]
    public TMP_Dropdown modelDropdown;
    public Toggle calibrationToggle;
    public Toggle saliencyToggle;
    public Toggle recommendToggle;

    [Header("Transform Controls")]
    public Slider scaleSlider;
    public TMP_InputField scaleInput;
    public Slider rotXSlider, rotYSlider, rotZSlider;
    public TMP_InputField rotXInput, rotYInput, rotZInput;

    private List<string> _meshIDs = new List<string>();
    private bool _isUpdatingUI = false;

    // ▼▼▼ [핵심] 게임 시작하자마자 "내가 Instance다"라고 등록 ▼▼▼
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject); // 중복 방지
    }
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    void Start()
    {
        InitializeDropdown();
        InitializeListeners();
    }

    void InitializeDropdown()
    {
        modelDropdown.ClearOptions();
        _meshIDs.Clear();
        List<string> options = new List<string> { "Select Model..." };
        _meshIDs.Add("");
        if (LibraryManager.Instance != null)
        {
            foreach (var entry in LibraryManager.Instance.allMeshEntries)
            {
                options.Add(entry.displayName);
                _meshIDs.Add(entry.id);
            }
        }
        modelDropdown.AddOptions(options);
    }

    void InitializeListeners()
    {
        modelDropdown.onValueChanged.AddListener(OnModelSelected);

        // 토글 리스너
        calibrationToggle.onValueChanged.AddListener((isOn) => {
            if (_isUpdatingUI) return;
            MainController.Instance.ToggleCalibration(isOn);
        });

        saliencyToggle.onValueChanged.AddListener((isOn) => {
            if (_isUpdatingUI) return;
            MainController.Instance.ToggleSaliency(isOn);
        });

        recommendToggle.onValueChanged.AddListener((isOn) => {
            if (_isUpdatingUI) return;
            MainController.Instance.ToggleRecommendation(isOn);
        });

        // 슬라이더 리스너 연결
        SetupTransformListeners();
    }

    // 슬라이더/인풋필드 리스너 세팅
    void SetupTransformListeners()
    {
        // Scale
        scaleSlider.onValueChanged.AddListener((val) => OnSliderChanged(val, scaleInput, Vector3.one));
        scaleInput.onEndEdit.AddListener((str) => OnInputChanged(str, scaleSlider, Vector3.one));

        // Rotation X
        rotXSlider.onValueChanged.AddListener((val) => OnSliderChanged(val, rotXInput, Vector3.right));
        rotXInput.onEndEdit.AddListener((str) => OnInputChanged(str, rotXSlider, Vector3.right));

        // Rotation Y
        rotYSlider.onValueChanged.AddListener((val) => OnSliderChanged(val, rotYInput, Vector3.up));
        rotYInput.onEndEdit.AddListener((str) => OnInputChanged(str, rotYSlider, Vector3.up));

        // Rotation Z
        rotZSlider.onValueChanged.AddListener((val) => OnSliderChanged(val, rotZInput, Vector3.forward));
        rotZInput.onEndEdit.AddListener((str) => OnInputChanged(str, rotZSlider, Vector3.forward));
    }

    // ▼▼▼ MainController가 호출하는 동기화 함수 ▼▼▼
    public void SyncToggles(bool calib, bool saliency, bool recommend)
    {
        _isUpdatingUI = true; // 이벤트 루프 차단

        calibrationToggle.isOn = calib;
        saliencyToggle.isOn = saliency;
        recommendToggle.isOn = recommend;

        _isUpdatingUI = false; // 차단 해제
    }
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    public void OnModelSelected(int index)
    {
        if (index == 0) return;
        RuntimeMeshLoader.Instance.LoadMeshByID(_meshIDs[index]);
        SyncUIValues(_meshIDs[index]);
    }

    // 슬라이더 동작 함수들
    void OnSliderChanged(float value, TMP_InputField targetInput, Vector3 axis)
    {
        if (_isUpdatingUI) return;
        targetInput.SetTextWithoutNotify(value.ToString("F2"));
        ApplyTransform();
    }

    void OnInputChanged(string text, Slider targetSlider, Vector3 axis)
    {
        if (_isUpdatingUI) return;
        if (float.TryParse(text, out float value))
        {
            targetSlider.SetValueWithoutNotify(value);
            ApplyTransform();
        }
    }

    void ApplyTransform()
    {
        if (MainController.Instance == null || MainController.Instance.targetMesh == null) return;

        Transform t = MainController.Instance.targetMesh.transform;

        // Scale
        float s = scaleSlider.value;
        t.localScale = new Vector3(s, s, s);

        // Rotation
        t.localRotation = Quaternion.Euler(rotXSlider.value, rotYSlider.value, rotZSlider.value);
    }

    // 코드에서 정한 스케일(예: 로드 시 자동 맞춤)을 슬라이더/입력칸에 표시만 한다.
    public void SetScaleWithoutNotify(float scale)
    {
        if (scale > scaleSlider.maxValue) scaleSlider.maxValue = scale * 2f;
        if (scale < scaleSlider.minValue) scaleSlider.minValue = scale * 0.5f;
        scaleSlider.SetValueWithoutNotify(scale);
        scaleInput.SetTextWithoutNotify(scale.ToString("F2"));
    }

    void SyncUIValues(string id)
    {
        var entry = LibraryManager.Instance.allMeshEntries.Find(x => x.id == id);
        if (entry != null)
        {
            _isUpdatingUI = true;

            float initScale = (entry.initialScale.x == 0) ? 1f : entry.initialScale.x;
            scaleSlider.value = initScale;
            scaleInput.text = initScale.ToString("F2");

            rotXSlider.value = entry.initialRotation.x;
            rotXInput.text = entry.initialRotation.x.ToString("F2");

            rotYSlider.value = entry.initialRotation.y;
            rotYInput.text = entry.initialRotation.y.ToString("F2");

            rotZSlider.value = entry.initialRotation.z;
            rotZInput.text = entry.initialRotation.z.ToString("F2");

            _isUpdatingUI = false;
        }
    }
}