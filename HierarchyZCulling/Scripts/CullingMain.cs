using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

[RequireComponent(typeof(Camera))]
public partial class CullingMain : MonoBehaviour
{

    private Camera m_MainCam;
    
    private RenderTexture m_DepthTexture;//带mip的深度图
    private Shader m_DepthShader;//用来生成mip的Shader
    private Material m_DepthTextureMaterial;
    private const RenderTextureFormat m_DepthTextureFormat = RenderTextureFormat.RHalf;//深度取值范围0-1，单通道即可。
    
    private int m_DepthTextureShaderID;
    private int m_DepthTextureSize;

    private bool m_DepthTextureGenerated;
    
    public int DepthTextureSize
    {
        get
        {
            if (m_DepthTextureSize == 0)
                m_DepthTextureSize = Mathf.NextPowerOfTwo(Mathf.Max(Screen.width, Screen.height));
            return m_DepthTextureSize;
        }
    }

    private void Start()
    {
#if UNITY_EDITOR
        //Debug Init
        var town = GameObject.Find("Town");
        var length = town.transform.childCount;
        m_Objs = new List<GameObject>(length);
        for (int i = 0; i < length; i++)
        {
            var child = town.transform.GetChild(i);
            m_Objs.Add(child.gameObject);
        }
#endif
        
        m_MainCam = Camera.main;
        if (m_MainCam != null)
        {
            m_MainCam.depthTextureMode |= DepthTextureMode.Depth;
        }

        //DepthMipMap
        InitDepthShaderID();
        InitDepthTexture();
        
        InitComputeShader();
        InitComputeId();
        InitComputeThread();
    }

    private void Update()
    {
        //上一帧的深度RT
        if (m_DepthTextureGenerated)
        {
            m_DepthTextureGenerated = false;
        }
    }

    private void OnPostRender()
    {
        if (!m_EnableCulling) return;
        
        GenerateDepthMap();
        GetObjectFlags();
        OnHizCulling();
    }

    private void OnEnable()
    {
        m_EnableCulling = true;
    }

    private void OnDisable()
    {
        //关闭摄像机后禁用剔除
        m_EnableCulling = false;
        //关闭摄像机后重置buffer
        m_BufferSettled = false;
        m_ObjectMatrixBuffer?.Release();
        m_ObjectMatrixBuffer = null;
        
        m_ObjectFlagBuffer?.Release();
        m_ObjectFlagBuffer = null;
    }

    private void OnDestroy()
    {
        m_DepthTexture.Release();
        Destroy(m_DepthTexture);
    }

    private void InitDepthShaderID()
    {
        m_DepthTextureShaderID = Shader.PropertyToID("_CameraDepthTexture");
        if (m_DepthShader == null)
        {
#if UNITY_EDITOR
            m_DepthShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Plugins/HierarchyZCulling/Shader/DepthMipmap.shader");
#endif
        }
        m_DepthTextureMaterial = new Material(m_DepthShader);
    }
    
    private void InitDepthTexture()
    {
        if (m_DepthTexture != null) return;

        m_DepthTexture = new RenderTexture(DepthTextureSize, DepthTextureSize, 0, m_DepthTextureFormat)
        {
            autoGenerateMips = false,
            useMipMap = true,
            filterMode = FilterMode.Point
        };
        m_DepthTexture.Create();
    }
    
    private void GenerateDepthMap()
    {
        int texWidth = m_DepthTexture.width;
        int mipmapLevel = 0;

        RenderTexture preRenderTexture = null;//上一层mipmap，即mipmapLevel-1对应的mipmap
        
        //最低到16x16的mip，所以只要宽度大于8就继续生成下一层mipmap
        while (texWidth > 8)
        {
            var currentRenderTexture = RenderTexture.GetTemporary(texWidth, texWidth, 0, m_DepthTextureFormat);//当前mipmapLevel对应的mipmap
            currentRenderTexture.filterMode = FilterMode.Point;
            if (preRenderTexture == null)
            {
                //Mipmap[0]即Copy原始的深度图
                Graphics.Blit(Shader.GetGlobalTexture(m_DepthTextureShaderID), currentRenderTexture);    
            }
            else
            {
                //将Mipmap[i] Blit到Mipmap[i + 1]上
                Graphics.Blit(preRenderTexture, currentRenderTexture, m_DepthTextureMaterial);
                RenderTexture.ReleaseTemporary(preRenderTexture);
            }
                
            Graphics.CopyTexture(currentRenderTexture, 0, 0, m_DepthTexture, 0, mipmapLevel);
            preRenderTexture = currentRenderTexture;
            
            texWidth /= 2;
            mipmapLevel++;
        }
        
        RenderTexture.ReleaseTemporary(preRenderTexture);
        
        m_DepthTextureGenerated = true;
    }
}
