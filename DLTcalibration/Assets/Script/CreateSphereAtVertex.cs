using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CreateSphereAtVertex : MonoBehaviour
{
    //public GameObject cube; // Assign the cube GameObject in the Inspector
    // GameObject는 다른 게임 오브젝트를 엑세스 하기 위해 필요한듯 (이 경우는 vertex를 표시하기 위한 구의 prefap 저장)
    public GameObject vertexSphere;
    // 기존 회전 정보 저장 | Quaternion은 (x,y,z,w)로 표현
    private Quaternion originalRotation;
    // vertex 개수 저장
    public int vertexCount;
    public int indexNumber;
    public Dictionary<int, Vector3> posIndex;
    void Start()
    {
        // Example: Create a sphere at vertex index 0 of the cube
        //something 태그가 있는 모든 개체 찾기
        GameObject[] objects = GameObject.FindGameObjectsWithTag("something");
        indexNumber = 0;
        // Ensure the cube GameObject is assigned
        if (objects != null)
        { 
            foreach(GameObject obj in objects) 
            {
                // Get the mesh filter of the cube
                // MeshFilter 내부엔 vertex와 uv정보가 모두 들어있음
                MeshFilter cubeMeshFilter = obj.GetComponent<MeshFilter>();
                posIndex = new Dictionary<int, Vector3>();
                Dictionary<Vector2, int> uvIndex = new Dictionary<Vector2, int>();
                // Ensure the mesh filter is not null and has a mesh
                if (cubeMeshFilter != null && cubeMeshFilter.sharedMesh != null)
                {
                    // Get the cube's mesh
                    Mesh cubeMesh = cubeMeshFilter.sharedMesh;
                    
                    vertexCount = cubeMesh.vertices.Length;
                    for(int i = 0; i<cubeMesh.vertices.Length; i++) {
                        // Get the world position of the specified vertex
                        Vector3 vertexPosition = obj.transform.TransformPoint(cubeMesh.vertices[i]);
                        if(posIndex.FirstOrDefault(x => x.Value == vertexPosition).Key != 0){
                            
                            continue;
                        }
                        else{
                            indexNumber++;
                            posIndex.Add(indexNumber, vertexPosition);
                            
                        }
                        
                        Vector2 uvCoordinate = cubeMesh.uv[i];
                        uvIndex.Add(uvCoordinate, indexNumber);


                        //vertex가 가시적으로 
                        Instantiate(vertexSphere, vertexPosition, Quaternion.identity);
                        vertexSphere.transform.localScale = new Vector3(0.1f,0.1f,0.1f);
                        vertexSphere.name = "vertex" + indexNumber;
                        
                    }
                    foreach (KeyValuePair <int, Vector3 > key in posIndex)
                    {
                        Debug.Log("Key (Vertex Position): " + key.Value + "Number: " + key.Key);
                    }
                    UnityEngine.Object[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
                    foreach (GameObject vertexSphere in allObjects){
                        //Debug.Log(somethingElse + "is an active object" + somethingElse.GetInstanceID());
                        //vertex 위치 출력
                        Debug.Log(vertexSphere.transform.position);
                    }
                }
                // Optional: Attach the sphere to a parent object for organization
                // sphere.transform.parent = cube.transform;

            else
            {
                Debug.LogError("Mesh filter or mesh not found on the cube.");
            }
            }
        }
        
        else
        {
            Debug.LogError("Cube GameObject not assigned. Please assign the cube GameObject in the Inspector.");
        }
    }

    void Update()
    {
        originalRotation = transform.rotation;
    }
}