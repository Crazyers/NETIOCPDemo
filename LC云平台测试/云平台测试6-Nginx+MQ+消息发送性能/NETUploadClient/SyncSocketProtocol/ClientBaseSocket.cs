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
            //开始异步接收
            m_tcpClient.Client.BeginReceive(m_recvBuffer.Buffer, 0, m_recvBuffer.Buffer.Count(), 0, new AsyncCallback(ReceiveCallback), new object());
            TestHeatLiChuang.IntConnectedNum++;
        }

        public void Disconnect()
        {
            m_tcpClient.Close();
            TestHeatLiChuang.IntConnectedNum--;
            //m_tcpClient.Client.
        }

        private void ReceiveCallback(IAsyncResult result)
        {
            try
            {
                // Retrieve the state object and the client socket     
                // from the asynchronous state object.     

                Socket client = m_tcpClient.Client;
                // Read data from the remote device.     
                int bytesRead = client.EndReceive(result);
                if (bytesRead > 0)
                {
                    // There might be more data, so store the data received so far.     
                    //state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));
                    // Get the rest of the data.     
                    m_tcpClient.Client.BeginReceive(m_recvBuffer.Buffer, 0, m_recvBuffer.Buffer.Count(), 0, new AsyncCallback(ReceiveCallback), new object());
                }
                else
                {
                    string str = "68 20 38 04 21 08 00 59 42 81 2E 90 1F 00 05 19 28 53 00 05 19 28 53 00 17 00 00 00 00 35 00 00 00 00 2C 42 33 18 00 03 19 00 10 19 00 61 27 00 26 57 23 31 03 15 20 03 01 F7 16";
                    var tmpArr = str.Split(' ');
                    byte[] readBuffer = new byte[tmpArr.Count()];
                    for (int i = 0; i < tmpArr.Count(); i++)
                    {
                        string strTmp = tmpArr[i];
                        int intTmp = Convert.ToInt32(strTmp, 16); //先转成10进制
                        readBuffer[i] = Convert.ToByte(intTmp);
                    }
                    SendData(readBuffer, 0, 0);
                    //发送回复
                    // All the data has arrived; put it in response.     
                    //if (state.sb.Length > 1)
                    //{
                    //    response = state.sb.ToString();
                    //}
                    // Signal that all bytes have been received.     
                    //receiveDone.Set();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static void Send(Socket client, String data)
        {
            // Convert the string data to byte data using ASCII encoding.     
            byte[] byteData = Encoding.ASCII.GetBytes(data);
            // Begin sending the data to the remote device.     
            client.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), client);
        }
        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.     
                Socket client = (Socket)ar.AsyncState;
                // Complete sending the data to the remote device.     
                int bytesSent = client.EndSend(ar);
                Console.WriteLine("Sent {0} bytes to server.", bytesSent);
                // Signal that all bytes have been sent.     
                //sendDone.Set();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
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
            
            //接收到以后
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

        /// <summary>
        /// 发送具体数据
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public bool SendData(byte[] buffer, int offset, int count)
        {
            try
            {
                SendCommand(buffer);
                return true;
            }
            catch (Exception E)
            {
                return false;
            }
        }
    }
}
