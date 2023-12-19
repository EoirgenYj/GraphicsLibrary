using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MeshSimplifyTool
{
    
    public interface IHeapNode
    {
        int HeapIndex { get; set; }
    }
    
    public class Heap<T> where T : IComparable<T>, IHeapNode
    {
        protected List<T> m_HeapList;

        public delegate bool Compare<T>(T a, T b);

        protected Compare<T> m_CompareFunc;

        protected Heap(Compare<T> func)
        {
            m_CompareFunc = func;
            m_HeapList = new List<T>();
        }

        protected Heap(Compare<T> func, List<T> heapList)
        {
            m_CompareFunc = func;
            m_HeapList = heapList;
            for (int i = heapList.Count / 2; i >= 0; i--)
            {
                Heapify(i);
            }
        }

        protected virtual void Heapify(int index)
        {
            int left = Left(index);
            int right = Right(index);
            int move = index;
            int size = m_HeapList.Count;

            if (left < size && m_CompareFunc(m_HeapList[left], m_HeapList[index]))
            {
                move = left;
            }

            if (right < size && m_CompareFunc(m_HeapList[right], m_HeapList[move]))
            {
                move = right;
            }

            if (move != index)
            {
                Swap(index, move);
                Heapify(move);
            }
        }

        protected static int Parent(int index)
        {
            return (index - 1) / 2;
        }
        
        protected static int Left(int index)
        {
            return 2 * index + 1;
        }

        protected static int Right(int index)
        {
            return 2 * index + 2;
        }

        protected void Swap(int source, int target)
        {
            // T temp = m_HeapList[source];
            // m_HeapList[source] = m_HeapList[target];
            // m_HeapList[target] = temp;
            
            (m_HeapList[source], m_HeapList[target]) = (m_HeapList[target], m_HeapList[source]);
            m_HeapList[source].HeapIndex = source;
            m_HeapList[target].HeapIndex = target;
        }

        public int Size()
        {
            return m_HeapList.Count;
        }

        public T Top()
        {
            return m_HeapList[0];
        }
        
        //取出堆顶元素
        public T ExtractTop()
        {
            if (m_HeapList.Count == 0)
            {
                throw new Exception("Heap underflow");
            }

            T top = Top();
            int last = m_HeapList.Count - 1;
            m_HeapList[0] = m_HeapList[last];
            m_HeapList[0].HeapIndex = 0;
            //取出堆根，然后从根末尾取一个元素插入堆顶，然后重新堆排序，保证堆继续是大根堆/小根堆
            m_HeapList.RemoveAt(last);
            Heapify(0);
            return top;
        }

        public void Insert(T element)
        {
            m_HeapList.Add(element);
            int index = m_HeapList.Count - 1;
            element.HeapIndex = index;
            ModifyElement(index, element);
        }

        public virtual void ModifyElement(int index, T element)
        {
            m_HeapList[index] = element;
            element.HeapIndex = index;
            Heapify(index);
            if (index != element.HeapIndex) return;

            m_HeapList[index] = element;
            element.HeapIndex = index;
            //如果修改之后index无变化，则手动比较一遍，确保下标是正确的
            
            int parent = Parent(index);
            while (index > 0 && m_CompareFunc(m_HeapList[index], m_HeapList[parent]))
            {
                Swap(index, parent);
                index = parent;
                parent = Parent(index);
            }
        }

        //用于大根堆
        private static bool Larger(T a, T b)
        {
            return a.CompareTo(b) > 0;
        }
        //用于小根堆
        private static bool Smaller(T a, T b)
        {
            return a.CompareTo(b) < 0;
        }

        
        public static Heap<T> CreateMaxHeap()
        {
            return new Heap<T>(Larger);
        }

        public static Heap<T> CreateMaxHeap(List<T> list)
        {
            return new Heap<T>(Larger, list);
        }
        
        public static Heap<T> CreateMinHeap()
        {
            return new Heap<T>(Smaller);
        }

        public static Heap<T> CreateMinHeap(List<T> list)
        {
            return new Heap<T>(Smaller, list);
        }
    }

}
