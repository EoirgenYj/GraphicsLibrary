using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MeshSimplifyTool
{
    public class RelevanceSphere
    {
        public bool Expanded;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        public float Relevance;

        public RelevanceSphere()
        {
            Scale = Vector3.one;
        }

        public void SetDefault(Transform target, float relevance)
        {
            Expanded = true;
            Position = target.position + Vector3.up;
            Rotation = target.rotation;
            Scale = Vector3.one;
            Relevance = relevance;
        }
    }    
}


