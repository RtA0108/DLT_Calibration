using UnityEngine;
using System.Collections;
using System.Linq; // 정렬(OrderBy)을 위해 추가

public class KangarooTextureAnimator : MonoBehaviour
{
    [Header("Dynamic Mesh Settings")]
    public string targetTag = "something"; // 동적 로드되는 메쉬 태그

    [Header("Resources Load Settings")]
    [Tooltip("Resources 폴더 하위의 경로를 입력하세요. (예: 4D_Textures/Kangaroo)")]
    public string resourcePath = "4D_Textures/Kangaroo";
    public float framesPerSecond = 24f;

    private Texture2D[] frames;
    private Material targetMaterial;
    private float timer = 0f;
    private int currentFrame = 0;
    private bool isReady = false;

    void Start()
    {
        // 1. Resources 폴더에서 텍스처 24장을 이름 순서대로 자동 로드
        LoadTextures();

        // 2. 동적 메쉬(OBJ)가 씬에 나타날 때까지 대기
        StartCoroutine(WaitForDynamicMesh());
    }

    void LoadTextures()
    {
        // 파일 이름(texture_00, 01...) 기준으로 오름차순 정렬하여 배열에 저장
        frames = Resources.LoadAll<Texture2D>(resourcePath).OrderBy(t => t.name).ToArray();

        if (frames.Length == 0)
        {
            Debug.LogError($"[경고] Resources/{resourcePath} 에서 텍스처를 찾을 수 없습니다!");
        }
        else
        {
            Debug.Log($"[성공] {frames.Length}장의 4D 텍스처 시퀀스를 자동으로 로드했습니다.");
        }
    }

    IEnumerator WaitForDynamicMesh()
    {
        GameObject loadedMesh = null;

        while (loadedMesh == null)
        {
            loadedMesh = GameObject.FindWithTag(targetTag);
            if (loadedMesh == null)
            {
                yield return new WaitForSeconds(0.5f);
            }
        }

        Renderer renderer = loadedMesh.GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            targetMaterial = renderer.material;
            isReady = true;
            Debug.Log($"[{targetTag}] 메쉬 포착 완료! 4D 텍스처 렌더링을 시작합니다.");
        }
    }

    void Update()
    {
        if (!isReady || frames == null || frames.Length == 0 || targetMaterial == null) return;

        timer += Time.deltaTime;
        if (timer >= 1f / framesPerSecond)
        {
            timer -= 1f / framesPerSecond;
            currentFrame = (currentFrame + 1) % frames.Length;

            targetMaterial.mainTexture = frames[currentFrame];
        }
    }
}