using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MeshSimplifyTool
{
    public partial class Simplify
    {
        //最大坍缩代价        
        internal const float MAX_VERTEX_COLLAPSE_COST = 10000000.0f;

        
        
        //原始的Mesh
        public Mesh OriginalMesh;
        //简化后的Mesh
        public Mesh SimplifierMesh;
        //顶点索引对应的潜在坍缩顶点的索引
        public int[] VertexMap;
        //顶点排序的列表
        public int[] VertexPermutation;
        //顶点列表
        public List<Vertex> VertexList;
        //三角列表
        public TriangleList[] TriangleLists;
        //世界坐标下顶点
        public Vector3[] WorldVertices;
        //原始的Mesh顶点数量
        public int OriginalMeshVertexCount = -1;
        //原始的Mesh尺寸
        public float OriginalMeshSize = 1.0f;
        //顶点的堆
        private Heap<Vertex> m_Heap;
        //使用边长
        public bool UseEdgeLength = true;
        //使用曲率
        public bool UseCurvature = true;
        //边缘曲率
        public float BorderCurvature = 2.0f;
        
        private List<Triangle> m_TempTriangles = new List<Triangle>();
        private List<Vertex> m_TempVertices = new List<Vertex>();
        public RelevanceSphere[] RelevanceSpheres;
        
        //

        public bool HasData()
        {
            return m_SimplifiedMesh != null || SimplifierMesh != null;
        }
        
        public void InitProcessCompute()
        {
            if (OriginalMesh == null) return;
            
            var vertexCount = OriginalMesh.vertexCount;

            VertexMap = new int[vertexCount];
            VertexPermutation = new int[vertexCount];
            VertexList = new List<Vertex>();
            TriangleLists = new TriangleList[OriginalMesh.subMeshCount];
            WorldVertices = new Vector3[OriginalMesh.vertices.Length];
            RelevanceSpheres = Array.Empty<RelevanceSphere>();
        }

        
        public void AddRelevanceSphere(GameObject targetObj)
        {
            if (RelevanceSpheres != null && RelevanceSpheres.Length > 0)
            {
                RelevanceSpheres[0].SetDefault(targetObj.transform, 0.0f);
            }
        }
        
        public void ProcessMeshFilter(GameObject targetObj)
        {
            MeshFilter meshFilter = targetObj.GetComponent<MeshFilter>();
            OriginalMesh = meshFilter.sharedMesh;
            InitProcessCompute();
            MeshUtil.TransformLocalToWorld(meshFilter, WorldVertices);
            ProcessCompute(targetObj, RelevanceSpheres);
        }

        public void ProcessSkinnedMeshRenderer(GameObject targetObj)
        {
            SkinnedMeshRenderer skinnedMeshRenderer = targetObj.GetComponent<SkinnedMeshRenderer>();
            OriginalMesh = skinnedMeshRenderer.sharedMesh;
            InitProcessCompute();
            MeshUtil.TransformLocalToWorld(skinnedMeshRenderer, WorldVertices);
            ProcessCompute(targetObj, RelevanceSpheres);
        }

        public void ProcessCompute(GameObject targetObj, RelevanceSphere[] relevanceSpheres)
        {
            OriginalMeshVertexCount = OriginalMesh.vertexCount;
            OriginalMeshSize = Mathf.Max(OriginalMesh.bounds.size.x, OriginalMesh.bounds.size.y,
                OriginalMesh.bounds.size.z);
            
            //创建小根堆
            m_Heap = Heap<Vertex>.CreateMinHeap();

            for (int i = 0; i < OriginalMeshVertexCount; i++)
            {
                VertexMap[i] = -1;
                VertexPermutation[i] = -1;
            }

            Vector2[] uv = OriginalMesh.uv;
            AddVertices(OriginalMesh.vertices, WorldVertices, OriginalMesh);
            

            //三角形数量的up-value
            int trianglesIndex = 0;
            for (int subMeshIndex = 0; subMeshIndex < OriginalMesh.subMeshCount; subMeshIndex++)
            {
                //获取顶点在Mesh中的索引
                int[] indices = OriginalMesh.GetTriangles(subMeshIndex);
                TriangleLists[subMeshIndex] = new TriangleList(indices.Length / 3);
                trianglesIndex = AddFacesListSubMesh(subMeshIndex, indices, uv, trianglesIndex);
            }

            if (Application.isEditor && !Application.isPlaying)
            {
                float[] costs = new float[VertexList.Count];
                int[] collapses = new int[VertexList.Count];
                
                CollapseCostJob.Compute(VertexList, TriangleLists, relevanceSpheres, UseEdgeLength, UseCurvature, BorderCurvature, OriginalMeshSize, costs, collapses);

                for (int i = 0; i < VertexList.Count; i++)
                {
                    var vertex = VertexList[i];
                    vertex.ObjDist = costs[i];
                    vertex.CollapseVertex = collapses[i] == -1 ? null : VertexList[collapses[i]];
                    m_Heap.Insert(vertex);
                }
            }
            else
            {
                //暂不考虑运行时
            }
            
            //对顶点进行逆排序映射
            /*
             * 因为我们是形成了小根堆，所以每次从堆顶拿一个顶点出来，都表示这个顶点是当前坍缩代价最小的，相对应的vertexNum就是该顶点坍缩后的顶点数
             * 所以如果我们需要坍缩到例如1w面，那就会有一个顶点对应着1w面的索引，在这1w面之上的顶点全部都可以坍缩
             */
            
            int vertexNum = VertexList.Count;
            while (vertexNum-- > 0)
            {
                Vertex min = m_Heap.ExtractTop();
                VertexPermutation[min.ID] = vertexNum;
                VertexMap[min.ID] = min.CollapseVertex?.ID ?? -1;
                Collapse(min, min.CollapseVertex, targetObj.transform, relevanceSpheres);
            }
        }

        private void AddVertices(Vector3[] listVertices, Vector3[] listVerticesWorld, Mesh mesh)
        {
            Vector2[] uvs = mesh.uv;
            Vector3[] normals = mesh.normals;
            for (int i = 0; i < listVertices.Length; i++)
            {
                Vertex vVertex = new Vertex(listVertices[i], listVerticesWorld[i], i, uvs[i], normals[i]);
                for (int j = 0; j < VertexList.Count; j++)
                {
                    Vertex uVertex = VertexList[j];
                    //uv的实际长度和模型比值非常小的话，我们认为这两个顶点是相邻的，这里是把原始数据转存了一下
                    if (Vector3.Distance(vVertex.Position, uVertex.Position) / OriginalMeshSize < float.Epsilon)
                    {
                        vVertex.NeighborsVertexList.Add(uVertex);
                        uVertex.NeighborsVertexList.Add(vVertex);
                    }
                }
                VertexList.Add(vVertex);
            }
        }

        private int AddFacesListSubMesh(int subMeshIndex, int[] indices, Vector2[] uv, int trianglesIndex)
        {
            bool hasUVData = uv?.Length > 0;

            List<Triangle> list = TriangleLists[subMeshIndex].Triangles;
            //每三个顶点索引创建一个三角形
            for (int i = 0; i < indices.Length; i += 3)
            {
                Triangle triangle = new Triangle(subMeshIndex, trianglesIndex + list.Count, VertexList[indices[i]],
                    VertexList[indices[i + 1]], VertexList[indices[i + 2]], hasUVData, indices[i], indices[i + 1],
                    indices[i + 2], true);
                list.Add(triangle);
                ShareUV(uv, triangle);
            }

            return trianglesIndex + list.Count;
        }

        //坍缩
        private void Collapse(Vertex u, Vertex v, Transform transform, RelevanceSphere[] relevanceSpheres)
        {
            if (v == null)
            {
                u.Destructor();
                return;
            }

            int i;
            m_TempVertices.Clear();

            for (i = 0; i < u.NeighborsVertexList.Count; i++)
            {
                Vertex neighbor = u.NeighborsVertexList[i];
                if (neighbor != u)
                {
                    m_TempVertices.Add(neighbor);
                }
            }
            
            m_TempTriangles.Clear();

            for (i = 0; i < u.FacesTriangleList.Count; i++)
            {
                if (u.FacesTriangleList[i].HasVertex(v))
                {
                     m_TempTriangles.Add(u.FacesTriangleList[i]);
                }
            }
            
            //删除边缘uv上的三角形
            for (i = m_TempTriangles.Count - 1; i >= 0; i--)
            {
                Triangle triangle = m_TempTriangles[i];
                triangle.Destructor();
            }
            
            //更新存活的三角形，并用v替代u;
            for (i = u.FacesTriangleList.Count - 1; i >= 0; i--)
            {
                u.FacesTriangleList[i].ReplaceVertex(u, v);
            }
            //u顶点自身坍缩删除
            u.Destructor();
            
            //对于新的相邻顶点，重新计算坍缩代价
            for (i = 0; i < m_TempVertices.Count; i++)
            {
                ComputeEdgeCostAtVertex(m_TempVertices[i], transform, relevanceSpheres);
                m_Heap.ModifyElement(m_TempVertices[i].HeapIndex, m_TempVertices[i]);
            }
        }

        private void ShareUV(Vector2[] uv, Triangle triangle)
        {
            if (triangle == null || triangle.HasUVData == false) return;
            
            //如果恰好碰到了，不同的顶点索引对应同一个UV坐标，比如顶点索引0和顶点索引38的uv坐标是相同的，两个UV重叠在一起，那么他们是共享UV
            if (uv == null || uv.Length == 0) return;

            for (int i = 0; i < 3; i++)
            {
                //当前的顶点索引
                int currentVert = i;
                //当前的顶点对应的所有相邻三角形(面)的集合
                var facesTriangles = triangle.Vertices[currentVert].FacesTriangleList;
                
                for (int j = 0; j < facesTriangles.Count; j++)
                {
                    //相邻的三角形
                    Triangle facesTriangle = facesTriangles[j];
                    //跳过当前三角形
                    if (triangle == facesTriangle) continue;
                    //顶点在当前三角形中的uv坐标索引
                    int tex1 = triangle.TexAt(currentVert);
                    //相邻的三角形中，当前的顶点索引对应的uv坐标索引
                    int tex2 = facesTriangle.TexAt(triangle.Vertices[currentVert]);
                    //如果是同一个索引则跳过
                    if (tex1 == tex2) continue;

                    Vector2 uv1 = uv[tex1];
                    Vector2 uv2 = uv[tex2];

                    //当不同的三角形顶点索引对应同一个uv坐标时,即为当前三角形共享的UV坐标
                    if (uv1 == uv2)
                    {
                        triangle.SetTexAt(currentVert, tex2);
                    }
                }
            }
        }

        private void ComputeEdgeCostAtVertex(Vertex vertex, Transform transform, RelevanceSphere[] relevanceSpheres)
        {
            if (vertex.NeighborsVertexList.Count == 0)
            {
                vertex.CollapseVertex = null;
                vertex.ObjDist = -0.01f;
                return;
            }

            vertex.ObjDist = MAX_VERTEX_COLLAPSE_COST;
            vertex.CollapseVertex = null;

            //相关性趋势
            float relevanceBias = 0.0f;

            if (relevanceSpheres != null)
            {
                for (int sphere = 0; sphere < relevanceSpheres.Length; sphere++)
                {
                    Matrix4x4 sphereMatrix = Matrix4x4.TRS(relevanceSpheres[sphere].Position,
                        relevanceSpheres[sphere].Rotation, relevanceSpheres[sphere].Scale);
                    Vector3 positionWorld = vertex.PositionWorld;
                    Vector3 positionLocal = sphereMatrix.inverse.MultiplyPoint(positionWorld);

                    if (positionLocal.magnitude <= 0.5f)
                    {
                        relevanceBias = relevanceSpheres[sphere].Relevance;
                    }
                }
            }

            for (int i = 0; i < vertex.NeighborsVertexList.Count; i++)
            {
                float dist = ComputeEdgeCollapseCost(vertex, vertex.NeighborsVertexList[i], relevanceBias);

                if (vertex.CollapseVertex == null || dist < vertex.ObjDist)
                {
                    vertex.CollapseVertex = vertex.NeighborsVertexList[i];
                    vertex.ObjDist = dist;
                }
            }
            
        }

        private float ComputeEdgeCollapseCost(Vertex u, Vertex v, float relevanceBias)
        {
            int i;
            //边长
            float edgeLength = UseEdgeLength ? Vector3.Magnitude(v.Position - u.Position) / OriginalMeshSize : 1.0f;
            //曲率
            float curvature = 0.001f;
            if (edgeLength < float.Epsilon)
            {
                return BorderCurvature * (1 - Vector3.Dot(u.Normal, v.Normal) + 2 * Vector3.Distance(u.UV, v.UV));
            }
            else
            {
                List<Triangle> sides = new List<Triangle>();
                //把u和v的相邻的三角形全部拿到
                for (i = 0; i < u.FacesTriangleList.Count; i++)
                {
                    if (u.FacesTriangleList[i].HasVertex(v))
                    {
                        sides.Add(u.FacesTriangleList[i]);
                    }
                }
                //cost(u, v) = ||u - v|| * max{min{(1 - u.normal * n.normal) / 2}}  一条边是否要坍缩
                /*
                 * 先通过叉积求出法线，两个法线点积的结果就是坍塌边uv的曲率值，然后和两点距离相乘，得到就是坍缩代价
                 */
                if (UseCurvature)
                {
                    for (i = 0; i < u.FacesTriangleList.Count; i++)
                    {
                        float minCurvature = 1.0f;

                        for (int j = 0; j < sides.Count; j++)
                        {
                            float dotProduct = Vector3.Dot(u.FacesTriangleList[i].Normal, sides[j].Normal);
                            minCurvature = Mathf.Min(minCurvature, (1.0f - dotProduct) / 2.0f);
                        }

                        curvature = Mathf.Max(curvature, minCurvature);
                    }
                }

                bool isBorder = u.IsBorder();
                if (isBorder && sides.Count > 1)
                {
                    curvature = 1.0f;
                }

                if (BorderCurvature > 1 && isBorder)
                {
                    curvature = BorderCurvature;
                }
            }

            curvature += relevanceBias;

            return edgeLength * curvature;
        }
    }    
}

