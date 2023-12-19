using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;

namespace MeshSimplifyTool
{
    internal class XMeshSimplifyImpl
    {
        #region Internal Func

        internal static Dictionary<GameObject, Simplify> SimplifiesMap = new Dictionary<GameObject, Simplify>();

        //删除生成的减面后的mesh，没有运行时需求，简单清空目录就好了
        internal void DeleteGeneratedMesh()
        {
            
        }
        //计算模型信息
        internal void ComputeMeshData(GameObject targetObj)
        {
            ComputeMeshDataRecursive(targetObj);
        }
        //生成模型
        internal void GenerateMesh(GameObject targetObj)
        {
            GenerateMeshRecursive(targetObj);
        }

        internal void GenerateAndSaveMesh(GameObject targetObj)
        {
            GenerateMesh(targetObj);
            var objList = new List<GameObject> { targetObj };
            
            for (int i = 0; i < targetObj.transform.childCount; i++)
            {
                var transform = targetObj.transform.GetChild(i);
                MeshFilter meshFilter = transform.GetComponent<MeshFilter>();
                SkinnedMeshRenderer skinnedMeshRenderer = transform.GetComponent<SkinnedMeshRenderer>();
                if (meshFilter != null || skinnedMeshRenderer != null)
                {
                    objList.Add(transform.gameObject);
                }
            }

            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var gameObject in objList)
                {
                    if (SimplifiesMap.TryGetValue(gameObject, out var simplify))
                    {
                        SaveMeshAsset(gameObject, simplify);
                    }
                    else
                    {
                        Debug.LogError($"Need Generate Mesh Infos On:{gameObject.name}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
                throw;
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();                
            }


        }
        
        internal void GenerateAllMesh()
        {
            //TODO 生成信息
            
            GenerateMeshTotal();
        }

        internal void ClearTotal()
        {
            SimplifiesMap.Clear();
        }

        private StringBuilder m_GenerateStringBuilder = new StringBuilder(200);

        
        internal void SaveAllMeshAssets()
        {
            AssetDatabase.StartAssetEditing();
            foreach (var simplify in SimplifiesMap)
            {
                SaveMeshAsset(simplify.Key, simplify.Value);
            }
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        
        #endregion

        #region Private Func
        
        private void SaveMeshAsset(GameObject targetObj, Simplify simplify)
        {
            string assetPath = simplify.AssetPath;
            if (string.IsNullOrEmpty(assetPath))
            {
                assetPath = AssetDatabase.GetAssetPath(simplify.OriginalMesh);
                if (string.IsNullOrEmpty(assetPath))
                {
                    Debug.LogError($"Error AssetPath On GameObject: {targetObj.name}");
                    return;    
                }

                simplify.AssetPath = assetPath;
            }
            
            string assetName = Path.GetFileNameWithoutExtension(assetPath);
            string assetNameTotal = Path.GetFileName(assetPath);
            m_GenerateStringBuilder.Append(assetPath.Substring(0, assetPath.Length - assetNameTotal.Length))
                .Append(assetNameTotal.Replace(assetNameTotal, $"{assetName}_Simplified.asset"));
            string generatePath = m_GenerateStringBuilder.ToString();

            if (simplify.HasData())
            {
                AssetDatabase.CreateAsset(simplify.SimplifierMesh, generatePath);
                // simplify.SimplifierMesh.UploadMeshData(false);
                Resources.UnloadAsset(simplify.SimplifierMesh);
            }

            m_GenerateStringBuilder.Clear();
        }

        private void ComputeMeshDataRecursive(GameObject targetObj)
        {
            ProcessCompute(targetObj);
            if (targetObj.transform.childCount <= 0) return;
            
            for (int i = 0; i < targetObj.transform.childCount; i++)
            {
                var transform = targetObj.transform.GetChild(i);
                ComputeMeshDataRecursive(transform.gameObject);
            }
        }

        private void ProcessCompute(GameObject targetObj)
        {
            if (SimplifiesMap.TryGetValue(targetObj, out var simplify)) return;
            MeshSimplifyType meshType = MeshUtil.IsCorrectMesh(targetObj);
            switch (meshType)
            {
                case MeshSimplifyType.Invalid:
                    // TODO 报错
                    return;
                case MeshSimplifyType.MeshFilter:
                    simplify = new Simplify();
                    simplify.ProcessMeshFilter(targetObj);
                    break;
                case MeshSimplifyType.SkinnedMeshRenderer:
                    simplify = new Simplify();
                    simplify.ProcessSkinnedMeshRenderer(targetObj);
                    break;
            }

            SimplifiesMap.Add(targetObj, simplify);
        }

        private void GenerateMeshTotal()
        {
            DeleteGeneratedMesh();
            foreach (var simplify in SimplifiesMap)
            {
                ProcessGenerate(simplify.Key, simplify.Value);
            }
        }

        private void ReGenerateMeshTotal()
        {
            List<GameObject> tempObj = SimplifiesMap.Keys.ToList();
            foreach (var obj in tempObj)
            {
                SimplifiesMap.Remove(obj);
                ProcessCompute(obj);
            }

            GenerateMeshTotal();
        }
        
        private void GenerateMeshRecursive(GameObject targetObj)
        {
            if (SimplifiesMap.TryGetValue(targetObj, out var simplify))
            {
                ProcessGenerate(targetObj, simplify);
            }
            else
            {
                ComputeMeshDataRecursive(targetObj);
                if (SimplifiesMap.TryGetValue(targetObj, out simplify))
                {
                    ProcessGenerate(targetObj, simplify);
                    if (targetObj.transform.childCount > 0)
                    {
                        for (int i = 0; i < targetObj.transform.childCount; i++)
                        {
                            Transform transform = targetObj.transform.GetChild(i);
                            GenerateMeshRecursive(transform.gameObject);
                        }
                    }
                }
                else
                {
                    //TODO 报错
                    return;
                }
            }
            
        }

        private void ProcessGenerate(GameObject targetObj, Simplify simplify)
        {
            if (simplify == null) return;
            
            if (simplify.SimplifierMesh != null) simplify.SimplifierMesh.Clear();

            float amount = simplify.VertexAmount;
            simplify.SimplifierMesh = Simplify.InternalCreateNewEmptyMesh(simplify);
            simplify.ComputeMeshWithVertexCount(targetObj, Mathf.RoundToInt(amount * simplify.GetOriginalMeshUniqueVertexCount()));
        }
        
        
        #endregion
    }    
}

