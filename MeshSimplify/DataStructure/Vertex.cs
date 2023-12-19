using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MeshSimplifyTool
{
    public class Vertex : IHeapNode, IComparable<Vertex>
    {
        private Vector3 m_Position;
        public Vector3 Position => m_Position;

        private Vector3 m_PositionWorld;
        public Vector3 PositionWorld => m_PositionWorld;
        
        private int m_ID;
        public int ID => m_ID;
        
        private List<Vertex> m_NeighborsVertexList; // 相邻的顶点
        public List<Vertex> NeighborsVertexList => m_NeighborsVertexList;
        
        private List<Triangle> m_FacesTriangleList; // 相邻的三角形(相邻的面)
        public List<Triangle> FacesTriangleList => m_FacesTriangleList;
        
        public float ObjDist; // 缓存边坍缩的代价
        public Vertex CollapseVertex; //候选的坍缩顶点

        private Vector2 m_UV;
        public Vector2 UV => m_UV;
        
        private Vector3 m_Normal;
        public Vector3 Normal => m_Normal;

        public int HeapIndex { get; set; }
        
        /// <summary>
        /// 大于 : 1
        /// 等于 : 0
        /// 小于 : -1
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(Vertex other)
        {
            return ObjDist > other.ObjDist ? 1 : ObjDist < other.ObjDist ? -1 : 0;
        }

        public Vertex(Vector3 position, Vector3 positionWorld, int id, Vector2 uv, Vector3 normal)
        {
            m_Position = position;
            m_PositionWorld = positionWorld;
            m_ID = id;
            m_UV = uv;
            m_Normal = normal;

            m_NeighborsVertexList = new List<Vertex>();
            m_FacesTriangleList = new List<Triangle>();
        }

        //自身坍缩/删除
        public void Destructor()
        {
            for (int i = 0, iMax = m_NeighborsVertexList.Count; i < iMax; i++)
            {
                m_NeighborsVertexList[i].NeighborsVertexList.Remove(this);
            }
        }

        public void RemoveIfNonNeighbor(Vertex neighbor)
        {
            int index = m_NeighborsVertexList.IndexOf(neighbor);
            if (index < 0) return;

            if (m_FacesTriangleList.Any(triangle => triangle.HasVertex(neighbor)))
            {
                return;
            }
            
            m_NeighborsVertexList.RemoveAt(index);
        }

        public bool IsBorder()
        {
            foreach (var vertex in m_NeighborsVertexList)
            {
                int count = 0;
                foreach (var triangle in m_FacesTriangleList)
                {
                    if (triangle.HasVertex(vertex))
                    {
                        count++;
                    }

                    if (count == 1)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
    
}

