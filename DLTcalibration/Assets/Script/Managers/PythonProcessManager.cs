using UnityEngine;
using System.Diagnostics;
using System.IO;
using System;

public class PythonProcessManager : MonoBehaviour
{
    public static PythonProcessManager Instance;

    [Header("Local Settings (CfS-CNN)")]
    public string localPythonPath = "python";
    public string localScriptName = "bridge_cfs.py";

    // WSL Conda Python 경로
    private const string WSL_PYTHON_PATH = "/home/minsu/miniconda3/envs/textured_saliency/bin/python";
    // WSL 실행 스크립트 경로
    private const string WSL_SCRIPT_PATH = "/home/minsu/TexMeshSaliency/bridge_tex.py";

    [Header("Common")]
    // 프로젝트 루트 기준 폴더명 (Assets 폴더와 같은 레벨에 있는 폴더)
    public string scriptDirectory = "PythonScripts";

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // =========================================================
    // ★ [수정 완료] 인자 4개를 받아서 각자의 도우미 함수로 정확히 분배합니다.
    // =========================================================

    public bool RunSaliencyCalculation(string objPath, string texPath, string cfsOutDir, string texOutDir)
    {
        UnityEngine.Debug.Log("[PythonProcessManager] Saliency 계산 프로세스 시작 (경로 분리 적용)");

        // 1. CfS-CNN (Local Windows)
        bool resultLocal = RunLocalProcess(objPath, cfsOutDir);

        // =========================================================
        // ★ [수정됨] 텍스처 경로가 없어도 파이썬이 알아서 찾게끔 무조건 실행!
        // =========================================================
        // 만약 빈칸으로 오면 파이썬에게 "네가 알아서 찾아(AUTO_FIND)"라고 던져줍니다.
        string safeTexPath = string.IsNullOrEmpty(texPath) ? "AUTO_FIND" : texPath;

        bool resultWsl = RunWslProcess(objPath, safeTexPath, texOutDir);

        return resultLocal && resultWsl;
    }
    // ---------------------------------------------------------
    // 1. 윈도우 로컬 프로세스 실행 (CfS-CNN)
    // ---------------------------------------------------------
    private bool RunLocalProcess(string meshPath, string outputDir)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string scriptPath = Path.Combine(projectRoot, scriptDirectory, localScriptName);

        if (!File.Exists(scriptPath))
        {
            UnityEngine.Debug.LogError($"[Local Error] 스크립트 파일이 없습니다! 경로를 확인하세요: {scriptPath}");
            return false;
        }

        ProcessStartInfo start = new ProcessStartInfo();
        start.FileName = localPythonPath;
        // 인자: "스크립트경로" "메쉬경로" "출력폴더"
        start.Arguments = $"\"{scriptPath}\" \"{meshPath}\" \"{outputDir}\"";

        start.UseShellExecute = false;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        start.CreateNoWindow = true;

        return ExecuteProcess(start, "Local(CfS)");
    }

    // ---------------------------------------------------------
    // 2. WSL 프로세스 실행 (TexSaliency)
    // ---------------------------------------------------------
    private bool RunWslProcess(string meshPath, string texturePath, string outputDir)
    {
        // ★ [핵심] 윈도우 경로를 WSL 경로(/mnt/c/...)로 완벽하게 변환!
        string wslMesh = ConvertToWslPath(meshPath);
        string wslTex = ConvertToWslPath(texturePath);
        string wslOut = ConvertToWslPath(outputDir);

        ProcessStartInfo start = new ProcessStartInfo();
        start.FileName = "wsl";

        // 명령어 구조: wsl [파이썬경로] [스크립트경로] --mesh [메쉬] --tex [텍스처] --out [출력]
        start.Arguments = $"{WSL_PYTHON_PATH} \"{WSL_SCRIPT_PATH}\" --mesh \"{wslMesh}\" --tex \"{wslTex}\" --out \"{wslOut}\"";

        start.UseShellExecute = false;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        start.CreateNoWindow = true;

        return ExecuteProcess(start, "WSL(Tex)");
    }

    // ---------------------------------------------------------
    // 공통 실행기 및 경로 변환기 (기존 코드 완벽 유지)
    // ---------------------------------------------------------
    private bool ExecuteProcess(ProcessStartInfo startInfo, string label)
    {
        UnityEngine.Debug.Log($"[{label}] 명령어 실행:\n{startInfo.FileName} {startInfo.Arguments}");

        try
        {
            using (Process process = Process.Start(startInfo))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrEmpty(output))
                    UnityEngine.Debug.Log($"[{label} Output]\n{output}");

                if (!string.IsNullOrEmpty(error))
                    UnityEngine.Debug.LogError($"[{label} Error / StdErr]\n{error}");

                if (process.ExitCode != 0)
                {
                    UnityEngine.Debug.LogError($"[{label}] 프로세스 비정상 종료 (ExitCode: {process.ExitCode})");
                    return false;
                }

                return true;
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"[{label}] 실행 실패 (Exception): {e.Message}");
            return false;
        }
    }

    private string ConvertToWslPath(string windowsPath)
    {
        if (string.IsNullOrEmpty(windowsPath)) return "";
        string path = windowsPath.Replace("\\", "/");
        if (path.Length > 1 && path[1] == ':')
        {
            char driveLetter = char.ToLower(path[0]);
            path = $"/mnt/{driveLetter}{path.Substring(2)}";
        }
        return path;
    }
}