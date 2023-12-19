using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace MeshSimplifyTool
{
    public enum MeshSimplifyType
    {
        Invalid,
        MeshFilter,
        SkinnedMeshRenderer
    }
    
    public static class MeshUtil
    {
        public static MeshSimplifyType IsCorrectMesh(GameObject targetObj)
        {
            var meshFilter = targetObj.GetComponent<MeshFilter>();
            var skinnedMeshRenderer = targetObj.GetComponent<SkinnedMeshRenderer>();

            if (meshFilter != null && meshFilter.sharedMesh != null && meshFilter.sharedMesh.vertexCount > 0)
            {
                return MeshSimplifyType.MeshFilter;
            }

            if (skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMesh != null && skinnedMeshRenderer.sharedMesh.vertexCount > 0)
            {
                return MeshSimplifyType.SkinnedMeshRenderer;
            }

            return MeshSimplifyType.Invalid;
            // return targetObj.GetComponent<MeshFilter>() != null ? MeshSimplifyType.MeshFilter : targetObj.GetComponent<SkinnedMeshRenderer>() != null ? MeshSimplifyType.SkinnedMeshRenderer : MeshSimplifyType.Invalid;
        }
        
        public static void TransformLocalToWorld(SkinnedMeshRenderer skin, Vector3[] worldVertices)
        {
            SkinLocalToWorldJob job = new SkinLocalToWorldJob();
            Transform[] bones = skin.bones;
            job.Bones = new NativeArray<Matrix4x4>(bones.Length, Allocator.TempJob);
            for (int i = 0, iMax = bones.Length; i < iMax; i++)
            {
                job.Bones[i] = bones[i].localToWorldMatrix;
            }

            Mesh mesh = skin.sharedMesh;
            job.BoneWeights = new NativeArray<BoneWeight>(mesh.boneWeights, Allocator.TempJob);
            job.BindPoses = new NativeArray<Matrix4x4>(mesh.bindposes, Allocator.TempJob);
            job.Vertices = new NativeArray<Vector3>(mesh.vertices, Allocator.TempJob);
            job.WorldPositions = new NativeArray<Vector3>(worldVertices, Allocator.TempJob);
            JobHandle handle = job.Schedule(worldVertices.Length, 1);
            handle.Complete();
            
            job.WorldPositions.CopyTo(worldVertices);
            job.Bones.Dispose();
            job.BoneWeights.Dispose();
            job.BindPoses.Dispose();
            job.Vertices.Dispose();
            job.WorldPositions.Dispose();
        }
        
        public static void TransformLocalToWorld(MeshFilter filter, Vector3[] worldVertices)
        {
            MeshLocalToWorldJob job = new MeshLocalToWorldJob();
            Mesh mesh = filter.sharedMesh;
            job.LocalToWorldMatrix = filter.transform.localToWorldMatrix;
            job.Vertices = new NativeArray<Vector3>(mesh.vertices, Allocator.TempJob);
            job.WorldPositions = new NativeArray<Vector3>(worldVertices, Allocator.TempJob);
            JobHandle handle = job.Schedule(worldVertices.Length, 1);
            handle.Complete();
            job.WorldPositions.CopyTo(worldVertices);
            job.Vertices.Dispose();
            job.WorldPositions.Dispose();
        }
    }
    

}

