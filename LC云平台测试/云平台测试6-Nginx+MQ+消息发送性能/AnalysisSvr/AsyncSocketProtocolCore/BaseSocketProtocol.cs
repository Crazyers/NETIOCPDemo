using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;

namespace AnalysisSvr
{
    public class BaseSocketProtocol : AsyncSocketInvokeElement
    {
        public string UserName { get; protected set; }
        protected string m_socketFlag;
        public string SocketFlag { get { return m_socketFlag; } }

        public BaseSocketProtocol()
        {
            UserName = "";
            m_socketFlag = "";
        }

        /// <summary>
        /// 有超时连接，相对应的需要设计心跳包，心跳包用来检测连接和维护连接状态，
        /// 心跳包的原理是客户端发送一个包给服务器，服务器收到后发一个响应包给客户端，
        /// 通过检测是否有返回来判断连接是否正常
        /// </summary>
        /// <returns></returns>
        public bool DoActive()
        {
            m_outgoingDataAssembler.AddSuccess();
            return DoSendResult();
        }
    }
}
