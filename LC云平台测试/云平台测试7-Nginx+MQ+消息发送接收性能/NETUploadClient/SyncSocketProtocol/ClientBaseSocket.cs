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
        protected DynamicBufferManager ReceiveBufferManager; //接收数据的缓存
        protected DynamicBufferManager sendBufferManager; //发送数据的缓存，统一写到内存中，调用一次发送

        public ClientBaseSocket()
        {
            m_tcpClient = new TcpClient();
            m_tcpClient.Client.Blocking = true; //使用阻塞模式，即同步模式
            ReceiveBufferManager = new DynamicBufferManager(1024 * 4);
            sendBufferManager = new DynamicBufferManager(1024 * 4);
        }

        public void Connect(string host, int port)
        {
            m_tcpClient.Connect(host, port);
            m_host = host;
            m_port = port;
            Console.WriteLine("开始接收数据");
            //开始异步接收
            m_tcpClient.Client.BeginReceive(ReceiveBufferManager.Buffer, 0, ReceiveBufferManager.Buffer.Count(), 0, ReceiveCallback, new object());
            TestHeatLiChuang.IntConnectedNum++;
        }

        public void Disconnect()
        {
            m_tcpClient.Close();
            TestHeatLiChuang.IntConnectedNum--;
            //m_tcpClient.Client.
        }

        /// <summary>
        /// 接收到数据的回调函数
        /// </summary>
        /// <param name="result"></param>
        private void ReceiveCallback(IAsyncResult result)
        {
            try
            {
              
                if (m_tcpClient.Client == null)
                {
                    Console.WriteLine("远程连接关闭");
                    return;
                   
                }
                Socket client = m_tcpClient.Client;
                int bytesRead = client.EndReceive(result);
                if (bytesRead > 0)// 有数据，继续接收.
                {
                    Console.WriteLine("接收到数据大小 {0} bytes to.", bytesRead);
                    m_tcpClient.Client.BeginReceive(ReceiveBufferManager.Buffer, 0, ReceiveBufferManager.Buffer.Count(), 0, ReceiveCallback, new object());
                    //发送测试数据
                    SendTestData();
                }
                else//接收完了，处理其他过程，发送给服务器
                {
                    SendTestData();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private void SendTestData()
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
            //Console.WriteLine("Sent {0} bytes to server.", readBuffer.Length);
        }
        private static void Send(Socket client, String data)
        {
            byte[] byteData = Encoding.ASCII.GetBytes(data);
            client.BeginSend(byteData, 0, byteData.Length, 0, SendCallback, client);
        }

        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                Socket client = (Socket)ar.AsyncState;
                int bytesSent = client.EndSend(ar);
                //Console.WriteLine("Sent {0} bytes to server.", bytesSent);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        /// <summary>
        /// 发送消息内容
        /// </summary>
        /// <param name="buffer"></param>
        public void SendCommand(byte[] buffer)
        {
            sendBufferManager.Clear();
            sendBufferManager.WriteBuffer(buffer); //写入二进制数据
            m_tcpClient.Client.Send(sendBufferManager.Buffer, 0, sendBufferManager.DataCount, SocketFlags.None);
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
