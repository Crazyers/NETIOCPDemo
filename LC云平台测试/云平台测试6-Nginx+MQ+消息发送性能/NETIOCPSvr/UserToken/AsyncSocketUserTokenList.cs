using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AsyncSocketServer.AsyncSocketCore
{
    /// <summary>
    /// 当前所有已经建立的链接的AsyncSocketUserToken的链表
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
            lock (m_list)
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
