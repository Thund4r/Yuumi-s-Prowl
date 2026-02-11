using UnityEngine;
using System.Collections.Generic;

namespace YuumisProwl.Utilities
{
    /// <summary>
    /// Generic object pool for reusing GameObjects to avoid instantiation/destruction costs.
    /// Critical for mobile performance - eliminates garbage collection spikes.
    /// </summary>
    public class ObjectPool<T> where T : MonoBehaviour
    {
        private Queue<T> pool = new Queue<T>();
        private T prefab;
        private Transform parent;
        private int initialSize;

        public int PoolSize => pool.Count;

        /// <summary>
        /// Creates a new object pool.
        /// </summary>
        /// <param name="prefab">The prefab to pool</param>
        /// <param name="initialSize">Number of objects to pre-instantiate</param>
        /// <param name="parent">Parent transform for pooled objects</param>
        public ObjectPool(T prefab, int initialSize = 10, Transform parent = null)
        {
            this.prefab = prefab;
            this.initialSize = initialSize;
            this.parent = parent;

            // Pre-populate the pool
            for (int i = 0; i < initialSize; i++)
            {
                T obj = CreateNewObject();
                Return(obj);
            }
        }

        /// <summary>
        /// Gets an object from the pool. Creates a new one if pool is empty.
        /// </summary>
        public T Get()
        {
            T obj;

            if (pool.Count > 0)
            {
                obj = pool.Dequeue();
                obj.gameObject.SetActive(true);
            }
            else
            {
                obj = CreateNewObject();
            }

            return obj;
        }

        /// <summary>
        /// Returns an object to the pool.
        /// </summary>
        public void Return(T obj)
        {
            if (obj == null) return;

            obj.gameObject.SetActive(false);
            pool.Enqueue(obj);
        }

        /// <summary>
        /// Creates a new instance of the pooled object.
        /// </summary>
        private T CreateNewObject()
        {
            T obj = Object.Instantiate(prefab, parent);
            obj.gameObject.SetActive(false);
            return obj;
        }

        /// <summary>
        /// Clears the pool and destroys all objects.
        /// </summary>
        public void Clear()
        {
            while (pool.Count > 0)
            {
                T obj = pool.Dequeue();
                if (obj != null)
                {
                    Object.Destroy(obj.gameObject);
                }
            }
        }

        /// <summary>
        /// Preloads additional objects into the pool.
        /// </summary>
        public void Preload(int count)
        {
            for (int i = 0; i < count; i++)
            {
                T obj = CreateNewObject();
                Return(obj);
            }
        }
    }
}
