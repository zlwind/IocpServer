using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Threading;

namespace IOCP
{
    /// <summary>
    /// IOCP Socket服务器
    /// </summary>
    class IocpServer:IDisposable
    {
        #region 
        private int _maxClient;//服务器程序允许的最大客户端连接数

        private Socket listenSocket;//监听socket

        private int _clientCount;//当前连接的客户端数

        private int _bufferSize=1024;//缓冲区的大小

        BufferManager _bufferManager;//缓存管理

        Semaphore _maxAcceptClient;//信号量 并发客户端连接

        SocketAsyncEventArgsPool _objectPool;//SocketAsyncEventArgs对象池

        const int opsToPreAlloc = 2;
        #endregion

        private bool disposed = false;//指示资源释放

        #region 属性
        /// <summary>
        /// 服务器是否在运行
        /// </summary>
        public bool IsRunning { get; private set; }
        /// <summary>
        /// 监听的ip地址
        /// </summary>
        public IPAddress Address { get; private set; }
        /// <summary>
        /// 监听的端口
        /// </summary>
        public int Port { get; private set; }
        /// <summary>
        /// 通信使用的编码
        /// </summary>
        public Encoding Encoding { get; set; }

        #endregion

        #region 构造函数

        public IocpServer(int listenPort, int maxClient)
            : this(IPAddress.Any, listenPort, maxClient)
        { }

        public IocpServer(IPEndPoint localEP, int maxClient) 
            : this(localEP.Address, localEP.Port, maxClient)
        { }

        public IocpServer(IPAddress localIPAddress, int listenPort, int maxClient)
        {
            this.Address = localIPAddress;//本地ip
            this.Port = listenPort;//本地端口
            this.Encoding = Encoding.Default;//编码

            _maxClient = maxClient;//最大客户端连接数

            listenSocket = new Socket(Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);//声明监听socket

            _bufferManager = new BufferManager(_bufferSize*_maxClient*opsToPreAlloc,_bufferSize);//声明缓存

            _objectPool = new SocketAsyncEventArgsPool(_maxClient);//声明SocketAsyncEventArgs池子大小，用于从里面取出读写socket数据

            _maxAcceptClient = new Semaphore(_maxClient, _maxClient);//信号量 声明可以并发连接的最大个数
          
        }
        #endregion

        #region 初始化
        /// <summary>
        /// 初始化函数
        /// </summary>
        public void Init()
        {

            _bufferManager.InitBuffer();//缓存初始化

            //声明SocketAsyncEventArgs类
            //用于socket异步读写
            //每一个readWriteEventArg对应_bufferSize大小的缓存管理
            //将readWriteEventArg压入栈中，读写时将其取出
            SocketAsyncEventArgs readWriteEventArg;
            for (int i = 0; i < _maxClient; i++)
            {
                readWriteEventArg = new SocketAsyncEventArgs();
                readWriteEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(OnIOCompleted);
                readWriteEventArg.UserToken = null;

                _bufferManager.SetBuffer(readWriteEventArg);
                _objectPool.Push(readWriteEventArg);

            }
        }
        #endregion

        #region 启动
        /// <summary>
        /// 启动
        /// </summary>
        public void Start()
        {
            if (!IsRunning)
            {
                //初始化
                Init();
                //运行标志位置位
                IsRunning = true;
                //监听socket绑定ip 端口
                IPEndPoint localEndPoint = new IPEndPoint(Address,Port);
                listenSocket = new Socket(localEndPoint.AddressFamily,SocketType.Stream,ProtocolType.Tcp);
                if (localEndPoint.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    listenSocket.SetSocketOption(SocketOptionLevel.IPv6, (SocketOptionName)27, false);
                    listenSocket.Bind(new IPEndPoint(IPAddress.IPv6Any,localEndPoint.Port));
                }
                else
                {
                    listenSocket.Bind(localEndPoint);
                }
                //开始监听
                listenSocket.Listen(this._maxClient);
                //在监听socket上投递一个接受连接请求
                StartAccept(null);
            }
        }
        #endregion

      

        #region 接收一个Socket连接
        private void StartAccept(SocketAsyncEventArgs asyniar)
        {
            //第一次连接，创建用于连接SocketAsyncEventArgs的对象，注册连接完成方法
            if (asyniar == null)
            {
                asyniar = new SocketAsyncEventArgs();
                asyniar.Completed += new EventHandler<SocketAsyncEventArgs>(OnAcceptCompleted);
            }
            //不是第一次连接，将AcceptSocket=null，方便再次连接获取新的AcceptSocket对象
            else
            {
                asyniar.AcceptSocket = null;
            }
            //信号量，控制最大客户端连接个数
            _maxAcceptClient.WaitOne();
            //如果 I/O 操作挂起，则为 true。 操作完成时，将引发 e 参数的 Completed 事件。
            //如果 I/O 操作同步完成，则为 false。 将不会引发 e 参数的 Completed 事件，
            //并且可能在方法调用返回后立即检查作为参数传递的 e 对象以检索操作的结果。
            if (!listenSocket.AcceptAsync(asyniar))
            {
                ProcessAccept(asyniar);
            }
        }
       
        /// <summary>
        /// accept完成时回调函数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnAcceptCompleted(object sender, SocketAsyncEventArgs e)
        {
            ProcessAccept(e);
        }
        /// <summary>
        /// 接收一个连接处理函数
        /// </summary>
        /// <param name="e"></param>
        private void ProcessAccept(SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                //连接成功，获取连接的socket
                Socket s = e.AcceptSocket;
                if (s.Connected)
                {
                    try
                    {
                        //客户端连接个数原子操作加1
                        Interlocked.Increment(ref _clientCount);
                        //从读写socket栈中取出一个SocketAsyncEventArgs对象，用于读写操作
                        SocketAsyncEventArgs asyniar = _objectPool.Pop();
                        //将连接成功的socket赋值UserToken
                        asyniar.UserToken = s;
                        Console.WriteLine(String.Format("客户 {0} 连入, 共有 {1} 个连接。", s.RemoteEndPoint.ToString(), _clientCount));
                        //如果 I/O 操作挂起，则为 true。 操作完成时，将引发 e 参数的 Completed 事件。
                        //如果 I/O 操作同步完成，则为 false。 将不会引发 e 参数的 Completed 事件，
                        //并且可能在方法调用返回后立即检查作为参数传递的 e 对象以检索操作的结果。
                        if (!s.ReceiveAsync(asyniar))
                        {
                            //同步接收时处理接收完成事件
                            ProcessReceive(e);
                        }
                    }
                    catch(SocketException ex)
                    {
                        Console.WriteLine(String.Format("接收客户 {0} 数据出错, 异常信息： {1} 。", s.RemoteEndPoint, ex.ToString()));
                    }
                    //投递下一个接受请求
                    StartAccept(e);
                }
            }
        }，
        #endregion

        #region 接收数据
        /// <summary>
        /// 接收数据完成时 处理方法
        /// </summary>
        /// <param name="e">与接收完成操作相关联的SocketAsyncEventArg对象</param>
        private void ProcessReceive(SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                if (e.BytesTransferred > 0)
                {
                    //获取socket对象
                    Socket s = (Socket)e.UserToken;
                    //处理接收的数据
                    if (s.Available == 0)
                    {
                        byte[] data = new byte[e.BytesTransferred];
                       // e.SetBuffer(e.Offset, e.BytesTransferred);
                        Array.Copy(e.Buffer, e.Offset, data, 0, data.Length);

                        string info = Encoding.Default.GetString(data);
                        Console.WriteLine(String.Format("收到 {0} 数据为 {1}", s.RemoteEndPoint.ToString(), info));
                    }
                    //继续循环接收数据
                    if (!s.ReceiveAsync(e))
                    { ProcessReceive(e); }
                }
                else
                { CloseClientSocket(e); }
               
            }
        }
        #endregion

        #region 端口完成回调函数
        private void OnIOCompleted(object sender, SocketAsyncEventArgs e)
        {
            switch (e.LastOperation)
            {
                //case SocketAsyncOperation.Accept:
                //    ProcessAccept(e);
                //    break;
                case SocketAsyncOperation.Receive:
                    ProcessReceive(e);
                    break;
                case SocketAsyncOperation.Send:
                    break;
                default:
                    throw new ArgumentException("The last operation completed on the socket was not a receive or send");

            }
        }
        #endregion

        #region 关闭socket连接
        /// <summary>
        /// 关闭socket连接
        /// </summary>
        /// <param name="e"></param>
        private void CloseClientSocket(SocketAsyncEventArgs e)
        {
            Console.WriteLine(String.Format("客户 {0} 断开连接!", ((Socket)e.UserToken).RemoteEndPoint.ToString()));
            Socket s = e.UserToken as Socket;
            CloseClientSocket(s, e);
        }
        /// <summary>
        /// 关闭socket连接
        /// </summary>
        /// <param name="s"></param>
        /// <param name="e"></param>
        private void CloseClientSocket(Socket s, SocketAsyncEventArgs e)
        {
            try
            { s.Shutdown(SocketShutdown.Send); }
            catch
            { ;}
            finally
            {
                //读写socket关闭
                s.Close();
            }
            //客户端连接个数原子操作减1
            Interlocked.Decrement(ref _clientCount);
            //信号量释放一个socket连接
            _maxAcceptClient.Release();
            //将socket对应的SocketAsyncEventArgs对象返回池子中供其他线程调用
            _objectPool.Push(e);
        }
        #endregion

        #region 停止服务
        /// <summary>
        /// 停止服务
        /// </summary>
        public void Stop()
        {
            if (IsRunning)
            {
                IsRunning = false;
                //关闭监听socket
                listenSocket.Close();
            }
        }
        #endregion


        #region 资源释放
        //实现IDisposable接口方法
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        //参数disposing表示是否需要释放那些实现IDisposable接口的托管对象。
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                //释放非托管资源
                if (disposing)
                {
                    try
                    {
                        //释放监听socket
                        Stop();
                        if (listenSocket != null)
                        { listenSocket = null; }
                    }
                    catch (SocketException ex)
                    { ;}
                }
                //让类型知道自己已经被释放
                disposed = true;
            }
        }
        #endregion

    }
}
