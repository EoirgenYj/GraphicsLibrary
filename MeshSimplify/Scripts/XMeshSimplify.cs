using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MeshSimplifyTool
{
    public class XMeshSimplify
    {
        private static XMeshSimplifyImpl m_MeshSimplifyImpl = new XMeshSimplifyImpl();
        
        //删除模型
        public static void DeleteGenerateMesh()
        {
        }
        //计算模型信息
        public static void ComputeMeshData(GameObject targetObj)
        {
            m_MeshSimplifyImpl.ComputeMeshData(targetObj);
        }
        //生成模型
        public static void GenerateMesh(GameObject targetObj)
        {
            m_MeshSimplifyImpl.GenerateMesh(targetObj);
        }

        public static void ClearTotalMesh()
        {
            m_MeshSimplifyImpl.ClearTotal();
        }

        public static void GenerateAndSaveMesh(GameObject targetObj)
        {
            m_MeshSimplifyImpl.GenerateAndSaveMesh(targetObj);
        }

        [MenuItem("测试/测试减面")]
        public static void OnTestSimplify()
        {
            string path = "Assets/Plugins/MeshSimplify/S01601_29Dimianmian.prefab";
            var obj = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            // XMeshSimplify.ComputeMeshData(obj);
            ClearTotalMesh();
            GenerateAndSaveMesh(obj);
        }
    }
}


