using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace AsyncSocketServer
{
    public class AsyncSocketServer
    {
        private Socket listenSocket;
        
        private int m_numConnections; //最大支持连接个数
        private int m_receiveBufferSize; //每个连接接收缓存大小
        private Semaphore m_maxNumberAcceptedClients; //限制访问接收连接的线程数，用来控制最大并发数

        public int SocketTimeOutMS { get; set; }

        private AsyncSocketUserTokenPool m_asyncSocketUserTokenPool;
        public AsyncSocketUserTokenList AsyncSocketUserTokenList { get; private set; }
        public LogOutputSocketProtocolMgr LogOutputSocketProtocolMgr { get; private set; }
        public UploadSocketProtocolMgr UploadSocketProtocolMgr { get; private set; }

        private DaemonThread m_daemonThread;

        public AsyncSocketServer(int numConnections)
        {
            m_numConnections = numConnections;
            m_receiveBufferSize = ProtocolConst.ReceiveBufferSize;

            m_asyncSocketUserTokenPool = new AsyncSocketUserTokenPool(numConnections);
            LogOutputSocketProtocolMgr = new LogOutputSocketProtocolMgr();
            AsyncSocketUserTokenList = new AsyncSocketUserTokenList();
            m_maxNumberAcceptedClients = new Semaphore(numConnections, numConnections);
            UploadSocketProtocolMgr = new UploadSocketProtocolMgr();
        }

        /// <summary>
        /// 初始化连接池
        /// </summary>
        public void Init()
        {
            AsyncSocketUserToken userToken;
            for (int i = 0; i < m_numConnections; i++) //按照连接数建立读写对象
            {
                userToken = new AsyncSocketUserToken(m_receiveBufferSize);
                userToken.ReceiveEventArgs.Completed += new EventHandler<SocketAsyncEventArgs>(IO_Completed);
                userToken.SendEventArgs.Completed += new EventHandler<SocketAsyncEventArgs>(IO_Completed);
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

        public void StartAccept(SocketAsyncEventArgs acceptEventArgs)
        {
            if (acceptEventArgs == null)
            {
                acceptEventArgs = new SocketAsyncEventArgs();
                acceptEventArgs.Completed += new EventHandler<SocketAsyncEventArgs>(AcceptEventArg_Completed);
            }
            else
            {
                acceptEventArgs.AcceptSocket = null; //释放上次绑定的Socket，等待下一个Socket连接
            }

            m_maxNumberAcceptedClients.WaitOne(); //获取信号量,阻止当前线程，直到当前 System.Threading.WaitHandle 收到信号。
            bool willRaiseEvent = listenSocket.AcceptAsync(acceptEventArgs);//开始一个异步操作来接受一个传入的连接尝试。
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
        /// 循环接受连接
        /// </summary>
        /// <param name="acceptEventArgs"></param>
        private void ProcessAccept(SocketAsyncEventArgs acceptEventArgs)
        {
            //Program.Logger.InfoFormat("Client connection accepted. Local Address: {0}, Remote Address: {1}", acceptEventArgs.AcceptSocket.LocalEndPoint, acceptEventArgs.AcceptSocket.RemoteEndPoint);

            //从连接池中分配一个连接赋给当前连接
            AsyncSocketUserToken userToken = m_asyncSocketUserTokenPool.Pop();
            AsyncSocketUserTokenList.Add(userToken); //添加到正在连接列表
            userToken.ConnectSocket = acceptEventArgs.AcceptSocket;
            userToken.ConnectDateTime = DateTime.Now;

            try
            {
                bool willRaiseEvent = userToken.ConnectSocket.ReceiveAsync(userToken.ReceiveEventArgs); //投递接收请求
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

            StartAccept(acceptEventArgs); //把当前异步事件释放，等待下次连接
        }

        /// <summary>
        /// SocketAsyncEventArgs编程模式不支持设置同时工作线程个数，使用的NET的IO线程，
        /// 由NET底层提供，这点和直接使用完成端口API编程不同。
        /// NET底层IO线程也是每个异步事件都是由不同的线程返回到Completed事件，
        /// 因此在Completed事件需要对用户对象进行加锁，避免同一个用户对象同时触发两个Completed事件。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="asyncEventArgs"></param>
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
                //（确定是什么协议，用于绑定上线客户端，以后来的就都走这个socket）存在Socket对象，并且没有绑定协议对象，则进行协议对象绑定
                if ((userToken.AsyncSocketInvokeElement == null) & (userToken.ConnectSocket != null)) 
                {
                    BuildingSocketInvokeElement(userToken);
                    count = count - 1;
                }
                if (userToken.AsyncSocketInvokeElement == null) //如果没有解析对象，提示非法连接并关闭连接
                {
                    Program.Logger.WarnFormat("Illegal client connection. Local Address: {0}, Remote Address: {1}", userToken.ConnectSocket.LocalEndPoint, userToken.ConnectSocket.RemoteEndPoint);
                    CloseClientSocket(userToken);
                }
                else//现在是收到消息内容了
                {
                    if (count > 0) //处理接收数据
                    {
                        bool blnProcessReceive = userToken.AsyncSocketInvokeElement.ProcessReceive(userToken.ReceiveEventArgs.Buffer);
                        if (!blnProcessReceive) //如果处理数据返回失败，则断开连接
                        {
                            CloseClientSocket(userToken);
                        }
                        else //否则投递下次介绍数据请求
                        {
                            bool willRaiseEvent = userToken.ConnectSocket.ReceiveAsync(userToken.ReceiveEventArgs); //投递接收请求
                            if (!willRaiseEvent)//如果 I/O 操作同步完成，将返回 false。
                                ProcessReceive(userToken.ReceiveEventArgs);
                        }
                    }
                    else
                    {
                        bool willRaiseEvent = userToken.ConnectSocket.ReceiveAsync(userToken.ReceiveEventArgs); //投递接收请求
                        if (!willRaiseEvent)
                            ProcessReceive(userToken.ReceiveEventArgs);
                    }
                }
            }
            else
            {
                CloseClientSocket(userToken);
            }
        }

        /// <summary>
        /// 获取所要使用的协议
        /// </summary>
        /// <param name="userToken"></param>
        private void BuildingSocketInvokeElement(AsyncSocketUserToken userToken)
        {
            byte flag = userToken.ReceiveEventArgs.Buffer[userToken.ReceiveEventArgs.Offset];
            if (flag == (byte) ProtocolFlag.Upload)
                userToken.AsyncSocketInvokeElement = new UploadSocketProtocol(this, userToken);
            else
            {
                userToken.AsyncSocketInvokeElement = new HeatLiChuangProtocol(this, userToken);
            }
            if (userToken.AsyncSocketInvokeElement != null)
            {
                //Program.Logger.InfoFormat("Building socket invoke element {0}.Local Address: {1}, Remote Address: {2}", userToken.AsyncSocketInvokeElement, userToken.ConnectSocket.LocalEndPoint, userToken.ConnectSocket.RemoteEndPoint);
            } 
        }

        private bool ProcessSend(SocketAsyncEventArgs sendEventArgs)
        {
            AsyncSocketUserToken userToken = sendEventArgs.UserToken as AsyncSocketUserToken;
            if (userToken.AsyncSocketInvokeElement == null)
                return false;
            userToken.ActiveDateTime = DateTime.Now;
            if (sendEventArgs.SocketError == SocketError.Success)
                return userToken.AsyncSocketInvokeElement.SendCompleted(); //调用子类回调函数
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
