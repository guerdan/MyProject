using System;
using System.Collections.Generic;

namespace Script
{
    public interface IObject
    {
        /// <summary>
        /// 对象池回收时调用
        /// </summary>
        public void UnUse();

        /// <summary>
        /// 从对象池里拿出来用时调用
        /// </summary>
        public void ReUse();
    }

    public class ObjectPool<T> where T : IObject
    {
        private List<T> objects = new List<T>();

        // public T get()
        // {
        //     
        // }

        public void put(object args = null)
        {
        }
    }


    public abstract class AbstractExample : IObject
    {
        public abstract void ReUse();
        public abstract void UnUse();
    }

   
    
    
}