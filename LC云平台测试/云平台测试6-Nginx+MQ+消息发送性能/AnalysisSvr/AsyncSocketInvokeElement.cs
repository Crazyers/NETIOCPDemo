using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Threading;

namespace AnalysisSvr
{
    //异步Socket调用对象，所有的协议处理都从本类继承
    public class AsyncSocketInvokeElement
    {
        protected bool m_sendAsync; //标识是否有发送异步事件
        protected IncomingDataParser m_incomingDataParser; //协议解析器，用来解析客户端接收到的命令
        protected OutgoingDataAssembler m_outgoingDataAssembler; //协议组装器，用来组织服务端返回的命令
        public DateTime ConnectDT { get; protected set; }
        public DateTime ActiveDT { get; protected set; }
        public bool NetByteOrder { get; set; }//长度是否使用网络字节顺序

        public AsyncSocketInvokeElement()
        {
            m_sendAsync = false;
            NetByteOrder = false;
            ActiveDT = DateTime.UtcNow;
            ConnectDT = DateTime.UtcNow;
            m_incomingDataParser = new IncomingDataParser();
            m_outgoingDataAssembler = new OutgoingDataAssembler();
        }

        public virtual void Close()
        { 
        }

        /// <summary>
        /// 处理收到的包（根据不同的协议进行解析，解析完了以后进入队列）
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        public virtual bool ProcessReceive(byte[] buffer)
        {
            ActiveDT = DateTime.UtcNow;
            //当前测试力创热表
            bool result = ProcessPacket(buffer);
            return result;
        }

        /// <summary>
        /// 子类集成处理解析数据的过程
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        public virtual bool ProcessPacket(byte[] buffer)
        {
            return true;
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
            //ActiveDT = DateTime.UtcNow;
            //m_sendAsync = false;
            //AsyncSendBufferManager asyncSendBufferManager = AsyncSocketUserToken.SendBuffer;
            //asyncSendBufferManager.ClearFirstPacket(); //清除已发送的包
            //int offset = 0;
            //int count = 0;
            //if (asyncSendBufferManager.GetFirstPacket(ref offset, ref count))
            //{
            //    m_sendAsync = true;
            //    return m_asyncSocketServer.SendAsyncEvent(AsyncSocketUserToken.ConnectSocket, AsyncSocketUserToken.SendEventArgs,
            //        asyncSendBufferManager.DynamicBufferManager.Buffer, offset, count);
            //}
            //else
            //    return SendCallback();
            return true;
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
            return true;
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
            return true;
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
            return true;
        }
    }
}