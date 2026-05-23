
using System;
using System.Runtime.CompilerServices;

namespace Script.Framework.Else
{
    /// <summary>
    /// 真机上，读写要4倍快于系统List。编辑器模式下只快一点
    /// </summary>
    public class SList<T>
    {
        public T[] List;

        public int Count;

        // 定义索引器
        public T this[int index]
        {
            get
            {
                return List[index];
            }
            set
            {
                List[index] = value;
            }
        }

        public SList(int max_count)
        {
            List = new T[max_count];
            Count = 0;
        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            Count = 0;
        }


        /// <summary>
        /// 看要不要设计自动扩容。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(T t)
        {
            List[Count++] = t;
        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Pop()
        {
            return List[--Count];
        }



    }

    /// <summary>
    /// 建议不要struct，否则Equals要面对拆装箱问题，也能避免但是字典性能下降
    /// </summary>
    [Serializable]
    public class Tuple4Long
    {
        public ulong L0;
        public ulong L1;
        public ulong L2;
        public ulong L3;

        public Tuple4Long(ulong l0, ulong l1, ulong l2, ulong l3)
        {
            L0 = l0; L1 = l1; L2 = l2; L3 = l3;
        }

        

        public override bool Equals(object obj)
        {
            return obj is Tuple4Long other
                && L0 == other.L0
                && L1 == other.L1
                && L2 == other.L2
                && L3 == other.L3;
        }

        public override int GetHashCode()
        {
            var hash = L0.GetHashCode();
            hash = hash * 31 + L1.GetHashCode();
            hash = hash * 31 + L2.GetHashCode();
            hash = hash * 31 + L3.GetHashCode();
            return hash;
        }

    }

}