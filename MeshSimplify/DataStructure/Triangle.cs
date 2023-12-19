using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MeshSimplifyTool
{
    public class Triangle
    {
        private Vertex[] m_Vertices;
        public Vertex[] Vertices => m_Vertices;

        private bool m_HasUVData;
        public bool HasUVData => m_HasUVData;

        //UV在Mesh中的索引
        private int[] m_UVs;
        public int[] UVs => m_UVs;
        //组成三角形的顶点索引
        private int[] m_Indices;
        public int[] Indices => m_Indices;

        private Vector3 m_Normal;
        public Vector3 Normal => m_Normal;

        private int m_SubMeshIndex;
        public int SubMeshIndex => m_SubMeshIndex;

        private int m_Index;
        public int Index => m_Index;

        //m_FacesIndex记录了对于当前的三角形来说，它在它的各个顶点中，处于相邻三角形列表中的索引
        private int[] m_FacesIndex;
        public int[] FacesIndex => m_FacesIndex;

        public Triangle(int subMeshIndex, int tIndex, Vertex v0, Vertex v1, Vertex v2, bool hasUVData, int vIndex0, int vIndex1, int vIndex2, bool compute)
        {
            m_Vertices = new Vertex[3];
            m_UVs = new int[3];
            m_Indices = new int[3];

            m_Vertices[0] = v0;
            m_Vertices[1] = v1;
            m_Vertices[2] = v2;

            m_SubMeshIndex = subMeshIndex;
            m_Index = tIndex;
            m_HasUVData = hasUVData;

            if (m_HasUVData)
            {
                m_UVs[0] = vIndex0;
                m_UVs[1] = vIndex1;
                m_UVs[2] = vIndex2;
            }

            m_Indices[0] = vIndex0;
            m_Indices[1] = vIndex1;
            m_Indices[2] = vIndex2;

            if (compute)
            {
                ComputeNormal();
            }

            m_FacesIndex = new int[3];

            for (int i = 0; i < 3; i++)
            {
                //m_FacesIndex记录了对于当前的三角形来说，它在它的各个顶点中，处于相邻三角形列表中的索引
                m_FacesIndex[i] = m_Vertices[i].FacesTriangleList.Count;
                //把自己添加到每个顶点的相邻三角形列表的末端
                m_Vertices[i].FacesTriangleList.Add(this);

                if (!compute) continue;

                for (int j = 0; j < 3; j++)
                {
                    if (i == j) continue;
                    if (m_Vertices[i].NeighborsVertexList.Contains(m_Vertices[j]) == false)
                    {
                        //把三角形的三个顶点互相添加为自己的相邻顶点
                        m_Vertices[i].NeighborsVertexList.Add(m_Vertices[j]);
                    }
                }
            }
        }

        public int TexAt(int i)
        {
            return m_UVs[i];
        }

        public int TexAt(Vertex vertex)
        {
            for (int i = 0; i < 3; i++)
            {
                if (m_Vertices[i] == vertex)
                {
                    return m_UVs[i];
                }
            }
            Debug.LogError("Triangle::TexAt(): Vertex not found");
            return 0;
        }

        public void SetTexAt(int i, int uv)
        {
            m_UVs[i] = uv;
        }
        
        public bool HasVertex(Vertex vertex)
        {
            return IndexOf(vertex) >= 0;
        }
        
        public int IndexOf(Vertex vertex)
        {
            for (int i = 0; i < 3; i++)
            {
                if (vertex == m_Vertices[i])
                {
                    return i;
                }
            }

            return -1;
        }

        public void ReplaceVertex(Vertex oldVertex, Vertex newVertex)
        {
            int index;
            for (index = 0; index < 3; index++)
            {
                if (oldVertex == m_Vertices[index])
                {
                    //替换为新的vertex
                    m_Vertices[index] = newVertex;
                    for (int i = 0; i < 3; i++)
                    {
                        if (i == index) continue;
                        //替换邻居的vertex为新
                        Vertex neighbor = m_Vertices[i];
                        List<Vertex> nNeighbors = neighbor.NeighborsVertexList;
                        nNeighbors.Remove(oldVertex);
                        if (!nNeighbors.Contains(newVertex))
                        {
                            nNeighbors.Add(newVertex);
                        }
                        //反向修改邻居
                        /*
                         * 由·——·
                         *     |
                         *     ·
                         * 变成了
                         *  ·
                         *   \
                         *    ·
                         */
                        List<Vertex> newVertexNeighbors = newVertex.NeighborsVertexList;
                        if (!newVertexNeighbors.Contains(neighbor))
                        {
                            newVertexNeighbors.Add(neighbor);
                        }
                    }
                    break;
                }
            }

            m_FacesIndex[index] = newVertex.FacesTriangleList.Count;
            newVertex.FacesTriangleList.Add(this);
            
            ComputeNormal();
        }
        
        //自身坍缩/删除
        public void Destructor()
        {
            int i;

            for (i = 0; i < 3; i++)
            {
                if (m_Vertices[i] != null)
                {
                    //获取当前三角形的顶点的相邻三角形列表
                    List<Triangle> list = m_Vertices[i].FacesTriangleList;
                    //取出列表最末尾的一个三角形
                    Triangle triangle = list[list.Count - 1];
                    //将取出的三角形复制到 顶点的相邻三角形列表中 当前的三角形 原本的位置
                    list[m_FacesIndex[i]] = triangle;
                    //将取出的三角形的对应的 当前顶点的相邻三角形列表中索引 设置为当前三角形的索引 
                    triangle.FacesIndex[triangle.IndexOf(m_Vertices[i])] = m_FacesIndex[i];
                    //移除最末尾的元素
                    list.RemoveAt(list.Count - 1);
                }
            }
            for (i = 0; i < 3; i++)
            {
                int iNext = (i + 1) % 3;

                if (m_Vertices[i] == null ||  m_Vertices[iNext] == null) continue;
                    
                m_Vertices[i].RemoveIfNonNeighbor(m_Vertices[iNext]);
                m_Vertices[iNext].RemoveIfNonNeighbor(m_Vertices[i]);
            }
        }
        
        public void ComputeNormal()
        {
            Vector3 v0 = m_Vertices[0].Position;
            Vector3 v1 = m_Vertices[1].Position;
            Vector3 v2 = m_Vertices[2].Position;
            
            m_Normal = Vector3.Cross(v1 - v0 , v2 - v1);
            if (m_Normal.magnitude == 0.0f) return;

            m_Normal /= m_Normal.magnitude;
        }
    }    
    
    /// <summary>
    /// 一个三角形列表。我们将其封装为一个类，以便在需要时能够序列化一个由三角形列表组成的列表, Unity不会序列化列表的列表或列表的数组。
    /// </summary>
    public class TriangleList
    {
        public TriangleList(int capacity)
        {
            Triangles = new List<Triangle>(capacity);
        }

        public List<Triangle> Triangles;
    }
}


