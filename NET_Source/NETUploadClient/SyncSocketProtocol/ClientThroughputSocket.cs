using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NETUploadClient.SyncSocketProtocolCore;

namespace NETUploadClient.SyncSocketProtocol
{
    /// <summary>
    /// 测试压力
    /// </summary>
    public  class ClientThroughputSocket : ClientBaseSocket
    {
        public ClientThroughputSocket()
            : base()
        {
            m_protocolFlag = AsyncSocketServer.ProtocolFlag.Throughput;
        }

        /// <summary>
        /// 发送具体数据
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public bool DoData(byte[] buffer, int offset, int count)
        {
            try
            {
                m_outgoingDataAssembler.Clear();
                m_outgoingDataAssembler.AddRequest();
                m_outgoingDataAssembler.AddCommand(AsyncSocketServer.ProtocolKey.CyclePacket);
                SendCommand(buffer, offset, count);
                return true;
            }
            catch (Exception E)
            {
                //记录日志
                m_errorString = E.Message;
                return false;
            }
        }

        public bool DoEof(Int64 fileSize)
        {
            try
            {
                m_outgoingDataAssembler.Clear();
                m_outgoingDataAssembler.AddRequest();
                m_outgoingDataAssembler.AddCommand(AsyncSocketServer.ProtocolKey.Eof);
                SendCommand();
                bool bSuccess = RecvCommand();
                if (bSuccess)
                    return CheckErrorCode();
                else
                    return false;
            }
            catch (Exception E)
            {
                //记录日志
                m_errorString = E.Message;
                return false;
            }
        }
    }
}
