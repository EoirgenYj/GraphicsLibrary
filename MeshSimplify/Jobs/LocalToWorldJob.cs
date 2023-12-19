using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace MeshSimplifyTool
{
    public struct SkinLocalToWorldJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Matrix4x4> Bones;
        [ReadOnly] public NativeArray<BoneWeight> BoneWeights;
        [ReadOnly] public NativeArray<Matrix4x4> BindPoses;
        [ReadOnly] public NativeArray<Vector3> Vertices;
        public NativeArray<Vector3> WorldPositions;

        public void Execute(int index)
        {
            BoneWeight boneWeight = BoneWeights[index];
            Vector4 vector4 = Vertices[index];
            vector4.w = 1;
            WorldPositions[index] = Bones[boneWeight.boneIndex0] * BindPoses[boneWeight.boneIndex0] * vector4 *
                                    boneWeight.weight0
                                    + Bones[boneWeight.boneIndex1] * BindPoses[boneWeight.boneIndex1] * vector4 *
                                    boneWeight.weight1
                                    + Bones[boneWeight.boneIndex2] * BindPoses[boneWeight.boneIndex2] * vector4 *
                                    boneWeight.weight2
                                    + Bones[boneWeight.boneIndex3] * BindPoses[boneWeight.boneIndex3] * vector4 *
                                    boneWeight.weight3;
        }
    }

    public struct MeshLocalToWorldJob : IJobParallelFor
    {
        public Matrix4x4 LocalToWorldMatrix;
        [ReadOnly] public NativeArray<Vector3> Vertices;
        public NativeArray<Vector3> WorldPositions;

        public void Execute(int index)
        {
            Vector4 vector4 = Vertices[index];
            vector4.w = 1;
            WorldPositions[index] = LocalToWorldMatrix * vector4;
        }
    }
}


