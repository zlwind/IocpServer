using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;

namespace IOCP
{
    class BufferManager
    {
        int m_numBytes;//缓存的个数

        byte[] m_buffer;//缓存管理

        Stack<int> m_freeIndexPool;//缓存偏移量栈
        int m_currentIndex;//缓存偏移指针
        int m_bufferSize;//缓存大小

        public BufferManager(int totalBytes,int bufferSize)
        {
            m_numBytes = totalBytes;
            m_currentIndex = 0;
            m_bufferSize = bufferSize;
            m_freeIndexPool = new Stack<int>();
        }
        /// <summary>
        /// 分配缓存空间
        /// </summary>
        public void InitBuffer()
        {
            //创建一个大空间缓存，并进行分区
            //每一个SocketAsyncEventArg对象对应一个空间
            m_buffer = new byte[m_numBytes];
        }
        /// <summary>
        /// 设置缓存
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public bool SetBuffer(SocketAsyncEventArgs args)
        {
            if (m_freeIndexPool.Count > 0)
            {
                args.SetBuffer(m_buffer, m_freeIndexPool.Pop(), m_bufferSize);
            }
            else
            {
                if ((m_numBytes - m_bufferSize) < m_currentIndex)
                {
                    return false;
                }
                args.SetBuffer(m_buffer, m_currentIndex, m_bufferSize);
                m_currentIndex += m_bufferSize;
            }
            return true;
        }
        /// <summary>
        ///清除 SocketAsyncEventArg对象缓存
        ///将缓存偏移量重新放回偏移量栈中
        /// </summary>
        /// <param name="args">SocketAsyncEventArg对象</param>
        public void FreeBuffer(SocketAsyncEventArgs args)
        {
            m_freeIndexPool.Push(args.Offset);
            args.SetBuffer(null, 0, 0);
        }

    }
}
