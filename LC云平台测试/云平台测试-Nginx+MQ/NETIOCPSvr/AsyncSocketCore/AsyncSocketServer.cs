using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using AsyncSocketServer.AsyncSocketCore;

namespace AsyncSocketServer
{
    /// <summary>
    /// 参考：https://msdn.microsoft.com/zh-cn/library/system.net.sockets.socketasynceventargs(v=vs.110).aspx
    /// </summary>
    public class AsyncSocketServer
    {
        private Socket listenSocket;
        private int m_numConnections; //最大支持连接个数
        private int m_receiveBufferSize; //每个连接接收缓存大小
        private Semaphore m_maxNumberAcceptedClients; //限制访问接收连接的线程数，用来控制最大并发数
        private AsyncSocketUserTokenPool m_asyncSocketUserTokenPool;
        private DaemonThread m_daemonThread;

        public int SocketTimeOutMS { get; set; }
        public AsyncSocketUserTokenList AsyncSocketUserTokenList { get; private set; }

        /// <summary>
        /// 启动服务
        /// </summary>
        /// <param name="numConnections"></param>
        public AsyncSocketServer(int numConnections)
        {
            m_numConnections = numConnections;
            m_receiveBufferSize = ProtocolConst.ReceiveBufferSize;

            m_asyncSocketUserTokenPool = new AsyncSocketUserTokenPool(numConnections);
            AsyncSocketUserTokenList = new AsyncSocketUserTokenList();
            m_maxNumberAcceptedClients = new Semaphore(numConnections, numConnections);
        }

        /// <summary>
        /// 初始化连接池
        /// </summary>
        public void Init()
        {
            for (int i = 0; i < m_numConnections; i++) //按照连接数建立读写对象
            {
                AsyncSocketUserToken userToken = new AsyncSocketUserToken(m_receiveBufferSize);
                userToken.ReceiveEventArgs.Completed += IO_Completed;
                userToken.SendEventArgs.Completed += IO_Completed;
                m_asyncSocketUserTokenPool.Push(userToken);
            }
        }

        public void Start(IPEndPoint localEndPoint)
        {
            listenSocket = new Socket(localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listenSocket.Bind(localEndPoint);
            listenSocket.Listen(m_numConnections);//m_numConnections--挂起连接队列的最大长度(能接收多少个连接，如果超出则拒绝)
            Program.Logger.InfoFormat("Start listen socket {0} success", localEndPoint.ToString());
            //for (int i = 0; i < 64; i++) //不能循环投递多次AcceptAsync，会造成只接收8000连接后不接收连接了
            StartAccept(null);
            m_daemonThread = new DaemonThread(this);
        }

        /// <summary>
        /// 开始接收链接
        /// </summary>
        /// <param name="acceptEventArgs">接收的Socket上下文对象</param>
        public void StartAccept(SocketAsyncEventArgs acceptEventArgs)
        {
            if (acceptEventArgs == null)
            {
                acceptEventArgs = new SocketAsyncEventArgs();
                acceptEventArgs.Completed += AcceptEventArg_Completed;
            }
            else
            {
                acceptEventArgs.AcceptSocket = null; //释放上次绑定的Socket，等待下一个Socket连接
            }

            m_maxNumberAcceptedClients.WaitOne(); //获取信号量,阻止当前线程，直到当前 System.Threading.WaitHandle 收到信号。
            //开始一个异步操作来接受一个传入的连接尝试。
            //如果 I/O 操作挂起，将返回 true。操作完成时，将引发 e 参数的 System.Net.Sockets.SocketAsyncEventArgs.Completed事件。
            //如果 I/O 操作同步完成，将返回 false。将不会引发 e 参数的 System.Net.Sockets.SocketAsyncEventArgs.Completed事件，
            //并且可能在方法调用返回后立即检查作为参数传递的 e 对象以检索操作的结果。
            bool willRaiseEvent = listenSocket.AcceptAsync(acceptEventArgs);
            if (!willRaiseEvent)
            {
                ProcessAccept(acceptEventArgs);
            }
        }

        /// <summary>
        /// 接受连接响应事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="acceptEventArgs"></param>
        void AcceptEventArg_Completed(object sender, SocketAsyncEventArgs acceptEventArgs)
        {
            try
            {
                ProcessAccept(acceptEventArgs);
            }
            catch (Exception E)
            {
                Program.Logger.ErrorFormat("Accept client {0} error, message: {1}", acceptEventArgs.AcceptSocket, E.Message);
                Program.Logger.Error(E.StackTrace);
            }
        }

        /// <summary>
        /// 接收链接后的处理过程
        /// </summary>
        /// <param name="e"></param>
        private void ProcessAccept(SocketAsyncEventArgs e)
        {
            //Program.Logger.InfoFormat("Client connection accepted. Local Address: {0}, Remote Address: {1}", acceptEventArgs.AcceptSocket.LocalEndPoint, acceptEventArgs.AcceptSocket.RemoteEndPoint);

            //从连接池中分配一个连接赋给当前连接
            AsyncSocketUserToken userToken = m_asyncSocketUserTokenPool.Pop();
            AsyncSocketUserTokenList.Add(userToken); //添加到正在连接列表
            userToken.ConnectSocket = e.AcceptSocket;
            userToken.ConnectDateTime = DateTime.Now;

            try
            {
                // As soon as the client is connected, post a receive to the connection
                bool willRaiseEvent = userToken.ConnectSocket.ReceiveAsync(userToken.ReceiveEventArgs);
                if (!willRaiseEvent)
                {
                    lock (userToken)
                    {
                        ProcessReceive(userToken.ReceiveEventArgs);
                    }
                }
            }
            catch (Exception E)
            {
                Program.Logger.ErrorFormat("Accept client {0} error, message: {1}, trace:{2}", userToken.ConnectSocket, E.Message, E.StackTrace);
                //Program.Logger.Error(E.StackTrace);                
            }

            // Accept the next connection request
            StartAccept(e);
        }

        /// <summary>
        /// This method is called whenever a receive or send operation is completed on a socket 
        /// SocketAsyncEventArgs编程模式不支持设置同时工作线程个数，使用的NET的IO线程，
        /// 由NET底层提供，这点和直接使用完成端口API编程不同。
        /// NET底层IO线程也是每个异步事件都是由不同的线程返回到Completed事件，
        /// 因此在Completed事件需要对用户对象进行加锁，避免同一个用户对象同时触发两个Completed事件。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="asyncEventArgs">SocketAsyncEventArg associated with the completed receive operation</param>
        void IO_Completed(object sender, SocketAsyncEventArgs asyncEventArgs)
        {
            AsyncSocketUserToken userToken = asyncEventArgs.UserToken as AsyncSocketUserToken;
            userToken.ActiveDateTime = DateTime.Now;
            try
            {
                lock (userToken)
                {
                    if (asyncEventArgs.LastOperation == SocketAsyncOperation.Receive)
                        ProcessReceive(asyncEventArgs);
                    else if (asyncEventArgs.LastOperation == SocketAsyncOperation.Send)
                        ProcessSend(asyncEventArgs);
                    else
                        throw new ArgumentException("The last operation completed on the socket was not a receive or send");
                }
            }
            catch (Exception E)
            {
                Program.Logger.ErrorFormat("IO_Completed {0} error, message: {1}, trace:{2}", userToken.ConnectSocket, E.Message, E.StackTrace);
                //Program.Logger.Error(E.StackTrace);
            }
        }

        /// <summary>
        /// This method is invoked when an asynchronous receive operation completes. 
        /// 处理接收数据
        /// </summary>
        /// <param name="receiveEventArgs"></param>
        private void ProcessReceive(SocketAsyncEventArgs receiveEventArgs)
        {
            AsyncSocketUserToken userToken = receiveEventArgs.UserToken as AsyncSocketUserToken;
            if (userToken.ConnectSocket == null)
                return;
            userToken.ActiveDateTime = DateTime.Now;
            if (userToken.ReceiveEventArgs.BytesTransferred > 0 && userToken.ReceiveEventArgs.SocketError == SocketError.Success)
            {
                int count = userToken.ReceiveEventArgs.BytesTransferred;
                //如果消息内容>0
                if (count > 0)
                {
                    //处理接收数据（写入消息队列）
                    bool blnProcessReceive = true; // userToken.AsyncSocketInvokeElement.ProcessReceive(userToken.ReceiveEventArgs.Buffer);
                    if (!blnProcessReceive) //如果处理数据返回失败，则断开连接
                    {
                        CloseClientSocket(userToken);
                    }
                    else //否则投递下次介绍数据请求
                    {
                        bool willRaiseEvent = userToken.ConnectSocket.ReceiveAsync(userToken.ReceiveEventArgs);
                        if (!willRaiseEvent) //如果 I/O 操作同步完成，将返回 false。
                            ProcessReceive(userToken.ReceiveEventArgs);
                    }
                }
                else
                {
                    bool willRaiseEvent = userToken.ConnectSocket.ReceiveAsync(userToken.ReceiveEventArgs);
                    if (!willRaiseEvent)
                        ProcessReceive(userToken.ReceiveEventArgs);
                }
            }
            else
            {
                CloseClientSocket(userToken);
            }
        }

        /// <summary>
        /// 发送消息（得先从队列里把要发送的数据拿出来）
        /// This method is invoked when an asynchronous send operation completes.  
        /// The method issues another receive on the socket to read any additional data sent from the client
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        private bool ProcessSend(SocketAsyncEventArgs e)
        {
            AsyncSocketUserToken userToken = e.UserToken as AsyncSocketUserToken;
            userToken.ActiveDateTime = DateTime.Now;
            if (e.SocketError == SocketError.Success)
            {
                AsyncSendBufferManager bufferManager = userToken.SendBuffer;
                bufferManager.ClearFirstPacket(); //清除已发送的包
                int offset = 0;
                int count = 0;
                if (bufferManager.GetFirstPacket(ref offset, ref count))
                {
                    return SendAsyncEvent(userToken.ConnectSocket, userToken.SendEventArgs, bufferManager.DynamicBufferManager.Buffer, offset, count);
                }
                else
                {
                    return true;
                }
                //return SendCallback();
            }
            else
            {
                CloseClientSocket(userToken);
                return false;
            }
        }

        public bool SendAsyncEvent(Socket connectSocket, SocketAsyncEventArgs sendEventArgs, byte[] buffer, int offset, int count)
        {
            if (connectSocket == null)
                return false;
            sendEventArgs.SetBuffer(buffer, offset, count);//设置要用于异步套接字方法的数据缓冲区。
            bool willRaiseEvent = connectSocket.SendAsync(sendEventArgs);
            if (!willRaiseEvent)
            {
                return ProcessSend(sendEventArgs);
            }
            else
                return true;
        }

        /// <summary>
        /// 关闭Socket
        /// </summary>
        /// <param name="userToken"></param>
        public void CloseClientSocket(AsyncSocketUserToken userToken)
        {
            if (userToken.ConnectSocket == null)
                return;
            string socketInfo = string.Format("Local Address: {0} Remote Address: {1}", userToken.ConnectSocket.LocalEndPoint, userToken.ConnectSocket.RemoteEndPoint);
            Program.Logger.InfoFormat("Client connection disconnected. {0}", socketInfo);
            try
            {
                userToken.ConnectSocket.Shutdown(SocketShutdown.Both);
            }
            catch (Exception E)
            {
                Program.Logger.ErrorFormat("CloseClientSocket Disconnect client {0} error, message: {1}", socketInfo, E.Message);
            }
            userToken.ConnectSocket.Close();
            userToken.ConnectSocket = null; //释放引用，并清理缓存，包括释放协议对象等资源

            m_maxNumberAcceptedClients.Release();
            m_asyncSocketUserTokenPool.Push(userToken);
            AsyncSocketUserTokenList.Remove(userToken);
        }
    }
}
