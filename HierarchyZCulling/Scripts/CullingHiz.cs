using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public partial class CullingMain
{
    private bool m_EnableCulling;
    public bool EnableCulling => m_EnableCulling;

    private List<GameObject> m_Objs;

    private struct ObjectFlag
    {
        public Vector3 BoundMin;
        public Vector3 BoundMax;
        public int ObjectID;
    }

    private bool m_OnPostRendered;
    private bool m_BufferSettled;

    public ComputeShader Compute;

    private const int m_ThreadMaxCount = 64;
    private int m_ObjectCount;
    private ObjectFlag[] m_ObjectFlagArr;
    private Matrix4x4[] m_ObjectMatrix4x4s;

    private ComputeBuffer m_ObjectMatrixBuffer;
    private ComputeBuffer m_ObjectFlagBuffer;

    private int m_CullingKernelID;

    private int m_ObjectMatrixBufferID;
    private int m_ObjectFlagBufferID;
    private int m_ObjectCountID;
    private int m_HizTextureID;
    private int m_VPMatrixID;
    private int m_CameraFrustumPlanesID;
    private Plane[] m_CameraFrustumPlanes = new Plane[6];
    private Vector4[] m_CameraFrustumPlanesVector4 = new Vector4[6];

    private void InitComputeId()
    {
        m_CullingKernelID = Compute.FindKernel("CullingMain");
        m_ObjectMatrixBufferID = Shader.PropertyToID("object_matrix_buffer");
        m_ObjectFlagBufferID = Shader.PropertyToID("object_flag_buffer");
        m_ObjectCountID = Shader.PropertyToID("obj_total_count");
        m_HizTextureID = Shader.PropertyToID("hiz_texture_2d");
        m_VPMatrixID = Shader.PropertyToID("camera_vp_matrix");
        m_CameraFrustumPlanesID = Shader.PropertyToID("camera_frustum_planes");
    }

    private void InitComputeShader()
    {
        if (Compute == null)
        {
#if UNITY_EDITOR
            Compute = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Plugins/HierarchyZCulling/Shader/DepthCulling.compute");
#endif
        }
        Compute.SetBool("is_opengl",
            m_MainCam.projectionMatrix.Equals(GL.GetGPUProjectionMatrix(m_MainCam.projectionMatrix, false)));
        Compute.SetInt("original_depth_size", m_DepthTextureSize);
        Compute.SetInt("thread_count", m_ThreadMaxCount);
    }

    private void InitComputeThread()
    {
        m_ObjectCount = m_ThreadMaxCount * 10;
        if (m_ObjectFlagArr == null)
            m_ObjectFlagArr = new ObjectFlag[m_ObjectCount];
    }
    
    private void ModifyBufferCount()
    {
        if (m_BufferSettled) return;

        // int st = sizeof(float) * 6 + sizeof(int);
        // Debug.Log(st);
        m_ObjectMatrixBuffer?.Release();
        m_ObjectFlagBuffer?.Release();
        m_ObjectMatrixBuffer = new ComputeBuffer(m_ObjectCount, sizeof(float) * 16);
        m_ObjectFlagBuffer = new ComputeBuffer(m_ObjectCount, sizeof(float) * 6 + sizeof(int));
        Compute.SetBuffer(m_CullingKernelID, m_ObjectMatrixBufferID, m_ObjectMatrixBuffer);
        Compute.SetBuffer(m_CullingKernelID, m_ObjectFlagBufferID, m_ObjectFlagBuffer);
        Compute.SetInt(m_ObjectCountID, m_ObjectCount);
        m_ObjectMatrix4x4s = new Matrix4x4[m_ObjectCount];
        m_ObjectFlagArr = new ObjectFlag[m_ObjectCount];
        m_BufferSettled = true;
    }

    //TODO 动态减数组长度
    private void ModifyObjectArrLength()
    {
        m_BufferSettled = false;
        var diff = m_Objs.Count - m_ObjectCount;
        var coefficient = diff % m_ThreadMaxCount == 0 ? diff / 64 : diff / 64 + 1;
        m_ObjectCount += coefficient * m_ThreadMaxCount;
        ModifyBufferCount();
    }

    //TODO ObjectFlag进入静态物体管理
    private void GetObjectFlags()
    {
        if (m_Objs.Count > m_ObjectCount)
            ModifyObjectArrLength();
        
        ModifyBufferCount();
        
        for (int i = 0; i < m_Objs.Count; i++)
        {
            var obj = m_Objs[i];
            var bound = obj.GetComponent<MeshFilter>().sharedMesh.bounds;
            m_ObjectFlagArr[i] = new ObjectFlag
            {   
                BoundMin = bound.min,
                BoundMax = bound.max,
                ObjectID = i,
            };
            Vector3 position = obj.transform.position;
            m_ObjectMatrix4x4s[i] = Matrix4x4.TRS(position, obj.transform.rotation, obj.transform.localScale);
        }
        m_ObjectMatrixBuffer.SetData(m_ObjectMatrix4x4s);
        m_ObjectFlagBuffer.SetData(m_ObjectFlagArr);
    }

    private void UpdateCameraFrustumPlanes()
    {
        GeometryUtility.CalculateFrustumPlanes(m_MainCam, m_CameraFrustumPlanes);
        for (int i = 0; i < m_CameraFrustumPlanes.Length; i++)
        {
            Vector4 vector4 = m_CameraFrustumPlanes[i].normal;
            vector4.w = m_CameraFrustumPlanes[i].distance;
            m_CameraFrustumPlanesVector4[i] = vector4;
        }
        Compute.SetVectorArray(m_CameraFrustumPlanesID, m_CameraFrustumPlanesVector4);
    }
    
    private void OnHizCulling()
    {
        Compute.SetTexture(m_CullingKernelID, m_HizTextureID, m_DepthTexture);
        Compute.SetMatrix(m_VPMatrixID, GL.GetGPUProjectionMatrix(m_MainCam.projectionMatrix, false) * m_MainCam.worldToCameraMatrix);
        UpdateCameraFrustumPlanes();
        Compute.Dispatch(m_CullingKernelID, m_ObjectCount / m_ThreadMaxCount, 1, 1);
        m_OnPostRendered = true;
        
        m_ObjectFlagBuffer.GetData(m_ObjectFlagArr);
        for (int i = 0; i < m_Objs.Count; i++)
        {
            // var obj = m_Objs[i];
            // if (m_ObjectFlagArr[i].ObjectID < 0)
            // {
            //     if (obj.gameObject.activeSelf) obj.SetActive(false);
            // }
            // else
            // {
            //     if (!obj.activeSelf) obj.SetActive(true);
            // }
            var tempBounds = m_Objs[i].GetComponent<MeshCollider>().sharedMesh.bounds;
            Bounds bounds = new Bounds(m_Objs[i].transform.position, tempBounds.size);
            // if (GeometryUtility.TestPlanesAABB(m_CameraFrustumPlanes, bounds))
            // {
            //     m_Objs[i].SetActive(true);
            // }
            // else
            // {
            //     m_Objs[i].SetActive(false);
            // }
        }
    }
}
