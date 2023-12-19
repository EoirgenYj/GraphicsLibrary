using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MeshSimplifyTool
{
    public partial class Simplify
    {
        public string AssetPath;
        public float VertexAmount = 0.25f;

        private Vector3[] m_OriginalVertices;
        private Vector3[] m_OriginalNormals;
        private Vector4[] m_OriginalTangents;
        private Vector2[] m_OriginalTexcoord1;
        private Vector2[] m_OriginalTexcoord2;
        private Color32[] m_OriginalColors32;
        private BoneWeight[] m_OriginalBoneWeights;
        

        private int[][] m_OriginalSubMeshs;

        private Vector3[] m_VerticesIn;
        private Vector3[] m_NormalsIn;
        private Vector4[] m_TangentsIn;
        private Vector2[] m_TexCoord1In;
        private Vector2[] m_TexCoord2In;
        private Color32[] m_Colors32In;
        private BoneWeight[] m_BoneWeights;
        private Matrix4x4[] m_BindPoses;
        private int[][] m_SubMeshs;
        private int[] m_TriangleCount;
        private Mesh m_SimplifiedMesh;
        private int[] m_VertexIndexMap;

        private Action<Vector3[]> m_AssignVertices;
        private Action<Vector3[]> m_AssignNormals;
        private Action<Vector4[]> m_AssignTangents;
        private Action<Vector2[]> m_AssignUV;
        private Action<Vector2[]> m_AssignUV2;
        private Action<Color32[]> m_AssignColor32;
        private Action<BoneWeight[]> m_AssignBoneWeights;
        private Action<int[]>[] m_SetTriangles;

        internal int GetOriginalMeshUniqueVertexCount()
        {
            return OriginalMeshVertexCount;
        }

        internal static Mesh InternalCreateNewEmptyMesh(Simplify simplify)
        {
            return simplify.CreateNewEmptyMesh();
        }

        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="targetObj"></param>
        /// <param name="vertices">原始的顶点数量*百分比</param>
        internal void ComputeMeshWithVertexCount(GameObject targetObj, int vertices)
        {
            if (GetOriginalMeshUniqueVertexCount() == -1) return;

            if (vertices < 3) return;

            m_OriginalVertices = m_OriginalVertices ?? OriginalMesh.vertices;
            m_OriginalNormals = m_OriginalNormals ?? OriginalMesh.normals;
            m_OriginalTangents = m_OriginalTangents ?? OriginalMesh.tangents;
            m_OriginalTexcoord1 = m_OriginalTexcoord1 ?? OriginalMesh.uv;
            m_OriginalTexcoord2 = m_OriginalTexcoord2 ?? OriginalMesh.uv2;
            m_OriginalColors32 = m_OriginalColors32 ?? OriginalMesh.colors32;
            m_OriginalBoneWeights = m_OriginalBoneWeights ?? OriginalMesh.boneWeights;
            m_BindPoses = m_BindPoses ?? OriginalMesh.bindposes;

            int subMeshCount = OriginalMesh.subMeshCount;
            if (m_OriginalSubMeshs == null)
            {
                m_OriginalSubMeshs = new int[subMeshCount][];
                for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
                {
                    m_OriginalSubMeshs[subMesh] = OriginalMesh.GetTriangles(subMesh);
                }
            }

            if (vertices >= GetOriginalMeshUniqueVertexCount())
            {
                //原始顶点数量
                SimplifierMesh.triangles = Array.Empty<int>();
                SimplifierMesh.subMeshCount = OriginalMesh.subMeshCount;

                SimplifierMesh.vertices = m_OriginalVertices;
                SimplifierMesh.normals = m_OriginalNormals;
                SimplifierMesh.tangents = m_OriginalTangents;
                SimplifierMesh.uv = m_OriginalTexcoord1;
                SimplifierMesh.uv2 = m_OriginalTexcoord2;
                SimplifierMesh.colors32 = m_OriginalColors32;
                SimplifierMesh.boneWeights = m_OriginalBoneWeights;
                SimplifierMesh.bindposes = m_BindPoses;
                
                SimplifierMesh.triangles = OriginalMesh.triangles;
                SimplifierMesh.subMeshCount = subMeshCount;

                for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
                {
                    SimplifierMesh.SetTriangles(m_OriginalSubMeshs[subMesh], subMesh);
                }

                SimplifierMesh.name = targetObj.name + " simplified mesh";
                return;
            }

            ConsolidateMesh(targetObj, VertexPermutation, VertexMap, vertices);

        }

        private unsafe void CopyStructure<T>(T[] original, ref T[] target)
        {
            if (target == null)
            {
                target = (T[])original.Clone();
            }
            else
            {
                original.CopyTo(target, 0);
            }
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="targetObj"></param>
        /// <param name="permutation">置换后的顶点索引</param>
        /// <param name="collapseMap">原始顶点索引，待坍缩</param>
        /// <param name="vertices">目标的顶点数量</param>
        private unsafe void ConsolidateMesh(GameObject targetObj, int[] permutation, int[] collapseMap, int vertices)
        {
            int subMeshCount = m_OriginalSubMeshs.Length;

            CopyStructure(m_OriginalVertices, ref m_VerticesIn);
            CopyStructure(m_OriginalNormals, ref m_NormalsIn);
            CopyStructure(m_OriginalTangents, ref m_TangentsIn);
            CopyStructure(m_OriginalTexcoord1, ref m_TexCoord1In);
            CopyStructure(m_OriginalTexcoord2, ref m_TexCoord2In);
            CopyStructure(m_OriginalColors32, ref m_Colors32In);
            CopyStructure(m_OriginalBoneWeights, ref m_BoneWeights);

            #region Legacy Copy

            //m_VerticesIn
            
            // if (m_VerticesIn == null)
            // {
            //     m_VerticesIn = (Vector3[])m_OriginalVertices.Clone();
            // }
            // else
            // {
            //     m_OriginalVertices.CopyTo(m_VerticesIn, 0);
            // }

            //m_NormalsIn
            // if (m_NormalsIn == null)
            // {
            //     m_NormalsIn = (Vector3[])m_OriginalNormals.Clone();
            // }
            // else
            // {
            //     m_OriginalNormals.CopyTo(m_NormalsIn, 0);
            // }

            //m_TangentsIn
            // if (m_TangentsIn == null)
            // {
            //     m_TangentsIn = (Vector4[])m_OriginalTangents.Clone();
            // }
            // else
            // {
            //     m_OriginalTangents.CopyTo(m_TangentsIn, 0);
            // }
            
            // //m_TexCoord1In
            // if (m_TexCoord1In == null)
            // {
            //     m_TexCoord1In = (Vector2[])m_OriginalTexcoord1.Clone();
            // }
            // else
            // {
            //     m_OriginalTexcoord1.CopyTo(m_TexCoord1In, 0);
            // }
            //
            // //m_TexCoord2In
            // if (m_TexCoord2In == null)
            // {
            //     m_TexCoord2In = (Vector2[])m_OriginalTexcoord2.Clone();
            // }
            // else
            // {
            //     m_OriginalTexcoord2.CopyTo(m_TexCoord2In, 0);
            // }
            
            // //m_Colors32In
            // if (m_Colors32In == null)
            // {
            //     m_Colors32In = (Color32[])m_OriginalColors32.Clone();
            // }
            // else
            // {
            //     m_OriginalColors32.CopyTo(m_Colors32In, 0);
            // }
            //
            // //m_BoneWeights
            // if (m_BoneWeights == null)
            // {
            //     m_BoneWeights = (BoneWeight[])m_OriginalBoneWeights.Clone();
            // }
            // else
            // {
            //     m_OriginalBoneWeights.CopyTo(m_BoneWeights, 0);
            // }
            //

            #endregion

            m_SubMeshs = m_SubMeshs ?? new int[subMeshCount][];
            m_TriangleCount = m_TriangleCount ?? new int[subMeshCount];

            bool validateUV1 = m_TexCoord1In?.Length > 0;
            bool validateUV2 = m_TexCoord2In?.Length > 0;
            bool validateNormal = m_NormalsIn?.Length > 0;
            bool validateTangent = m_TangentsIn?.Length > 0;
            bool validateColor32 = m_Colors32In?.Length > 0;
            bool validateBone = m_BoneWeights?.Length > 0;

            m_VertexIndexMap = m_VertexIndexMap ?? new int[m_VerticesIn.Length];
            for (int i = 0, iMax = m_VertexIndexMap.Length; i < iMax; i++)
            {
                m_VertexIndexMap[i] = -1;
            }

            int vIndex = 0;
            for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
            {
                CopyStructure(m_OriginalSubMeshs[subMeshIndex], ref m_SubMeshs[subMeshIndex]);
                int[] triangles = m_SubMeshs[subMeshIndex];
                /*
                 * 对于每个三角形来说，拿到这个三角形的第一个顶点，对应其坍缩后的总顶点数量的值
                 * 持续寻找直到这个值小于我们预期的坍缩后的顶点数量，跳出while循环时，即为这个三角形可以开始探索时的顶点序列，则寻找过程中的顶点都可以坍缩
                 */
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int index0 = triangles[i];
                    int index1 = triangles[i + 1];
                    int index2 = triangles[i + 2];
                    while (permutation[index0] >= vertices)
                    {
                        int cIndex = collapseMap[index0];
                        if (cIndex == -1 || index1 == cIndex || index2 == cIndex)
                        {
                            index0 = -1;
                            break;
                        }

                        index0 = cIndex;
                    }
                    
                    while (permutation[index1] >= vertices)
                    {
                        int cIndex = collapseMap[index1];
                        if (cIndex == -1 || index0 == cIndex || index2 == cIndex)
                        {
                            index1 = -1;
                            break;
                        }

                        index1 = cIndex;
                    }
                    
                    while (permutation[index2] >= vertices)
                    {
                        int cIndex = collapseMap[index2];
                        if (cIndex == -1 || index1 == cIndex || index0 == cIndex)
                        {
                            index2 = -1;
                            break;
                        }

                        index2 = cIndex;
                    }

                    if (index0 == -1 || index1 == -1 || index2 == -1)
                    {
                        triangles[i] = -1;
                        triangles[i + 1] = -1;
                        triangles[i + 2] = -1;
                        continue;
                    }
                    
                    
                    //将对应的顶点索引 置换为达到坍缩目标的顶点索引
                    if (m_VertexIndexMap[index0] == -1)
                    {
                        m_VertexIndexMap[index0] = vIndex++;
                    }
                    triangles[i] = m_VertexIndexMap[index0];

                    if (m_VertexIndexMap[index1] == -1)
                    {
                        m_VertexIndexMap[index1] = vIndex++;
                    }
                    triangles[i + 1] = m_VertexIndexMap[index1];

                    if (m_VertexIndexMap[index2] == -1)
                    {
                        m_VertexIndexMap[index2] = vIndex++;
                    }
                    triangles[i + 2] = m_VertexIndexMap[index2];
                }

                int length = triangles.Length;
                int head = 0;
                int tail = length - 1;
                while (head < tail)
                {
                    if (triangles[tail] == -1)
                    {
                        tail -= 3;
                        continue;
                    }

                    if (triangles[head] != -1)
                    {
                        head += 3;
                        continue;
                    }
                    //双指针搜索，整理离散数据

                    triangles[head] = triangles[tail - 2];
                    triangles[head + 1] = triangles[tail - 1];
                    triangles[head + 2] = triangles[tail];
                    triangles[tail - 2] = -1;
                    triangles[tail - 1] = -1;
                    triangles[tail] = -1;
                    head += 3;
                    tail -= 3;
                }
                
                //如果过程中有空数据被整理过：length = tail + 1
                if (tail < length - 1)
                {
                    m_TriangleCount[subMeshIndex] = tail + 1;
                }
                //初始状态下所有数据都是紧密排布的(比如模型减到0面...)
                else
                {
                    m_TriangleCount[subMeshIndex] = length;
                }
            }
            Vector2 tempUV = Vector2.zero;
            Vector2 tempUV2 = Vector2.zero;
            Vector3 tempNormal = Vector3.zero;
            Vector4 tempTangent = Vector4.zero;
            Color32 tempColor = Color.black;
            BoneWeight tempBoneWeight = new BoneWeight();
            
            Vector2 tempUVExchange = Vector2.zero;
            Vector2 tempUV2Exchange = Vector2.zero;
            Vector3 tempNormalExchange = Vector3.zero;
            Vector4 tempTangentExchange = Vector4.zero;
            Color32 tempColorExchange = Color.black;
            BoneWeight tempBoneWeightExchange = new BoneWeight();

            //index是原始的所有顶点索引,而m_VertexIndexMap[index]是从开始坍缩的顶点开始的，是之前的vIndex,从0开始的
            //顶点索引值为-1的将被跳过(保留),从第一个不为-1的值开始(0),逐步把要坍缩的顶点,从最小坍缩代价开始,集中到要坍缩的数据集合中去
            for (int index = 0; index < m_VertexIndexMap.Length; index++)
            {
                Vector3 temp = m_VerticesIn[index];
                if (validateUV1) tempUV = m_TexCoord1In[index];
                if (validateUV2) tempUV2 = m_TexCoord2In[index];
                if (validateNormal) tempNormal = m_NormalsIn[index];
                if (validateTangent) tempTangent = m_TangentsIn[index];
                if (validateColor32) tempColor = m_Colors32In[index];
                if (validateBone) tempBoneWeight = m_BoneWeights[index];
                while (m_VertexIndexMap[index] != -1)
                {
                    Vector3 tempExchange = m_VerticesIn[m_VertexIndexMap[index]];
                    if (validateUV1) tempUVExchange = m_TexCoord1In[m_VertexIndexMap[index]];
                    if (validateUV2) tempUV2Exchange = m_TexCoord2In[m_VertexIndexMap[index]];
                    if (validateNormal) tempNormalExchange = m_NormalsIn[m_VertexIndexMap[index]];
                    if (validateTangent) tempTangentExchange = m_TangentsIn[m_VertexIndexMap[index]];
                    if (validateColor32) tempColorExchange = m_Colors32In[m_VertexIndexMap[index]];
                    if (validateBone) tempBoneWeightExchange = m_BoneWeights[m_VertexIndexMap[index]];

                    m_VerticesIn[m_VertexIndexMap[index]] = temp;
                    
                    if (validateUV1) m_TexCoord1In[m_VertexIndexMap[index]] = tempUV;
                    if (validateUV2) m_TexCoord2In[m_VertexIndexMap[index]] = tempUV2;
                    if (validateNormal) m_NormalsIn[m_VertexIndexMap[index]] = tempNormal;
                    if (validateTangent) m_TangentsIn[m_VertexIndexMap[index]] = tempTangent;
                    if (validateColor32) m_Colors32In[m_VertexIndexMap[index]] = tempColor;
                    if (validateBone) m_BoneWeights[m_VertexIndexMap[index]] = tempBoneWeight;

                    temp = tempExchange;
                    tempUV = tempUVExchange;
                    tempUV2 = tempUV2Exchange;
                    tempNormal = tempNormalExchange;
                    tempTangent = tempTangentExchange;
                    tempColor = tempColorExchange;
                    tempBoneWeight = tempBoneWeightExchange;

                    int tempIndex = m_VertexIndexMap[index];
                    m_VertexIndexMap[index] = -1;
                    index = tempIndex;
                }
            }

            if (validateNormal)
            {
                for (int i = 0; i < vIndex; i++)
                {
                    m_NormalsIn[i] *= 0.1f;//Vector3.zero;
                }

                for (int i = 0; i < subMeshCount; i++)
                {
                    int[] triangles = m_SubMeshs[i];
                    for (int index = 0; index < m_TriangleCount[i]; index += 3)
                    {
                        int index0 = triangles[0];
                        int index1 = triangles[1];
                        int index2 = triangles[2];
                        Vector3 vertex0 = m_VerticesIn[index0];
                        Vector3 vertex1 = m_VerticesIn[index1];
                        Vector3 vertex2 = m_VerticesIn[index2];
                        
                        Vector3 normal = Vector3.Cross(vertex1 - vertex0, vertex2 - vertex1);//.normalized;

                        m_NormalsIn[index0] += normal;
                        m_NormalsIn[index1] += normal;
                        m_NormalsIn[index2] += normal;
                    }
                }

                for (int i = 0; i < vIndex; i++)
                {
                    m_NormalsIn[i] = m_NormalsIn[i].normalized;
                }
            }

            m_SimplifiedMesh = SimplifierMesh;

            m_AssignVertices = m_AssignVertices ?? (arr => m_SimplifiedMesh.vertices = arr);
            if (validateNormal) m_AssignNormals = m_AssignNormals ?? (arr => m_SimplifiedMesh.normals = arr);
            if (validateTangent) m_AssignTangents = m_AssignTangents ?? (arr => m_SimplifiedMesh.tangents = arr);
            if (validateUV1) m_AssignUV = m_AssignUV ?? (arr => m_SimplifiedMesh.uv = arr);
            if (validateUV2) m_AssignUV2 = m_AssignUV2 ?? (arr => m_SimplifiedMesh.uv2 = arr);
            if (validateColor32) m_AssignColor32 = m_AssignColor32 ?? (arr => m_SimplifiedMesh.colors32 = arr);
            if (validateBone) m_AssignBoneWeights = m_AssignBoneWeights ?? (arr => m_SimplifiedMesh.boneWeights = arr);
            if (m_SetTriangles == null)
            {
                m_SetTriangles = new Action<int[]>[subMeshCount];
                for (int i = 0; i < subMeshCount; i++)
                {
                    int index = i;
                    m_SetTriangles[i] = arr => m_SimplifiedMesh.SetTriangles(arr, index);
                }
            }

            m_SimplifiedMesh.triangles = null;
            
            //通过unsafe去原生内存做赋值
            UnsafeUtil.Vector3HackArraySizeCall(m_VerticesIn, vIndex, m_AssignVertices);
            if (validateNormal) UnsafeUtil.Vector3HackArraySizeCall(m_NormalsIn, vIndex, m_AssignNormals);
            if (validateTangent) UnsafeUtil.Vector4HackArraySizeCall(m_TangentsIn, vIndex, m_AssignTangents);
            if (validateUV1) UnsafeUtil.Vector2HackArraySizeCall(m_TexCoord1In, vIndex, m_AssignUV);
            if (validateUV2) UnsafeUtil.Vector2HackArraySizeCall(m_TexCoord2In, vIndex, m_AssignUV2);
            if (validateColor32) UnsafeUtil.Color32HackArraySizeCall(m_Colors32In, vIndex, m_AssignColor32);
            if (validateBone)
            {
                UnsafeUtil.BoneWeightHackArraySizeCall(m_BoneWeights, vIndex, m_AssignBoneWeights);
                m_SimplifiedMesh.bindposes = m_BindPoses;
            }

            m_SimplifiedMesh.subMeshCount = m_SubMeshs.Length;
            for (int i = 0; i < subMeshCount; i++)
            {
                UnsafeUtil.IntegerHackArraySizeCall(m_SubMeshs[i], m_TriangleCount[i], m_SetTriangles[i]);
            }
            m_SimplifiedMesh.UploadMeshData(false);
            SimplifierMesh = m_SimplifiedMesh;
        }
                
        private Mesh CreateNewEmptyMesh()
        {
            if (OriginalMesh == null)
            {
                return new Mesh();
            }

            Mesh meshOut = Object.Instantiate(OriginalMesh);
            meshOut.Clear();
            return meshOut;
        }
        
    }    
}


