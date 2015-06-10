using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Apache.NMS;
using AsyncSocketServer.AsyncSocketCore;
using log4net.Repository.Hierarchy;

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
        public ConcurrentBag<AsyncSocketUserToken> TokenBag = new ConcurrentBag<AsyncSocketUserToken>(); 
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
                userToken.SendEventArgs.Completed += IO_SendCompleted;//单独的发送过程
                m_asyncSocketUserTokenPool.Push(userToken);
            }
        }

        public void Start(IPEndPoint localEndPoint)
        {
            listenSocket = new Socket(localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listenSocket.Bind(localEndPoint);
            listenSocket.Listen(m_numConnections);//m_numConnections--挂起连接队列的最大长度(能接收多少个连接，如果超出则拒绝)
            Program.Logger.InfoFormat("Start listen socket {0} success", localEndPoint.ToString());
            StartAccept(null);
            m_daemonThread = new DaemonThread(this);
        }

        /// <summary>
        /// 开始接收链接
        /// </summary>
        /// <param name="e">接收的Socket上下文对象</param>
        public void StartAccept(SocketAsyncEventArgs e)
        {
            if (e == null)
            {
                e = new SocketAsyncEventArgs();
                e.Completed += AcceptEventArg_Completed;
            }
            else
            {
                e.AcceptSocket = null; //释放上次绑定的Socket，等待下一个Socket连接
            }

            m_maxNumberAcceptedClients.WaitOne(); //获取信号量,阻止当前线程，直到当前 System.Threading.WaitHandle 收到信号。
            //开始一个异步操作来接受一个传入的连接尝试。
            //如果 I/O 操作挂起，将返回 true。操作完成时，将引发 e 参数的 System.Net.Sockets.SocketAsyncEventArgs.Completed事件。
            //如果 I/O 操作同步完成，将返回 false。将不会引发 e 参数的 System.Net.Sockets.SocketAsyncEventArgs.Completed事件，
            //并且可能在方法调用返回后立即检查作为参数传递的 e 对象以检索操作的结果。
            bool willRaiseEvent = listenSocket.AcceptAsync(e);
            if (!willRaiseEvent)
            {
                ProcessAccept(e);
            }
        }

        #region 异步I/O完成时的响应事件
        /// <summary>
        /// 接受连接响应事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void AcceptEventArg_Completed(object sender, SocketAsyncEventArgs e)
        {
            try
            {
                ProcessAccept(e);
            }
            catch (Exception E)
            {
                Program.Logger.ErrorFormat("Accept client {0} error, message: {1}", e.AcceptSocket, E.Message);
                Program.Logger.Error(E.StackTrace);
            }
        }
        
        /// <summary>
        /// This method is called whenever a receive or send operation is completed on a socket 
        /// SocketAsyncEventArgs编程模式不支持设置同时工作线程个数，使用的NET的IO线程，
        /// 由NET底层提供，这点和直接使用完成端口API编程不同。
        /// NET底层IO线程也是每个异步事件都是由不同的线程返回到Completed事件，
        /// 因此在Completed事件需要对用户对象进行加锁，避免同一个用户对象同时触发两个Completed事件。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e">SocketAsyncEventArg associated with the completed receive operation</param>
        void IO_Completed(object sender, SocketAsyncEventArgs e)
        {
            AsyncSocketUserToken userToken = e.UserToken as AsyncSocketUserToken;
            userToken.ActiveDateTime = DateTime.Now;
            try
            {
                lock (userToken)
                {
                    if (e.LastOperation == SocketAsyncOperation.Receive)
                        ProcessReceive(e);
                    else if (e.LastOperation == SocketAsyncOperation.Send)
                    {
                        ProcessSend(e);
                    }
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
        /// 异步发送完成
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void IO_SendCompleted(object sender, SocketAsyncEventArgs e)
        {
            AsyncSocketUserToken userToken = e.UserToken as AsyncSocketUserToken;
            userToken.ActiveDateTime = DateTime.Now;
            try
            {
                lock (userToken)
                {
                    if (e.LastOperation == SocketAsyncOperation.Send)
                    {
                        ProcessSend(e);
                    }
                    else
                        throw new ArgumentException("The last operation completed on the socket was not send");
                }
            }
            catch (Exception E)
            {
                Program.Logger.ErrorFormat("IO_Completed {0} error, message: {1}, trace:{2}", userToken.ConnectSocket, E.Message, E.StackTrace);
                //Program.Logger.Error(E.StackTrace);
            }
        }
        #endregion I/O完成时的响应事件

        /// <summary>
        /// 接收链接后的处理过程
        /// </summary>
        /// <param name="e"></param>
        private void ProcessAccept(SocketAsyncEventArgs e)
        {
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
            }

            // Accept the next connection request
            StartAccept(e);
        }

        /// <summary>
        /// This method is invoked when an asynchronous receive operation completes. 
        /// 处理接收数据
        /// </summary>
        /// <param name="e"></param>
        private void ProcessReceive(SocketAsyncEventArgs e)
        {
            AsyncSocketUserToken userToken = e.UserToken as AsyncSocketUserToken;
            if (userToken.ConnectSocket == null)
                return;
            userToken.ActiveDateTime = DateTime.Now;
            if (userToken.ReceiveEventArgs.BytesTransferred > 0 && userToken.ReceiveEventArgs.SocketError == SocketError.Success)
            {
                int count = userToken.ReceiveEventArgs.BytesTransferred;
                if (count > 0)
                {
                    //处理接收数据（写入ActiveMQ消息队列）
                    //IBytesMessage msg = Program.Producer.CreateBytesMessage(userToken.ReceiveEventArgs.Buffer);
                    //Program.Producer.Send(msg, MsgDeliveryMode.NonPersistent, MsgPriority.Normal, TimeSpan.MinValue);
                    Monitor.IntReceivedMsg ++;//记录接收到的数目
                    bool willRaiseEvent = userToken.ConnectSocket.ReceiveAsync(userToken.ReceiveEventArgs);
                    if (!willRaiseEvent) //如果 I/O 操作同步完成(发送接收都完成)，将返回 false。进行下一次接收
                        ProcessReceive(userToken.ReceiveEventArgs);
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
                AsyncSendBufferManager bufferManager = userToken.SendBufferManager;
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

        /// <summary>
        /// 针对某一个UserToken进行非异步发送数据
        /// </summary>
        /// <param name="token"></param>
        /// <param name="buffer"></param>
        /// <returns></returns>
        public bool Send(AsyncSocketUserToken token, byte[] buffer)
        {
            try
            {
                if (token.ConnectSocket != null)
                {
                    //Program.Logger.ErrorFormat("发送完成 , buffer大小: {0}", buffer.Length);
                    token.ConnectSocket.Send(buffer);
                }
                else
                {
                    Program.Logger.ErrorFormat("发送报错 Socket 为空");
                }
                return true;
            }
            catch (Exception E)
            {
                CloseClientSocket(token);
                Program.Logger.ErrorFormat("发送报错 , message: {0}, trace:{1}", E.Message, E.StackTrace);
                return false;
            }
        }

        public bool SendAsyncEvent(Socket connectSocket, SocketAsyncEventArgs sendEventArgs, byte[] buffer, int offset, int count)
        {
            if (connectSocket == null)
                return false;
            sendEventArgs.SetBuffer(buffer, offset, count);//设置要用于异步套接字方法的数据缓冲区。
            // 如果 I/O 操作同步完成，将返回 false。在这种情况下，将不会引发 e 参数的 System.Net.Sockets.SocketAsyncEventArgs.Completed事件，
            // 并且可能在方法调用返回后立即检查作为参数传递的 e 对象以检索操作的结果。
            bool willRaiseEvent = connectSocket.SendAsync(sendEventArgs);
            if (!willRaiseEvent)//已经完成
            {
                return ProcessSend(sendEventArgs);
            }
            else
                return true;
        }

        /// <summary>
        /// 服务其主叫调用异步发送
        /// 经常报错"现在已经正在使用此 SocketAsyncEventArgs 实例进行异步套接字操作。
        /// 英文错误：An asynchronous socket operation is already in progress using this SocketAsyncEventArgs instance
        /// 解决方法：http://stackoverflow.com/questions/9170126/how-to-use-socket-sendasync-to-send-large-data
        /// </summary>
        public bool SendAsyncEventByServerCall(Socket connectSocket, SocketAsyncEventArgs sendEventArgs, byte[] buffer, int offset, int count)
        {
            if (connectSocket == null)
                return false;
            //AsyncSocketUserToken token = sendEventArgs.UserToken as AsyncSocketUserToken;
            
            sendEventArgs.SetBuffer(buffer, offset, count);//设置要用于异步套接字方法的数据缓冲区。
            // 如果 I/O 操作同步完成，将返回 false。在这种情况下，将不会引发 e 参数的 System.Net.Sockets.SocketAsyncEventArgs.Completed事件，
            // 并且可能在方法调用返回后立即检查作为参数传递的 e 对象以检索操作的结果。
            bool willRaiseEvent = connectSocket.SendAsync(sendEventArgs);
            if (!willRaiseEvent)//已经完成
            {
                return true;
                //return ProcessSend(sendEventArgs);
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
