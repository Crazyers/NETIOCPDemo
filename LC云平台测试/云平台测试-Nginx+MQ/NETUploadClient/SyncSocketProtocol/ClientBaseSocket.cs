using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using AsyncSocketServer;

namespace NETUploadClient.SyncSocketCore
{
    public class ClientBaseSocket
    {
        protected TcpClient m_tcpClient;
        protected string m_host;
        protected int m_port;
        protected int SocketTimeOutMS { get {return m_tcpClient.SendTimeout; } set { m_tcpClient.SendTimeout = value; m_tcpClient.ReceiveTimeout = value; } }
        private bool m_netByteOrder;
        public bool NetByteOrder { get { return m_netByteOrder; } set { m_netByteOrder = value; } } //长度是否使用网络字节顺序
        protected DynamicBufferManager m_recvBuffer; //接收数据的缓存
        protected DynamicBufferManager sendBufferManager; //发送数据的缓存，统一写到内存中，调用一次发送

        public ClientBaseSocket()
        {
            m_tcpClient = new TcpClient();
            m_tcpClient.Client.Blocking = true; //使用阻塞模式，即同步模式
            m_recvBuffer = new DynamicBufferManager(1024 * 4);
            sendBufferManager = new DynamicBufferManager(1024 * 4);
        }

        public void Connect(string host, int port)
        {
            m_tcpClient.Connect(host, port);
            m_host = host;
            m_port = port;
            TestHeatLiChuang.IntConnectedNum++;
        }

        public void Disconnect()
        {
            m_tcpClient.Close();
            m_tcpClient = new TcpClient();
            TestHeatLiChuang.IntConnectedNum--;
            //m_tcpClient.Client.
        }

        /// <summary>
        /// 发送消息内容（不包括协议头之类的内容）
        /// </summary>
        /// <param name="buffer"></param>
        public void SendCommand(byte[] buffer)
        {
            sendBufferManager.Clear();
            sendBufferManager.WriteBuffer(buffer); //写入二进制数据
            m_tcpClient.Client.Send(sendBufferManager.Buffer, 0, sendBufferManager.DataCount, SocketFlags.None);
        }
        /// <summary>
        /// 接收指令
        /// </summary>
        /// <returns></returns>
        public bool RecvCommand()
        {
            m_recvBuffer.Clear();
            m_tcpClient.Client.Receive(m_recvBuffer.Buffer, sizeof(int), SocketFlags.None);
            int packetLength = BitConverter.ToInt32(m_recvBuffer.Buffer, 0); //获取包长度
            if (NetByteOrder)
                packetLength = System.Net.IPAddress.NetworkToHostOrder(packetLength); //把网络字节顺序转为本地字节顺序
            m_recvBuffer.SetBufferSize(sizeof(int) + packetLength); //保证接收有足够的空间
            m_tcpClient.Client.Receive(m_recvBuffer.Buffer, sizeof(int), packetLength, SocketFlags.None);
            int commandLen = BitConverter.ToInt32(m_recvBuffer.Buffer, sizeof(int)); //取出命令长度
            string tmpStr = Encoding.UTF8.GetString(m_recvBuffer.Buffer, sizeof(int) + sizeof(int), commandLen);
            //if (!m_incomingDataParser.DecodeProtocolText(tmpStr)) //解析命令
            //    return false;
            //else
            //    return true;
            return true;
        }

        public bool RecvCommand(out byte[] buffer, out int offset, out int size)
        {
            m_recvBuffer.Clear();
            m_tcpClient.Client.Receive(m_recvBuffer.Buffer, sizeof(int), SocketFlags.None);//先接收32bit的数据
            int packetLength = BitConverter.ToInt32(m_recvBuffer.Buffer, 0); //获取包剩余长度
            if (NetByteOrder)
                packetLength = System.Net.IPAddress.NetworkToHostOrder(packetLength); //把网络字节顺序转为本地字节顺序
            m_recvBuffer.SetBufferSize(sizeof(int) + packetLength); //保证接收有足够的空间
            m_tcpClient.Client.Receive(m_recvBuffer.Buffer, sizeof(int), packetLength, SocketFlags.None);//再接收剩余的数据
            int commandLen = BitConverter.ToInt32(m_recvBuffer.Buffer, sizeof(int)); //取出命令长度
            string tmpStr = Encoding.UTF8.GetString(m_recvBuffer.Buffer, sizeof(int) + sizeof(int), commandLen);
          
            //if (!m_incomingDataParser.DecodeProtocolText(tmpStr)) //解析命令
            //{
            //    buffer = null;
            //    offset = 0;
            //    size = 0;
            //    return false;
            //}
            //else
            //{
                buffer = m_recvBuffer.Buffer;
                offset = commandLen + sizeof(int) + sizeof(int);
                size = packetLength - offset;
                return true;
            //}
        }
    }
}
