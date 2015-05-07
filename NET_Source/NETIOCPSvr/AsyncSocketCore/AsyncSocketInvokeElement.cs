using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Threading;

namespace AsyncSocketServer
{
    //异步Socket调用对象，所有的协议处理都从本类继承
    public class AsyncSocketInvokeElement
    {
        protected AsyncSocketServer m_asyncSocketServer;
        public AsyncSocketUserToken AsyncSocketUserToken { get; protected set; }
        public bool NetByteOrder { get; set; }//长度是否使用网络字节顺序
        protected bool m_sendAsync; //标识是否有发送异步事件
        public DateTime ConnectDT { get; protected set; }
        public DateTime ActiveDT { get; protected set; }

        protected IncomingDataParser m_incomingDataParser; //协议解析器，用来解析客户端接收到的命令
        protected OutgoingDataAssembler m_outgoingDataAssembler; //协议组装器，用来组织服务端返回的命令

        public AsyncSocketInvokeElement(AsyncSocketServer asyncSocketServer, AsyncSocketUserToken asyncSocketUserToken)
        {
            m_asyncSocketServer = asyncSocketServer;
            AsyncSocketUserToken = asyncSocketUserToken;

            NetByteOrder = false;

            m_incomingDataParser = new IncomingDataParser();
            m_outgoingDataAssembler = new OutgoingDataAssembler();

            m_sendAsync = false;

            ConnectDT = DateTime.UtcNow;
            ActiveDT = DateTime.UtcNow;
        }

        public virtual void Close()
        { 
        }

        /// <summary>
        /// 接收异步事件返回的数据，用于对数据进行缓存和分包，这是原始数据的包
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public virtual bool ProcessReceive(byte[] buffer, int offset, int count) 
        {
            ActiveDT = DateTime.UtcNow;
            DynamicBufferManager receiveBuffer = AsyncSocketUserToken.ReceiveBuffer;

            receiveBuffer.WriteBuffer(buffer, offset, count);
            bool result = true;
            while (receiveBuffer.DataCount > sizeof(int))
            {
                //按照长度分包
                int packetLength = BitConverter.ToInt32(receiveBuffer.Buffer, 0); //获取包长度
                if (NetByteOrder)
                    packetLength = System.Net.IPAddress.NetworkToHostOrder(packetLength); //把网络字节顺序转为本地字节顺序

                if ((packetLength > 10 * 1024 * 1024) | (receiveBuffer.DataCount > 10 * 1024 * 1024)) //最大Buffer异常保护
                    return false;

                //收到的数据达到包长度，进行处理，否则不处理，等待下一个包继续写入缓存
                if ((receiveBuffer.DataCount - sizeof(int)) >= packetLength)
                {
                    Analysis.IntTotalMsg++;
                    //处理包
                    result = ProcessPacket(receiveBuffer.Buffer, sizeof(int), packetLength);
                    if (result)//处理完成，从缓存中清理
                        receiveBuffer.Clear(packetLength + sizeof(int));
                    else
                        return result;
                }
                else
                {
                    return true;
                }
            }
            return true;
        }

        /// <summary>
        /// 处理分完包后的数据，把命令和数据分开，并对命令进行解析
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public virtual bool ProcessPacket(byte[] buffer, int offset, int count) 
        {
            if (count < sizeof(int))
                return false;
            int commandLen = BitConverter.ToInt32(buffer, offset); //取出命令长度
            //分包完成后，把内存数组是UTF-8编码转换为Unicode，即为C#的string，后续的处理就都可以基于string进行处理，比较方便
            string tmpStr = Encoding.UTF8.GetString(buffer, offset + sizeof(int), commandLen);
            bool blnDecode = m_incomingDataParser.DecodeProtocolText(tmpStr);
            if (!blnDecode) //解析命令
              return false;

            //处理命令，针对不同的命令进行处理
            return ProcessCommand(buffer, offset + sizeof(int) + commandLen, count - sizeof(int) - commandLen); 
        }

        /// <summary>
        /// 处理具体命令，子类从这个方法继承，buffer是收到的数据
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public virtual bool ProcessCommand(byte[] buffer, int offset, int count) 
        {
            return true;
        }

        public virtual bool SendCompleted()
        {
            ActiveDT = DateTime.UtcNow;
            m_sendAsync = false;
            AsyncSendBufferManager asyncSendBufferManager = AsyncSocketUserToken.SendBuffer;
            asyncSendBufferManager.ClearFirstPacket(); //清除已发送的包
            int offset = 0;
            int count = 0;
            if (asyncSendBufferManager.GetFirstPacket(ref offset, ref count))
            {
                m_sendAsync = true;
                return m_asyncSocketServer.SendAsyncEvent(AsyncSocketUserToken.ConnectSocket, AsyncSocketUserToken.SendEventArgs,
                    asyncSendBufferManager.DynamicBufferManager.Buffer, offset, count);
            }
            else
                return SendCallback();
        }

        //发送回调函数，用于连续下发数据
        public virtual bool SendCallback()
        {
            return true;
        }

        /// <summary>
        /// 发包我们主要调用DoSendResult，从发送缓冲中获取协议文本后，转换为UTF-8，然后写入发送列表中
        /// </summary>
        /// <returns></returns>
        public bool DoSendResult()
        {
            string commandText = m_outgoingDataAssembler.GetProtocolText();
            byte[] bufferUTF8 = Encoding.UTF8.GetBytes(commandText);
            int totalLength = sizeof(int) + bufferUTF8.Length; //获取总大小
            AsyncSendBufferManager asyncSendBufferManager = AsyncSocketUserToken.SendBuffer;
            asyncSendBufferManager.StartPacket();
            asyncSendBufferManager.DynamicBufferManager.WriteInt(totalLength, false); //写入总大小
            asyncSendBufferManager.DynamicBufferManager.WriteInt(bufferUTF8.Length, false); //写入命令大小
            asyncSendBufferManager.DynamicBufferManager.WriteBuffer(bufferUTF8); //写入命令内容
            asyncSendBufferManager.EndPacket();

            bool result = true;
            if (!m_sendAsync)
            {
                int packetOffset = 0;
                int packetCount = 0;
                if (asyncSendBufferManager.GetFirstPacket(ref packetOffset, ref packetCount))
                {
                    m_sendAsync = true;
                    result = m_asyncSocketServer.SendAsyncEvent(AsyncSocketUserToken.ConnectSocket, AsyncSocketUserToken.SendEventArgs, 
                        asyncSendBufferManager.DynamicBufferManager.Buffer, packetOffset, packetCount);
                }                
            }
            return result;
        }

        /// <summary>
        /// 发送结果
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public bool DoSendResult(byte[] buffer, int offset, int count)
        {
            string commandText = m_outgoingDataAssembler.GetProtocolText();
            byte[] bufferUTF8 = Encoding.UTF8.GetBytes(commandText);
            int totalLength = sizeof(int) + bufferUTF8.Length + count; //获取总大小
            AsyncSendBufferManager asyncSendBufferManager = AsyncSocketUserToken.SendBuffer;
            asyncSendBufferManager.StartPacket();
            asyncSendBufferManager.DynamicBufferManager.WriteInt(totalLength, false); //写入总大小
            asyncSendBufferManager.DynamicBufferManager.WriteInt(bufferUTF8.Length, false); //写入命令大小
            asyncSendBufferManager.DynamicBufferManager.WriteBuffer(bufferUTF8); //写入命令内容
            asyncSendBufferManager.DynamicBufferManager.WriteBuffer(buffer, offset, count); //写入二进制数据
            asyncSendBufferManager.EndPacket();

            bool result = true;
            if (!m_sendAsync)
            {
                int packetOffset = 0;
                int packetCount = 0;
                if (asyncSendBufferManager.GetFirstPacket(ref packetOffset, ref packetCount))
                {
                    m_sendAsync = true;
                    result = m_asyncSocketServer.SendAsyncEvent(AsyncSocketUserToken.ConnectSocket, AsyncSocketUserToken.SendEventArgs, 
                        asyncSendBufferManager.DynamicBufferManager.Buffer, packetOffset, packetCount);
                }
            }
            return result;
        }

        /// <summary>
        /// 不是按包格式下发一个内存块，用于日志这类下发协议
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public bool DoSendBuffer(byte[] buffer, int offset, int count) 
        {
            AsyncSendBufferManager asyncSendBufferManager = AsyncSocketUserToken.SendBuffer;
            asyncSendBufferManager.StartPacket();
            asyncSendBufferManager.DynamicBufferManager.WriteBuffer(buffer, offset, count);
            asyncSendBufferManager.EndPacket();

            bool result = true;
            if (!m_sendAsync)
            {
                int packetOffset = 0;
                int packetCount = 0;
                if (asyncSendBufferManager.GetFirstPacket(ref packetOffset, ref packetCount))
                {
                    m_sendAsync = true;
                    result = m_asyncSocketServer.SendAsyncEvent(AsyncSocketUserToken.ConnectSocket, AsyncSocketUserToken.SendEventArgs, 
                        asyncSendBufferManager.DynamicBufferManager.Buffer, packetOffset, packetCount);
                }
            }
            return result;
        }
    }
}