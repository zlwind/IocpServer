using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;

namespace IOCP
{
    /// <summary>
    /// SocketAsyncEventArgs对象池
    /// </summary>
    class SocketAsyncEventArgsPool
    {
        //声明栈
        Stack<SocketAsyncEventArgs> m_pool;
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="capacity">池子容量</param>
        public SocketAsyncEventArgsPool(int capacity)
        {
            m_pool = new Stack<SocketAsyncEventArgs>(capacity);
        }
        /// <summary>
        /// 将SocketAsyncEventArgs对象压入池中
        /// </summary>
        /// <param name="item">SocketAsyncEventArgs对象</param>
        public void Push(SocketAsyncEventArgs item)
        {
            if (item == null)
            { throw new ArgumentException("Items added to a SocketAsyncEventArgsPool cannot be null"); }
            lock (m_pool)
            {
                m_pool.Push(item);
            }
        }
        /// <summary>
        /// 从池子中获取一个SocketAsyncEventArgs对象
        /// </summary>
        /// <returns></returns>
        public SocketAsyncEventArgs Pop()
        {
            lock (m_pool)
            {
               return m_pool.Pop();
            }
        }
        /// <summary>
        /// 获取池子大小
        /// </summary>
        public int Count
        {
            get { return m_pool.Count; }
        }
            
    }
}
