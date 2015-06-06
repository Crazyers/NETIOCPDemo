using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AsyncSocketServer
{
    /// <summary>
    /// AsyncSocketUserToken的栈，是一个Stack
    /// 使用固定大小的缓存
    /// </summary>
    public class AsyncSocketUserTokenPool
    {
        private Stack<AsyncSocketUserToken> m_pool;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="capacity"></param>
        public AsyncSocketUserTokenPool(int capacity)
        {
            m_pool = new Stack<AsyncSocketUserToken>(capacity);
        }

        public void Push(AsyncSocketUserToken item)
        {
            if (item == null)
            {
                throw new ArgumentException("Items added to a AsyncSocketUserToken cannot be null");
            }
            lock (m_pool)
            {
                m_pool.Push(item);
            }
        }

        public AsyncSocketUserToken Pop()
        {
            lock (m_pool)
            {
                return m_pool.Pop();
            }
        }

        public int Count
        {
            get { return m_pool.Count; }
        }
    }

    /// <summary>
    /// 已经连接的Socket的列表
    /// </summary>
    public class AsyncSocketUserTokenList : Object
    {
        private List<AsyncSocketUserToken> m_list;

        public AsyncSocketUserTokenList()
        {
            m_list = new List<AsyncSocketUserToken>();
        }

        public void Add(AsyncSocketUserToken userToken)
        {
            lock(m_list)
            {
                m_list.Add(userToken);
            }
        }

        public void Remove(AsyncSocketUserToken userToken)
        {
            lock (m_list)
            {
                m_list.Remove(userToken);
            }
        }

        public int Count()
        {
            return m_list.Count;
        }

        public void CopyList(ref AsyncSocketUserToken[] array)
        {
            lock (m_list)
            {
                array = new AsyncSocketUserToken[m_list.Count];
                m_list.CopyTo(array);
            }
        }
    }
}
