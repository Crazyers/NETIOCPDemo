using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;

namespace AsyncSocketServer
{
    public class AsyncSocketUserToken
    {
        public SocketAsyncEventArgs ReceiveEventArgs { get; set; }
        protected byte[] m_asyncReceiveBuffer;
        public SocketAsyncEventArgs SendEventArgs { get; set; }
        public DynamicBufferManager ReceiveBuffer { get; set; }
        public AsyncSendBufferManager SendBuffer { get; set; }
        public AsyncSocketInvokeElement AsyncSocketInvokeElement { get; set; }

        protected Socket m_connectSocket;
        public Socket ConnectSocket
        {
            get
            {
                return m_connectSocket;
            }
            set
            {
                m_connectSocket = value;
                if (m_connectSocket == null) //清理缓存
                {
                    if (AsyncSocketInvokeElement != null)
                        AsyncSocketInvokeElement.Close();
                    ReceiveBuffer.Clear(ReceiveBuffer.DataCount);
                    SendBuffer.ClearPacket();
                }
                AsyncSocketInvokeElement = null;                
                ReceiveEventArgs.AcceptSocket = m_connectSocket;
                SendEventArgs.AcceptSocket = m_connectSocket;
            }
        }

        protected DateTime m_ConnectDateTime;
        public DateTime ConnectDateTime { get { return m_ConnectDateTime; } set { m_ConnectDateTime = value; } }
        protected DateTime m_ActiveDateTime;
        public DateTime ActiveDateTime { get { return m_ActiveDateTime; } set { m_ActiveDateTime = value; } }

        public AsyncSocketUserToken(int asyncReceiveBufferSize)
        {
            m_connectSocket = null;
            AsyncSocketInvokeElement = null;
            ReceiveEventArgs = new SocketAsyncEventArgs();
            ReceiveEventArgs.UserToken = this;
            m_asyncReceiveBuffer = new byte[asyncReceiveBufferSize];
            ReceiveEventArgs.SetBuffer(m_asyncReceiveBuffer, 0, m_asyncReceiveBuffer.Length);//设置要用于异步套接字方法的数据缓冲区。
            SendEventArgs = new SocketAsyncEventArgs();
            SendEventArgs.UserToken = this;
            ReceiveBuffer = new DynamicBufferManager(ProtocolConst.InitBufferSize);
            SendBuffer = new AsyncSendBufferManager(ProtocolConst.InitBufferSize); ;
        }
    }
}
