using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace MeshSimplifyTool
{
    public static class UnsafeUtil
    {
        public static unsafe float UintToFloat(uint u)
        {
            /*
             * float *p = (float*)&u;
             * return *p
             */
            return *(float*)&u;    
        }

        public static unsafe uint FloatToUint(float f)
        {
            return *(uint*)&f;    
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct ArrayHeader
        {
            internal IntPtr type;
            internal int length;
        }

        private static unsafe void HackArraySizeCall<TA>(TA[] array, ArrayHeader* header, int size, Action<TA[]> func)
        {
            int originalLength = header->length;
            header->length = size;
            try
            {
                func(array);
            }
            finally
            {
                header->length = originalLength;
            }
        }

        public static unsafe void IntegerHackArraySizeCall(int[] array, int size, Action<int[]> func)
        {
            if (array != null && size < array.Length)
            {
                fixed (void* p = array)
                {
                    HackArraySizeCall(array, (ArrayHeader*)p - 1, size, func);
                    return;
                }
            }
            
            //fallback
            func(array);
        }

        public static unsafe void Vector2HackArraySizeCall(Vector2[] array, int size, Action<Vector2[]> func)
        {
            if (array != null && size < array.Length)
            {
                fixed (void* p = array)
                {
                    HackArraySizeCall(array, (ArrayHeader*)p - 1, size, func);
                    return;
                }
            }

            func(array);
        }
        
        public static unsafe void Vector3HackArraySizeCall( Vector3[] array, int size, Action<Vector3[]> func)
        {
            if (array != null && size < array.Length)
            {
                fixed (void* p = array)
                {
                    HackArraySizeCall(array, (ArrayHeader*)p - 1, size, func);
                    return;
                }
            }
            
            func(array);
        }
        
        public static unsafe void Vector4HackArraySizeCall( Vector4[] array, int size, Action<Vector4[]> func)
        {
            if (array != null && size < array.Length)
            {
                fixed (void* p = array)
                {
                    HackArraySizeCall(array, (ArrayHeader*)p - 1, size, func);
                    return;
                }
            }
            
            func(array);
        }
        
        public static unsafe void Color32HackArraySizeCall( Color32[] array, int size, Action<Color32[]> func)
        {
            if (array != null && size < array.Length)
            {
                fixed (void* p = array)
                {
                    HackArraySizeCall(array, (ArrayHeader*)p - 1, size, func);
                    return;
                }
            }
            
            func(array);
        }
        
        public static unsafe void BoneWeightHackArraySizeCall( BoneWeight[] array, int size, Action<BoneWeight[]> func)
        {
            if (array != null && size < array.Length)
            {
                fixed (void* p = array)
                {
                    HackArraySizeCall(array, (ArrayHeader*)p - 1, size, func);
                    return;
                }
            }
            
            func(array);
        }

        public static unsafe T UncheckReadArrayElement<T>(NativeArray<T> nativeArray, int index) where T : struct
        {
            void* ptr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(nativeArray);
            return Unity.Collections.LowLevel.Unsafe.UnsafeUtility.ReadArrayElement<T>(ptr, index);
        }
    }    
}


