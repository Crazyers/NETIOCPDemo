using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NETUploadClient.SyncSocketCore;

namespace NETUploadClient.SyncSocketProtocol
{
    public class ClientHeatLiChuangProtocol: ClientBaseSocket
    {
        public ClientHeatLiChuangProtocol(): base()
        {
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
