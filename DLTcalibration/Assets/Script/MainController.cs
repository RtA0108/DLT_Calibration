using UnityEngine;
using System.IO;

public class MainController : MonoBehaviour
{
    public static MainController Instance;

    [Header("Helpers")]
    public CreateSphereAtVertex sphereGenerator;

    [Header("Target Info")]
    public GameObject targetMesh;

    [SerializeField] private string _currentObjPath;
    [SerializeField] private string _currentCfsPath; // 사용자가 입력한 CfS 경로 (없을 수 있음)
    [SerializeField] private string _currentTexPath; // 텍스처 이미지 경로

    // 자동 생성된 결과 파일 경로들을 저장할 변수
    [SerializeField] private string _generatedCfsPath;
    [SerializeField] private string _generatedTexSaliencyPath;

    [Header("Target Components")]
    public ProjectionMappingCalibrator currentCalibrator;
    public SaliencyMapVisualizer currentVisualizer;

    // 상태 변수
    public bool IsCalibrationActive = false;
    public bool IsSaliencyActive = false;
    public bool IsRecommendationActive = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (UIManager.Instance != null) UIManager.Instance.SyncToggles(false, false, false);
    }

    void Update()
    {
        if (currentCalibrator == null || currentVisualizer == null) return;

        if (Input.GetKeyDown(KeyCode.Alpha1)) ToggleCalibration();
        if (Input.GetKeyDown(KeyCode.Alpha2)) ToggleSaliency();
        if (Input.GetKeyDown(KeyCode.Alpha3)) ToggleRecommendation();

        // (비상용) F5키로 수동 계산 가능
        if (Input.GetKeyDown(KeyCode.F5)) StartCoroutine(ManualRunSaliency());
    }

    // ==================================================================================
    // ★ [핵심] 메쉬 등록 & 자동 계산 & 초기화 통합 함수
    // ==================================================================================
    // RegisterNewMesh 함수 부분만 교체하세요

    //public void RegisterNewMesh(GameObject newMesh, string objPath, string inputCfsPath, string texPath)
    //{
    //    // =========================================================
    //    // ★ [긴급 수정] 리소스 경로 -> 실제 절대 경로로 변환
    //    // =========================================================
    //    // 텍스처 경로가 있고, 절대 경로(C:\...)가 아니라면 리소스 경로로 간주
    //    if (!string.IsNullOrEmpty(texPath) && !Path.IsPathRooted(texPath))
    //    {
    //        // 1. Resources 폴더의 실제 위치 찾기
    //        string resourcesPath = Path.Combine(Application.dataPath, "Resources");

    //        // 2. 텍스처 확장자 찾기 (jpg, png 등 시도)
    //        string fullPath = Path.Combine(resourcesPath, texPath);

    //        if (File.Exists(fullPath + ".jpg")) texPath = fullPath + ".jpg";
    //        else if (File.Exists(fullPath + ".png")) texPath = fullPath + ".png";
    //        else if (File.Exists(fullPath + ".jpeg")) texPath = fullPath + ".jpeg";
    //        else if (File.Exists(fullPath + ".tga")) texPath = fullPath + ".tga";

    //        Debug.Log($"[Path Fix] 리소스 경로 변환됨: {texPath}");
    //    }
    //    // 0. CCTV
    //    Debug.LogWarning("================ [RegisterNewMesh 진단] ================");
    //    Debug.Log($"1. Obj: {objPath}");
    //    Debug.Log($"2. Tex: {texPath}");

    //    targetMesh = newMesh;
    //    _currentObjPath = objPath;
    //    _currentCfsPath = inputCfsPath;
    //    _currentTexPath = texPath;

    //    string meshDir = Path.GetDirectoryName(objPath);
    //    string meshName = Path.GetFileNameWithoutExtension(objPath);

    //    // ★ [수정 1] 원래 이름 규칙인 "_saliency.txt"로 원상 복구!
    //    string expectedCfsFile = Path.Combine(meshDir, $"{meshName}_saliency.txt");

    //    // TexSaliency 규칙
    //    string expectedTexFile = Path.Combine(meshDir, $"{meshName}_vertex_saliency.txt");

    //    // 파일 존재 여부 확인
    //    bool cfsExists = File.Exists(expectedCfsFile);
    //    bool texExists = File.Exists(expectedTexFile);
    //    Debug.Log($"3. 파일 확인 -> CfS({cfsExists}): {Path.GetFileName(expectedCfsFile)}");
    //    Debug.Log($"            -> Tex({texExists}): {Path.GetFileName(expectedTexFile)}");

    //    // 계산 필요 여부 (입력 경로도 없고, 예상 파일도 없으면 계산)
    //    //bool needCfsCalc = string.IsNullOrEmpty(inputCfsPath) && !cfsExists;
    //    //bool needTexCalc = !string.IsNullOrEmpty(texPath) && !texExists;
    //    bool needCfsCalc = !cfsExists;
    //    bool needTexCalc = !texExists;
    //    // -----------------------------------------------------------
    //    currentCalibrator = newMesh.GetComponent<ProjectionMappingCalibrator>();
    //    currentVisualizer = newMesh.GetComponent<SaliencyMapVisualizer>();

    //    if (needCfsCalc || needTexCalc)
    //    {
    //        Debug.LogWarning("4. 자동 계산 시작...");
    //        // 계산 실행
    //        PythonProcessManager.Instance.RunSaliencyCalculation(objPath, texPath, meshDir);
    //    }
    //    else
    //    {
    //        Debug.Log("4. 계산 건너뜀 (이미 파일이 있거나 조건 불충족)");
    //    }

    //    // -----------------------------------------------------------
    //    // 5. 최종 경로 할당 (파일이 진짜 생겼는지 다시 체크)

    //    if (File.Exists(expectedCfsFile)) _generatedCfsPath = expectedCfsFile;
    //    else _generatedCfsPath = inputCfsPath; // 없으면 사용자 입력값 유지 (없으면 빈칸)

    //    if (File.Exists(expectedTexFile)) _generatedTexSaliencyPath = expectedTexFile;
    //    else _generatedTexSaliencyPath = "";

    //    // 실패 시 에러 로그
    //    if (needCfsCalc && !File.Exists(expectedCfsFile))
    //        Debug.LogError($"[MainController] X CfS 파일 생성 실패! ({expectedCfsFile})");

    //    if (needTexCalc && !File.Exists(expectedTexFile))
    //        Debug.LogError($"[MainController] X TexSaliency 파일 생성 실패! ({expectedTexFile})");

    //    // -----------------------------------------------------------
    //    // 6. Calibrator 설정
    //    if (currentCalibrator != null)
    //    {
    //        // 이제 원래 이름대로 들어가므로 Calibrator도 정상 작동할 것입니다.
    //        currentCalibrator.SetPaths(objPath, _generatedCfsPath, _generatedTexSaliencyPath);

    //        if (File.Exists(_generatedTexSaliencyPath))
    //            currentCalibrator.saliencyMode = SaliencyMode.TexMesh;
    //        else
    //            currentCalibrator.saliencyMode = SaliencyMode.CFSCNN;
    //    }

    //    // 7. 구체 생성
    //    if (sphereGenerator != null) sphereGenerator.GenerateSpheres(newMesh);

    //    // 8. 초기화
    //    IsCalibrationActive = false; IsSaliencyActive = false; IsRecommendationActive = false;
    //    ApplyStates();

    //    if (currentCalibrator != null) currentCalibrator.Init();

    //    Debug.Log($"[RegisterNewMesh] 완료. (CfS: {Path.GetFileName(_generatedCfsPath)})");
    //}
    public void RegisterNewMesh(GameObject newMesh, string objPath, string inputCfsPath, string texPath)
    {
        // =========================================================
        // ★ [긴급 수정] 리소스 경로 -> 실제 절대 경로로 변환
        // =========================================================
        // 텍스처 경로가 있고, 절대 경로(C:\...)가 아니라면 리소스 경로로 간주
        if (!string.IsNullOrEmpty(texPath) && !Path.IsPathRooted(texPath))
        {
            // 1. Resources 폴더의 실제 위치 찾기
            string resPath = Path.Combine(Application.dataPath, "Resources");

            // 2. 텍스처 확장자 찾기 (jpg, png 등 시도)
            string fullPath = Path.Combine(resPath, texPath);

            if (File.Exists(fullPath + ".jpg")) texPath = fullPath + ".jpg";
            else if (File.Exists(fullPath + ".png")) texPath = fullPath + ".png";
            else if (File.Exists(fullPath + ".jpeg")) texPath = fullPath + ".jpeg";
            else if (File.Exists(fullPath + ".tga")) texPath = fullPath + ".tga";

            Debug.Log($"[Path Fix] 리소스 경로 변환됨: {texPath}");
        }

        // 0. CCTV
        Debug.LogWarning("================ [RegisterNewMesh 진단] ================");
        Debug.Log($"1. Obj: {objPath}");
        Debug.Log($"2. Tex: {texPath}");

        targetMesh = newMesh;
        _currentObjPath = objPath;
        _currentCfsPath = inputCfsPath;
        _currentTexPath = texPath;

        string meshDir = Path.GetDirectoryName(objPath);
        string meshName = Path.GetFileNameWithoutExtension(objPath);

        // =========================================================
        // ★ [경로 분리] Result_CfS 와 Result_Tex 전용 폴더 설정
        // =========================================================
        string resourcesPath = Path.Combine(Application.dataPath, "Resources");

        // 새 결과 폴더 경로 지정
        string cfsOutDir = Path.Combine(resourcesPath, "Result_CfS");
        string texOutDir = Path.Combine(resourcesPath, "Result_Tex");

        // 폴더가 없으면 자동으로 생성
        if (!Directory.Exists(cfsOutDir)) Directory.CreateDirectory(cfsOutDir);
        if (!Directory.Exists(texOutDir)) Directory.CreateDirectory(texOutDir);

        // 예상되는 결과 파일의 최종 절대 경로
        string expectedCfsFile = Path.Combine(cfsOutDir, $"{meshName}_saliency.txt");
        string expectedTexFile = Path.Combine(texOutDir, $"{meshName}_vertex_saliency.txt");
        // =========================================================

        // 파일 존재 여부 확인
        bool cfsExists = File.Exists(expectedCfsFile);
        bool texExists = File.Exists(expectedTexFile);
        Debug.Log($"3. 파일 확인 -> CfS({cfsExists}): {Path.GetFileName(expectedCfsFile)}");
        Debug.Log($"            -> Tex({texExists}): {Path.GetFileName(expectedTexFile)}");

        // 계산 필요 여부 (무조건 결과 파일이 없으면 계산!)
        bool needCfsCalc = !cfsExists;
        bool needTexCalc = !texExists;

        // -----------------------------------------------------------
        currentCalibrator = newMesh.GetComponent<ProjectionMappingCalibrator>();
        currentVisualizer = newMesh.GetComponent<SaliencyMapVisualizer>();

        if (needCfsCalc || needTexCalc)
        {
            Debug.LogWarning($"4. 자동 계산 시작... (CfS 필요: {needCfsCalc}, Tex 필요: {needTexCalc})");
            // ★ [수정됨] 매니저에게 아웃풋 폴더를 따로따로 2개 넘겨줍니다.
            PythonProcessManager.Instance.RunSaliencyCalculation(objPath, texPath, cfsOutDir, texOutDir);
        }
        else
        {
            Debug.Log("4. 계산 건너뜀 (이미 Result_CfS와 Result_Tex 폴더에 파일이 존재함)");
        }

        // -----------------------------------------------------------
        // 5. 최종 경로 할당 (파일이 진짜 생겼는지 다시 체크)

        if (File.Exists(expectedCfsFile)) _generatedCfsPath = expectedCfsFile;
        else _generatedCfsPath = inputCfsPath; // 없으면 사용자 입력값 유지 (없으면 빈칸)

        if (File.Exists(expectedTexFile)) _generatedTexSaliencyPath = expectedTexFile;
        else _generatedTexSaliencyPath = "";

        // 실패 시 에러 로그
        if (needCfsCalc && !File.Exists(expectedCfsFile))
            Debug.LogError($"[MainController] CfS 파일 생성 실패! ({expectedCfsFile})");

        if (needTexCalc && !File.Exists(expectedTexFile))
            Debug.LogError($"[MainController] TexSaliency 파일 생성 실패! ({expectedTexFile})");

        // -----------------------------------------------------------
        // 6. Calibrator 설정
        if (currentCalibrator != null)
        {
            // 이제 원래 이름대로 들어가므로 Calibrator도 정상 작동할 것입니다.
            currentCalibrator.SetPaths(objPath, _generatedCfsPath, _generatedTexSaliencyPath);

            if (File.Exists(_generatedTexSaliencyPath))
                currentCalibrator.saliencyMode = SaliencyMode.TexMesh;
            else
                currentCalibrator.saliencyMode = SaliencyMode.CFSCNN;
        }
        // Visualizer에게도 방금 만들어진 파일 주소를 정확히 넘겨줍니다!
        if (currentVisualizer != null)
        {
            currentVisualizer.cfsObjPath = objPath;
            currentVisualizer.cfsTxtPath = _generatedCfsPath;
            currentVisualizer.texObjPath = objPath;
            currentVisualizer.texTxtPath = _generatedTexSaliencyPath;
        }
        // 7. 구체 생성
        if (sphereGenerator != null) sphereGenerator.GenerateSpheres(newMesh);

        // 8. 초기화
        IsCalibrationActive = false; IsSaliencyActive = false; IsRecommendationActive = false;
        ApplyStates();

        if (currentCalibrator != null) currentCalibrator.Init();

        Debug.Log($"[RegisterNewMesh] 완료. (CfS: {Path.GetFileName(_generatedCfsPath)})");
    }

    // (수동 실행용)
    private System.Collections.IEnumerator ManualRunSaliency()
    {
        if (string.IsNullOrEmpty(_currentObjPath)) yield break;

        // =========================================================
        // ★ [경로 분리] 수동 계산 시에도 똑같이 폴더 2개로 나눕니다.
        // =========================================================
        string resourcesPath = Path.Combine(Application.dataPath, "Resources");
        string cfsOutDir = Path.Combine(resourcesPath, "Result_CfS");
        string texOutDir = Path.Combine(resourcesPath, "Result_Tex");

        if (!Directory.Exists(cfsOutDir)) Directory.CreateDirectory(cfsOutDir);
        if (!Directory.Exists(texOutDir)) Directory.CreateDirectory(texOutDir);

        // ★ [수정됨] 이제 3개가 아니라 4개의 인자(obj, tex, cfs폴더, tex폴더)를 정확히 넘겨줍니다!
        bool success = PythonProcessManager.Instance.RunSaliencyCalculation(_currentObjPath, _currentTexPath, cfsOutDir, texOutDir);

        if (success && currentCalibrator != null)
        {
            // 경로 갱신 및 재로딩
            currentCalibrator.Init();
            currentCalibrator.Run();
            Debug.Log("[MainController] 수동 계산 및 갱신 완료.");
        }
        yield return null;
    }

    // ==================================================================================
    // 토글 함수들
    // ==================================================================================
    public void ToggleCalibration() { ToggleCalibration(!IsCalibrationActive); }
    public void ToggleCalibration(bool isOn) { IsCalibrationActive = isOn; ApplyStates(); }

    public void ToggleSaliency() { ToggleSaliency(!IsSaliencyActive); }
    public void ToggleSaliency(bool isOn) { IsSaliencyActive = isOn; ApplyStates(); }

    public void ToggleRecommendation() { ToggleRecommendation(!IsRecommendationActive); }
    public void ToggleRecommendation(bool isOn) { IsRecommendationActive = isOn; ApplyStates(); }

    private void ApplyStates()
    {
        if (currentCalibrator != null)
        {
            currentCalibrator.enabled = IsCalibrationActive;
            currentCalibrator.visualized = IsRecommendationActive;

            if (IsCalibrationActive) currentCalibrator.Run();
            else currentCalibrator.ClearMarkers();
        }

        if (currentVisualizer != null)
        {
            currentVisualizer.enabled = IsSaliencyActive;
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.SyncToggles(IsCalibrationActive, IsSaliencyActive, IsRecommendationActive);
        }
    }
}