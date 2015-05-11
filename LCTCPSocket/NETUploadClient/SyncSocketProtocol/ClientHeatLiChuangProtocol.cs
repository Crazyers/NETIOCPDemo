using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NETUploadClient.SyncSocketProtocolCore;

namespace NETUploadClient.SyncSocketProtocol
{
    public class ClientHeatLiChuangProtocol: ClientBaseSocket
    {
        public ClientHeatLiChuangProtocol()
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
                SendCommand(buffer);
                return true;
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
