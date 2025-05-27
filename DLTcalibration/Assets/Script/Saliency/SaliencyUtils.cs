using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class SaliencyUtils
{
    //Occlusion Culling
    public static List<Vector3> GetVisibleVertices(
    Camera camera, MeshFilter meshFilter, Dictionary<int, Vector3> vertexPositions, LayerMask visibilityLayerMask)
    {
        List<Vector3> visibleVertices = new List<Vector3>();
        Vector3 camPos = camera.transform.position;
        Dictionary<Vector3, bool> uniquePositions = new Dictionary<Vector3, bool>();

        float dotThreshold = 0.5f; // ← 더 정면을 향한 정점만 raycast 시도
        float hitTolerance = 0.005f; // 거리 기반 (너무 뒤에 있어서 잘 안보이는 vertex 제외)

        foreach (var kvp in vertexPositions)
        {
            int vertexIndex = kvp.Key;
            Vector3 vertexWorldPos = meshFilter.transform.TransformPoint(kvp.Value);

            // 중복 제거용 좌표 정규화
            Vector3 roundedVertex = new Vector3(
                Mathf.Round(vertexWorldPos.x * 1000f) / 1000f,
                Mathf.Round(vertexWorldPos.y * 1000f) / 1000f,
                Mathf.Round(vertexWorldPos.z * 1000f) / 1000f
            );

            Vector3 worldNormal = meshFilter.transform.TransformDirection(meshFilter.mesh.normals[vertexIndex]);
            Vector3 toCamera = (camPos - vertexWorldPos).normalized;

            if (Vector3.Dot(worldNormal, toCamera) <= dotThreshold) continue;

            Ray ray = new Ray(camPos, (vertexWorldPos - camPos).normalized);
            //Mathf.Infinity를 고정된 수치로 변경할지 고민
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, visibilityLayerMask))
            {
                 if (Vector3.Distance(hit.point, vertexWorldPos) < hitTolerance)
                 {
                    if (!uniquePositions.ContainsKey(roundedVertex))
                    {
                        uniquePositions[roundedVertex] = true;
                        visibleVertices.Add(vertexWorldPos);
                    }
                 }
            }
        }

        return visibleVertices;
    }

    //Silhouette 기반 필터 진행중
    public static List<Vector3> FilterVerticesByDepthEdgeMask(List<Vector3> candidates, Camera cam, RenderTexture edgeMask)
    {
        List<Vector3> result = new List<Vector3>();

        Texture2D edgeTex = new Texture2D(edgeMask.width, edgeMask.height, TextureFormat.RGB24, false);
        RenderTexture.active = edgeMask;
        edgeTex.ReadPixels(new Rect(0, 0, edgeMask.width, edgeMask.height), 0, 0);
        edgeTex.Apply();
        RenderTexture.active = null;

        foreach (var worldPos in candidates)
        {
            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
            if (screenPos.z < 0) continue; // 카메라 뒤에 있는 점 제외

            int px = Mathf.RoundToInt(screenPos.x);
            int py = Mathf.RoundToInt(screenPos.y);

            // Unity의 Texture는 (0,0)이 bottom-left 기준이므로 그대로 사용 가능
            if (px >= 0 && px < edgeTex.width && py >= 0 && py < edgeTex.height)
            {
                Color c = edgeTex.GetPixel(px, py);
                if (c.grayscale < 0.5f) // edge 주변이 아닐 때만 통과
                    result.Add(worldPos);
            }
        }

        return result;
    }
    //----------------------------------
    public static List<Vector3> FilterVerticesByEdgeMask(
    List<Vector3> visibleVertices, Camera cam, RenderTexture edgeMask, float threshold = 0.01f)
    {
        Texture2D edgeTex = new Texture2D(edgeMask.width, edgeMask.height, TextureFormat.RGB24, false);
        RenderTexture.active = edgeMask;
        edgeTex.ReadPixels(new Rect(0, 0, edgeMask.width, edgeMask.height), 0, 0);
        edgeTex.Apply();

        List<Vector3> result = new();
        foreach (var v in visibleVertices)
        {
            Vector3 screenPos = cam.WorldToScreenPoint(v);
            if (screenPos.z < 0) continue;

            int x = Mathf.RoundToInt(screenPos.x);
            int y = Mathf.RoundToInt(screenPos.y);
            if (x < 0 || y < 0 || x >= edgeTex.width || y >= edgeTex.height) continue;

            Color pixel = edgeTex.GetPixel(x, y);
            if (pixel.r < threshold) result.Add(v); // 실루엣이 아닌 경우 유지
        }

        return result;
    }
    /// <summary>
    /// 시점 기반 + 메시 기하학 기반 실루엣 정점 제거 필터
    /// </summary>
    public static List<Vector3> FilterVerticesForCalibration(
        Camera cam, MeshFilter meshFilter, List<Vector3> visibleVertices,
        float silhouetteDotThreshold = 0.2f,
        float edgeProximityThreshold = 0.01f)
    {
        List<Vector3> filtered = new List<Vector3>();
        Mesh mesh = meshFilter.sharedMesh;
        Vector3[] meshNormals = mesh.normals;
        Vector3[] meshVertices = mesh.vertices;
        Vector3[] worldVertices = meshVertices.Select(v => meshFilter.transform.TransformPoint(v)).ToArray();

        // [1] dot 기반 필터링 후보 등록
        HashSet<Vector3> dotSilhouetteVertices = new();
        foreach (var v in visibleVertices)
        {
            Vector3 normal = meshFilter.transform.TransformDirection(GetVertexNormalA(meshFilter, v));
            Vector3 viewDir = (cam.transform.position - v).normalized;
            float dot = Mathf.Abs(Vector3.Dot(normal.normalized, viewDir));
            if (dot < silhouetteDotThreshold)
                dotSilhouetteVertices.Add(v);
        }

        // [2] geometry 기반 edge 실루엣 정점 탐색
        HashSet<int> edgeSilhouetteIndices = new();
        int[] triangles = mesh.triangles;
        Vector3[] triangleNormals = new Vector3[triangles.Length / 3];
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 v0 = worldVertices[triangles[i]];
            Vector3 v1 = worldVertices[triangles[i + 1]];
            Vector3 v2 = worldVertices[triangles[i + 2]];
            Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0).normalized;
            triangleNormals[i / 3] = normal;
        }

        Dictionary<(int, int), List<int>> edgeToTriangle = new();
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int tIndex = i / 3;
            int[] tri = { triangles[i], triangles[i + 1], triangles[i + 2] };
            for (int j = 0; j < 3; j++)
            {
                int a = tri[j];
                int b = tri[(j + 1) % 3];
                var edge = (Mathf.Min(a, b), Mathf.Max(a, b));
                if (!edgeToTriangle.ContainsKey(edge)) edgeToTriangle[edge] = new List<int>();
                edgeToTriangle[edge].Add(tIndex);
            }
        }

        Vector3 camPos = cam.transform.position;
        foreach (var kv in edgeToTriangle)
        {
            if (kv.Value.Count != 2) continue;
            int t0 = kv.Value[0];
            int t1 = kv.Value[1];
            Vector3 n0 = triangleNormals[t0];
            Vector3 n1 = triangleNormals[t1];

            int vi0 = kv.Key.Item1;
            int vi1 = kv.Key.Item2;
            Vector3 edgeCenter = (worldVertices[vi0] + worldVertices[vi1]) * 0.5f;
            Vector3 viewDir = (camPos - edgeCenter).normalized;
            float d0 = Vector3.Dot(n0, viewDir);
            float d1 = Vector3.Dot(n1, viewDir);

            if ((d0 > 0 && d1 < 0) || (d0 < 0 && d1 > 0))
            {
                edgeSilhouetteIndices.Add(vi0);
                edgeSilhouetteIndices.Add(vi1);
            }
        }

        HashSet<Vector3> edgeSilhouettePositions = new();
        foreach (int i in edgeSilhouetteIndices)
        {
            edgeSilhouettePositions.Add(worldVertices[i]);
        }

        // [3] 최종 제거
        foreach (var v in visibleVertices)
        {
            bool isDotSilhouette = dotSilhouetteVertices.Contains(v);
            bool isEdgeNear = edgeSilhouettePositions.Any(s => (s - v).sqrMagnitude < edgeProximityThreshold * edgeProximityThreshold);

            if (!isDotSilhouette && !isEdgeNear)
                filtered.Add(v);
        }

        return filtered;
    }

    private static Vector3 GetVertexNormalA(MeshFilter meshFilter, Vector3 vertexWorldPos)
    {
        Vector3[] localVertices = meshFilter.sharedMesh.vertices;
        Vector3[] normals = meshFilter.sharedMesh.normals;

        for (int i = 0; i < localVertices.Length; i++)
        {
            Vector3 worldPos = meshFilter.transform.TransformPoint(localVertices[i]);
            if (Vector3.Distance(worldPos, vertexWorldPos) < 0.0005f)
                return normals[i];
        }

        return Vector3.up; // fallback
    }
    //필터 정리 (없앨 가능성 많음)
    //public static List<Vector3> FilterVerticesForCalibration(
    //    Camera cam, MeshFilter meshFilter, List<Vector3> vertices,
    //    float viewportEdgeMarginRatio = 0.1f // 메시 화면 투영 기준 가장자리 비율
    //)
    //{
    //    List<Vector3> filtered = new List<Vector3>();
    //    Vector3 cameraPos = cam.transform.position;
    //    Vector3 cameraRight = cam.transform.right;

    //    //float minViewDot = 0.2f; - 1에서 쓰지만, 현재 무의미

    //    int total = vertices.Count;
    //    //int edgeRemoved = 0,  userFacingRemoved = 0;//viewDotRemoved = 0,

    //    // 중복 제거된 전체 mesh vertex
    //    Vector3[] allMeshVertices = GetUniqueWorldVertices(meshFilter);

    //    // === [0] Viewport Box Edge Filter ===
    //    //화면 좌표계로 변환
    //    List<Vector3> vpPositions = new List<Vector3>();
    //    foreach (var v in vertices)
    //    {
    //        Vector3 vp = cam.WorldToViewportPoint(v);
    //        vpPositions.Add(vp); 
    //    }

    //    //if (vpPositions.Count == 0)
    //    //{
    //    //    Debug.LogWarning("[Calibration Filter] No visible vertex in Viewport.");
    //    //    return new List<Vector3>();
    //    //}

    //    //mesh가 화면상에 차지하는 box 찾기
    //    float vpMinX = vpPositions.Min(vp => vp.x);
    //    float vpMaxX = vpPositions.Max(vp => vp.x);
    //    float vpMinY = vpPositions.Min(vp => vp.y);
    //    float vpMaxY = vpPositions.Max(vp => vp.y);
    //    //box의 Margin 계산
    //    float marginX = (vpMaxX - vpMinX) * viewportEdgeMarginRatio;
    //    float marginY = (vpMaxY - vpMinY) * viewportEdgeMarginRatio;

    //    List<Vector3> prefiltered = new List<Vector3>();
    //    foreach (var v in vertices)
    //    {
    //        Vector3 vp = cam.WorldToViewportPoint(v);
    //        if (vp.z < 0f) continue;

    //        bool isEdge =
    //            vp.x < vpMinX + marginX || vp.x > vpMaxX - marginX ||
    //            vp.y < vpMinY + marginY || vp.y > vpMaxY - marginY;

    //        if (isEdge)
    //        {
    //            //edgeRemoved++;
    //            //HighlightVertex(v, new Color(1f, 0.4f, 1f), 6f, false); // 핑크색
    //            continue;
    //        }

    //        prefiltered.Add(v);
    //    }

    //    foreach (Vector3 v in prefiltered)
    //    {
    //        Vector3 worldNormal = meshFilter.transform.TransformDirection(GetVertexNormal(meshFilter, v)).normalized;
    //        Vector3 toCamera = (cameraPos - v).normalized;

    //        //float sideViewAmount = Mathf.Abs(Vector3.Dot(toCamera, cameraRight));

    //        // === [1] ViewDot Filter -> 현재는 occlusion에서 조건이 강력하여 큰 의미 없음.
    //        //float dot = Vector3.Dot(worldNormal, toCamera);
    //        //if (dot < minViewDot)
    //        //{
    //        //    viewDotRemoved++;
    //        //    //Debug.Log($"[ViewDot Removed] Dot={dot:F3}, Pos={v}");

    //        //    continue;
    //        //}

    //        // === [2] UserNormal (시점 무관)
    //        //카메라 공간에서 normal vector 변환
    //        Vector3 camSpaceNormal = cam.transform.InverseTransformDirection(worldNormal);
    //        //float minYNorm = -0.5f;
    //        //float maxYNorm = 1.0f;
    //        //float userNormalXMaxAbs = 0.9f;

    //        //if (camSpaceNormal.y < minYNorm || camSpaceNormal.y > maxYNorm || Mathf.Abs(camSpaceNormal.x) > userNormalXMaxAbs)
    //        // 카메라 좌표계 기준으로 재판단
    //        if (Mathf.Abs(camSpaceNormal.y) > 0.9f || Mathf.Abs(camSpaceNormal.x) > 0.9f)
    //        {
    //            //userFacingRemoved++;

    //            continue;
    //        }


    //        filtered.Add(v);
    //    }


    //    return filtered;
    //}
    // 분산 + saliency score 비율 변경하며 vertex 추천
    public static List<Vector3> SelectHybridDistributedVertices(List<Vector3> candidates, Dictionary<Vector3, float> saliencyMap, float l, int selectionCount, float alpha = 0.5f)
    {
        List<Vector3> selected = new List<Vector3>();

        if (candidates.Count <= selectionCount)
            return new List<Vector3>(candidates);

        // --- Step 1: 가장 높은 엔트로피를 가진 정점으로 첫 정점 선택 ---
        Vector3 first = candidates.OrderByDescending(v => saliencyMap[v]).First();
        selected.Add(first);
        candidates.Remove(first);

        // --- Step 2: Hybrid 분산 선택 ---
        while (selected.Count < selectionCount)
        {
            Vector3 bestVertex = Vector3.zero;
            float bestScore = float.MinValue;

            foreach (var candidate in candidates)
            {
                float minDist = selected.Min(s => Vector3.Distance(candidate, s));
                // 하이브리드 점수 (alpha 조절) -> alpha가 높을수록 saliency에 대한 영향 증가
                float score = alpha * saliencyMap[candidate] + (1 - alpha) * (minDist / l);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestVertex = candidate;
                }
            }

            if (bestVertex != Vector3.zero)
            {
                selected.Add(bestVertex);
                candidates.Remove(bestVertex);
            }
            else break;
        }

        return selected;
    }

    public static void HighlightVertex(Vector3 position, Color color, float scale, bool isSaliencyHighlight, int index = -1)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.position = position;
        sphere.transform.localScale = Vector3.one * scale;
        if (isSaliencyHighlight)
            sphere.name = $"HighVertex_{index}";

        Renderer r = sphere.GetComponent<Renderer>();
        if (r != null)
        {
            r.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            r.material.color = color;
        }
    }
    //중복 없애기 (수정 필요성? -> 추후 확인 예정)
    public static Vector3[] GetUniqueWorldVertices(MeshFilter meshFilter, int precision = 1000)
    {
        return meshFilter.mesh.vertices
            .Select(v => meshFilter.transform.TransformPoint(v))
            .GroupBy(v => new Vector3(
                Mathf.Round(v.x * precision) / precision,
                Mathf.Round(v.y * precision) / precision,
                Mathf.Round(v.z * precision) / precision
            ))
            .Select(g => g.First())
            .ToArray();
    }
   
    // 필터에서 사용중
    private static Vector3 GetVertexNormal(MeshFilter meshFilter, Vector3 vertexPosition)
    {
        Vector3[] positions = meshFilter.mesh.vertices;
        Vector3[] normals = meshFilter.mesh.normals;

        for (int i = 0; i < positions.Length; i++)
        {
            if (Vector3.Distance(meshFilter.transform.TransformPoint(positions[i]), vertexPosition) < 0.001f)
                return normals[i];
        }

        return Vector3.zero;
    }

    //----------------------------------neighbor 탐색 수정 중-----------------------------------------------

    /// <summary>
    /// 중복 위치를 가진 vertex index들을 포함하여 topology neighbor 확장 (depth 제한 적용)
    /// </summary>
    public static HashSet<int> FindTopologyNeighbors(int startIndex, Dictionary<int, HashSet<int>> adjacency, int maxDepth, Mesh mesh, Transform transform)
    {
        Queue<(int, int)> queue = new Queue<(int, int)>();
        HashSet<int> visited = new HashSet<int> { startIndex };
        queue.Enqueue((startIndex, 0));

        while (queue.Count > 0)
        {
            var (current, depth) = queue.Dequeue();
            if (depth >= maxDepth) continue;

            foreach (int neighbor in adjacency[current])
            {
                if (visited.Add(neighbor))
                    queue.Enqueue((neighbor, depth + 1));
            }
        }

        // 중복 위치 정점 보정 포함
        Dictionary<Vector3, List<int>> positionMap = GetOrBuildPositionToIndicesMap(mesh, transform);
        HashSet<int> expanded = new HashSet<int>(visited);
        foreach (int idx in visited)
        {
            Vector3 wp = transform.TransformPoint(mesh.vertices[idx]);
            if (positionMap.TryGetValue(wp, out var dupList))
            {
                foreach (var dupIdx in dupList)
                    expanded.Add(dupIdx);
            }
        }

        expanded.Remove(startIndex);
        return expanded;
    }


    /// <summary>
    /// Euclidean 거리 기반 이웃 탐색 (반경 내 정점 반환)
    /// </summary>
    public static HashSet<int> FindEuclideanNeighbors(Vector3[] worldPositions, int centerIndex, float radius)
    {
        HashSet<int> neighbors = new();
        Vector3 center = worldPositions[centerIndex];
        float rSqr = radius * radius;

        for (int i = 0; i < worldPositions.Length; i++)
        {
            if (i == centerIndex) continue;
            if ((worldPositions[i] - center).sqrMagnitude <= rSqr)
                neighbors.Add(i);
        }
        return neighbors;
    }

    /// <summary>
    /// Geodesic 기반 이웃 탐색 (Dijkstra 방식, 거리 제한) -> 아직은 미사용
    /// </summary>
    public static HashSet<int> FindGeodesicNeighbors(int startIndex, Mesh mesh, Transform transform, float maxDistance)
    {
        Vector3[] vertices = mesh.vertices;
        Dictionary<int, HashSet<int>> adjacency = BuildAdjacency(mesh);
        Vector3[] worldPositions = vertices.Select(v => transform.TransformPoint(v)).ToArray();

        HashSet<int> result = new();
        Dictionary<int, float> dist = new() { [startIndex] = 0f };
        PriorityQueue<int> queue = new();
        queue.Enqueue(startIndex, 0f);

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            float currentDist = dist[current];

            if (current != startIndex)
                result.Add(current);

            if (adjacency.TryGetValue(current, out var neighbors))
            {
                foreach (int neighbor in neighbors)
                {
                    float edgeLength = (worldPositions[current] - worldPositions[neighbor]).magnitude;
                    float newDist = currentDist + edgeLength;

                    if (newDist <= maxDistance && (!dist.ContainsKey(neighbor) || newDist < dist[neighbor]))
                    {
                        dist[neighbor] = newDist;
                        queue.Enqueue(neighbor, newDist);
                    }
                }
            }
        }

        return result;
    }

    // 기존 adjacency 생성 함수 그대로 유지
    public static Dictionary<int, HashSet<int>> BuildAdjacency(Mesh mesh)
    {
        var adjacency = new Dictionary<int, HashSet<int>>();
        int[] triangles = mesh.triangles;

        for (int i = 0; i < triangles.Length; i += 3)
        {
            int v0 = triangles[i];
            int v1 = triangles[i + 1];
            int v2 = triangles[i + 2];
            AddEdge(adjacency, v0, v1);
            AddEdge(adjacency, v1, v2);
            AddEdge(adjacency, v2, v0);
        }
        return adjacency;
    }

    private static void AddEdge(Dictionary<int, HashSet<int>> adj, int a, int b)
    {
        if (!adj.ContainsKey(a)) adj[a] = new HashSet<int>();
        if (!adj.ContainsKey(b)) adj[b] = new HashSet<int>();
        adj[a].Add(b);
        adj[b].Add(a);
    }




    //기존에 수정중이던 neighbor 탐색 방법들 (사용 여부 확인 예정)

    private static Dictionary<Mesh, Dictionary<int, HashSet<int>>> _adjacencyCache = new();
    private static Dictionary<Mesh, Dictionary<Vector3, List<int>>> _positionMapCache = new();

    public static Dictionary<int, HashSet<int>> GetOrBuildAdjacency(Mesh mesh)
    {
        if (_adjacencyCache.TryGetValue(mesh, out var cached))
            return cached;

        var built = BuildAdjacency(mesh);
        _adjacencyCache[mesh] = built;
        return built;
    }

   

    /// <summary>
    /// worldPos에 가장 가까운 정점 index 반환
    /// </summary>
    public static int FindNearestVertexIndex(Vector3 worldPos, Vector3[] worldVertices)
    {
        float minDist = float.MaxValue;
        int nearestIndex = 0;
        for (int i = 0; i < worldVertices.Length; i++)
        {
            float dist = (worldVertices[i] - worldPos).sqrMagnitude;
            if (dist < minDist)
            {
                minDist = dist;
                nearestIndex = i;
            }
        }
        return nearestIndex;
    }

    /// <summary>
    /// 동일 world position을 공유하는 vertex index 리스트 생성
    /// </summary>
    public static Dictionary<Vector3, List<int>> GetOrBuildPositionToIndicesMap(Mesh mesh, Transform transform)
    {
        if (_positionMapCache.TryGetValue(mesh, out var cached))
            return cached;

        var dict = new Dictionary<Vector3, List<int>>();
        Vector3[] verts = mesh.vertices;
        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 worldPos = transform.TransformPoint(verts[i]);
            if (!dict.TryGetValue(worldPos, out var list))
            {
                list = new List<int>();
                dict[worldPos] = list;
            }
            list.Add(i);
        }

        _positionMapCache[mesh] = dict;
        return dict;
    }
    public static Dictionary<Vector3, float> NormalizeSaliencyMap(Dictionary<Vector3, float> saliencyMap)
    {
        Dictionary<Vector3, float> normalized = new();
        float min = saliencyMap.Values.Min();
        float max = saliencyMap.Values.Max();
        foreach (var kv in saliencyMap)
        {
            float t = Mathf.Clamp01((kv.Value - min) / (max - min));
            normalized[kv.Key] = t;
        }
        return normalized;
    }

    public static Dictionary<Vector3, float> GroupSaliencyByRoundedPosition(Dictionary<Vector3, float> saliencyMap, int precision = 1000)
    {
        return saliencyMap
            .GroupBy(kv => new Vector3(
                Mathf.Round(kv.Key.x * precision) / precision,
                Mathf.Round(kv.Key.y * precision) / precision,
                Mathf.Round(kv.Key.z * precision) / precision))
            .ToDictionary(g => g.Key, g => g.First().Value);
    }
}
public class PriorityQueue<T>
{
    private readonly SortedDictionary<float, Queue<T>> _dict = new();

    public void Enqueue(T item, float priority)
    {
        if (!_dict.TryGetValue(priority, out var queue))
        {
            queue = new Queue<T>();
            _dict[priority] = queue;
        }
        queue.Enqueue(item);
    }

    public T Dequeue()
    {
        var first = _dict.First();
        var item = first.Value.Dequeue();
        if (first.Value.Count == 0)
            _dict.Remove(first.Key);
        return item;
    }

    public int Count => _dict.Sum(p => p.Value.Count);
}

//public static List<Vector3> FilterVerticesForCalibration(
//    Camera cam, MeshFilter meshFilter, List<Vector3> vertices,
//    float sigma,
//    float viewportEdgeMarginRatio = 0.1f // 메시 화면 투영 기준 가장자리 비율
//)
//{
//    List<Vector3> filtered = new List<Vector3>();
//    Vector3 cameraPos = cam.transform.position;
//    Vector3 cameraRight = cam.transform.right;

//    float minViewDot = 0.2f;
//    //float maxSideViewAmount = 0.25f;

//    int total = vertices.Count;
//    int edgeRemoved = 0, viewDotRemoved = 0, userFacingRemoved = 0;//, sideViewRemoved = 0, accessibilityRemoved = 0;

//    // 중복 제거된 전체 mesh vertex
//    Vector3[] allMeshVertices = GetUniqueWorldVertices(meshFilter);

//    // === [0] Viewport Box Edge Filter ===
//    List<Vector3> vpPositions = vertices
//        .Select(v => cam.WorldToViewportPoint(v))
//        .Where(vp => vp.z > 0f)
//        .ToList();

//    if (vpPositions.Count == 0)
//    {
//        Debug.LogWarning("[Calibration Filter] No visible vertex in Viewport.");
//        return new List<Vector3>();
//    }

//    float vpMinX = vpPositions.Min(vp => vp.x);
//    float vpMaxX = vpPositions.Max(vp => vp.x);
//    float vpMinY = vpPositions.Min(vp => vp.y);
//    float vpMaxY = vpPositions.Max(vp => vp.y);
//    float marginX = (vpMaxX - vpMinX) * viewportEdgeMarginRatio;
//    float marginY = (vpMaxY - vpMinY) * viewportEdgeMarginRatio;

//    List<Vector3> prefiltered = new List<Vector3>();
//    foreach (var v in vertices)
//    {
//        Vector3 vp = cam.WorldToViewportPoint(v);
//        if (vp.z < 0f) continue;

//        bool isEdge =
//            vp.x < vpMinX + marginX || vp.x > vpMaxX - marginX ||
//            vp.y < vpMinY + marginY || vp.y > vpMaxY - marginY;

//        if (isEdge)
//        {
//            edgeRemoved++;
//            //HighlightVertex(v, new Color(1f, 0.4f, 1f), 6f, false); // 핑크색
//            continue;
//        }

//        prefiltered.Add(v);
//    }

//    foreach (Vector3 v in prefiltered)
//    {
//        Vector3 worldNormal = meshFilter.transform.TransformDirection(GetVertexNormal(meshFilter, v)).normalized;
//        Vector3 toCamera = (cameraPos - v).normalized;
//        float dot = Vector3.Dot(worldNormal, toCamera);
//        float sideViewAmount = Mathf.Abs(Vector3.Dot(toCamera, cameraRight));

//        // === [1] ViewDot Filter
//        if (dot < minViewDot)
//        {
//            viewDotRemoved++;
//            Debug.Log($"[ViewDot Removed] Dot={dot:F3}, Pos={v}");

//            continue;
//        }

//        // === [2] UserNormal (시점 무관)
//        Vector3 camSpaceNormal = cam.transform.InverseTransformDirection(worldNormal);
//        float minYNorm = -0.5f;
//        float maxYNorm = 1.0f;
//        float userNormalXMaxAbs = 0.9f;

//        if (camSpaceNormal.y < minYNorm || camSpaceNormal.y > maxYNorm ||
//            Mathf.Abs(camSpaceNormal.x) > userNormalXMaxAbs)
//        {
//            userFacingRemoved++;

//            continue;
//        }

//        // === [3] Accessibility Filter (시점 무관)
//        //float normalVertical = Mathf.Abs(worldNormal.y);
//        //float steepnessThreshold = 0.98f;
//        //float normalToCameraDot = Vector3.Dot(worldNormal, toCamera);

//        //if (normalVertical < (1f - steepnessThreshold)) // y < 0.02
//        //{
//        //    accessibilityRemoved++;

//        //    Debug.Log($"[Accessibility Removed - Steep Normal] y={normalVertical:F2}, Pos={v}");
//        //    continue;
//        //}

//        //if (normalToCameraDot < 0.1f)
//        //{
//        //    accessibilityRemoved++;
//        //    Debug.Log($"[Accessibility Removed - Facing Away] Dot={normalToCameraDot:F2}, Pos={v}");
//        //    continue;
//        //}

//        //List<Vector3> neighbors = GetNeighborsByEuclideanDistance(v, sigma, allMeshVertices);
//        //if (neighbors.Count > 0)
//        //{
//        //    float normalVariance = neighbors.Sum(n =>
//        //    {
//        //        Vector3 neighborNormal = meshFilter.transform.TransformDirection(GetVertexNormal(meshFilter, n)).normalized;
//        //        return (1f - Vector3.Dot(worldNormal, neighborNormal));
//        //    }) / neighbors.Count;

//        //    if (normalVariance > 0.6f)
//        //    {
//        //        accessibilityRemoved++;
//        //        HighlightVertex(v, new Color(1f, 0.4f, 1f), 6f, false);
//        //        Debug.Log($"[Accessibility Removed - High Curvature] Var={normalVariance:F2}, Pos={v}");
//        //        continue;
//        //    }
//        //}

//        // === [4] SideView (주석 상태 유지)
//        /*
//        if (sideViewAmount > maxSideViewAmount)
//        {
//            sideViewRemoved++;
//            continue;
//        }
//        */

//        filtered.Add(v);
//    }

//    Debug.Log($"[Calibration Filter]");
//    Debug.Log($"Total input: {total}");
//    Debug.Log($"Edge removed: {edgeRemoved}");
//    Debug.Log($"ViewDot removed: {viewDotRemoved}");
//    Debug.Log($"UserNormal removed: {userFacingRemoved}");
//    // Debug.Log($"Accessibility removed: {accessibilityRemoved}");
//    // Debug.Log($"SideView removed: {sideViewRemoved}");
//    Debug.Log($"Filtered kept: {filtered.Count}");

//    return filtered;
//}