using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

namespace MeshSimplifyTool
{
    public unsafe struct StructVertex
    {
        public Vector3 Position;
        public Vector3 PositionWorld;
        public int ID;
        public int* Neighbors;
        public int NeighborCount;
        public int* Faces;
        public int FacesCount;
        public int IsBorder;
    }

    public unsafe struct StructTriangle
    {
        public int* Indices;
        public Vector3 Normal;
        public int index;
    }

    public struct StructRelevanceSphere
    {
        public Matrix4x4 Transformation;
        public float Relevance;
    }

    public unsafe struct ComputeCostJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<StructVertex> Vertices;
        [ReadOnly] public NativeArray<StructTriangle> Triangles;
        [ReadOnly] public NativeArray<StructRelevanceSphere> Spheres;

        public NativeArray<float> Result;
        public NativeArray<int> Collapse;

        public bool UseEdgeLength;
        public bool UseCurvature;
        public float BorderCurvature;
        public float OriginalMeshSize;

        public void Execute(int index)
        {
            StructVertex sv = Vertices[index];
            if (sv.NeighborCount == 0)
            {
                Collapse[index] = -1;
                Result[index] = -0.01f;
                return;
            }

            float cost = float.MaxValue;
            int collapse = -1;

            float relevanceBias = 0.0f;

            for (int sphere = 0; sphere < Spheres.Length; sphere++)
            {
                Matrix4x4 mtxSphere = Spheres[sphere].Transformation;

                Vector3 v3World = sv.PositionWorld;
                Vector3 v3Local = mtxSphere.inverse.MultiplyPoint(v3World);

                if (v3Local.magnitude <= 0.5f)
                {
                    relevanceBias = Spheres[sphere].Relevance;
                }
            }

            for (int i = 0; i < sv.NeighborCount; i++)
            {
                float dist = ComputeEdgeCollapseCost(sv, Vertices[sv.Neighbors[i]], relevanceBias);

                if (collapse == -1 || dist < cost)
                {
                    collapse = sv.Neighbors[i];
                    cost = dist;
                }
            }

            Result[index] = cost;
            Collapse[index] = collapse;
        }

        public static bool HasVertex(StructTriangle t, int v)
        {
            return IndexOf(t, v) >= 0;
        }

        public static int IndexOf(StructTriangle t, int v)
        {
            for (int i = 0; i < 3; i++)
            {
                if (t.Indices[i] == v)
                {
                    return i;
                }
            }
            return -1;
        }
        
        private float ComputeEdgeCollapseCost(StructVertex u, StructVertex v, float relevanceBias)
        {
            bool useEdgeLength = UseEdgeLength;
            bool useCurvature = UseCurvature;

            int i;
            float edgeLength = useEdgeLength ? Vector3.Magnitude(v.Position - u.Position) / OriginalMeshSize : 1.0f;
            float curvature = 0.001f;
            if (edgeLength < float.Epsilon)
            {
                return BorderCurvature;
            }
            else
            {
                List<StructTriangle> sides = new List<StructTriangle>();

                for (i = 0; i < u.FacesCount; i++)
                {
                    StructTriangle uTriangle = Triangles[u.Faces[i]];
                    if (HasVertex(uTriangle, v.ID))
                    {
                        sides.Add(uTriangle);
                    }
                }

                if (useCurvature)
                {
                    for (i = 0; i < u.FacesCount; i++)
                    {
                        float minCurv = 1.0f;
                        for (int j = 0; j < sides.Count; j++)
                        {
                            float dotProduct = Vector3.Dot(Triangles[u.Faces[i]].Normal, sides[j].Normal);
                            minCurv = Mathf.Min(minCurv, (1.0f - dotProduct) / 2.0f);
                        }

                        curvature = Mathf.Max(curvature, minCurv);
                    }
                }

                if (u.IsBorder == 1 && sides.Count > 1)
                {
                    curvature = 1.0f;
                }

                if (BorderCurvature > 1 && u.IsBorder == 1)
                {
                    curvature = BorderCurvature;
                }

                curvature += relevanceBias;
            }

            return edgeLength * curvature;
        }
    }
    
    public static class CollapseCostJob
    {
        public static unsafe void Compute(List<Vertex> vertices, TriangleList[] triangleLists, RelevanceSphere[] relevanceSpheres
            , bool isUseEdgeLength, bool isUseCurvature, float borderCurvature, float originalMeshSize, float[] costs, int[] collapses)
        {
            ComputeCostJob job = new ComputeCostJob
            {
                UseEdgeLength = isUseEdgeLength,
                UseCurvature = isUseCurvature,
                BorderCurvature = borderCurvature,
                OriginalMeshSize = originalMeshSize
            };
            List<StructTriangle> structTriangles = new List<StructTriangle>();
            int intAlignment = UnsafeUtility.SizeOf<int>();
            foreach (var triangleList in triangleLists)
            {
                List<Triangle> triangles = triangleList.Triangles;
                foreach (var t in triangles)
                {
                    StructTriangle st = new StructTriangle()
                    {
                        index = t.Index,
                        Indices = (int*)UnsafeUtility.Malloc(t.Indices.Length * intAlignment, intAlignment, Allocator.TempJob),
                        Normal = t.Normal
                    };
                    for (int j = 0; j < t.Indices.Length; j++)
                    {
                        st.Indices[j] = t.Indices[j];
                    }
                    structTriangles.Add(st);
                }
            }

            job.Triangles = new NativeArray<StructTriangle>(structTriangles.ToArray(), Allocator.TempJob);
            job.Vertices = new NativeArray<StructVertex>(vertices.Count, Allocator.TempJob);
            for (int i = 0; i < vertices.Count; i++)
            {
                Vertex v = vertices[i];
                StructVertex sv = new StructVertex()
                {
                    Position = v.Position,
                    PositionWorld = v.PositionWorld,
                    ID = v.ID,
                    Neighbors = v.NeighborsVertexList.Count == 0
                        ? null
                        : (int*)UnsafeUtility.Malloc(v.NeighborsVertexList.Count * intAlignment, intAlignment,
                            Allocator.TempJob),
                    NeighborCount = v.NeighborsVertexList.Count,
                    Faces = (int*)UnsafeUtility.Malloc(v.FacesTriangleList.Count * intAlignment, intAlignment,
                        Allocator.TempJob),
                    FacesCount = v.FacesTriangleList.Count,
                    IsBorder = v.IsBorder() ? 1 : 0,
                };
                for (int j = 0; j < v.NeighborsVertexList.Count; j++)
                {
                    sv.Neighbors[j] = v.NeighborsVertexList[j].ID;
                }

                for (int j = 0; j < v.FacesTriangleList.Count; j++)
                {
                    sv.Faces[j] = v.FacesTriangleList[j].Index;
                }

                job.Vertices[i] = sv;
            }

            job.Spheres = new NativeArray<StructRelevanceSphere>(relevanceSpheres.Length, Allocator.TempJob);
            for (int i = 0; i < relevanceSpheres.Length; i++)
            {
                RelevanceSphere rs = relevanceSpheres[i];
                StructRelevanceSphere srs = new StructRelevanceSphere()
                {
                    Transformation = Matrix4x4.TRS(rs.Position, rs.Rotation, rs.Scale),
                    Relevance = rs.Relevance,
                };
                job.Spheres[i] = srs;
            }

            job.Result = new NativeArray<float>(costs, Allocator.TempJob);
            job.Collapse = new NativeArray<int>(collapses, Allocator.TempJob);
            JobHandle handle = job.Schedule(costs.Length, 1);
            handle.Complete();
            
            job.Result.CopyTo(costs);
            job.Collapse.CopyTo(collapses);
            for (int i = 0; i < job.Triangles.Length; i++)
            {
                UnsafeUtility.Free(job.Triangles[i].Indices, Allocator.TempJob);
            }

            for (int i = 0; i < vertices.Count; i++)
            {
                UnsafeUtility.Free(job.Vertices[i].Neighbors, Allocator.TempJob);
                UnsafeUtility.Free(job.Vertices[i].Faces, Allocator.TempJob);
            }
            job.Vertices.Dispose();
            job.Triangles.Dispose();
            job.Spheres.Dispose();
            job.Result.Dispose();
            job.Collapse.Dispose();
        }
    }
    
}

