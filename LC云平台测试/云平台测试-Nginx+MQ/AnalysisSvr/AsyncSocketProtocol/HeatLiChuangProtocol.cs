using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace AnalysisSvr
{
    /// <summary>
    /// 力创热表协议
    /// </summary>
    public  class HeatLiChuangProtocol : BaseSocketProtocol
    {
        public HeatLiChuangProtocol(): base()
        {
            m_socketFlag = "Control";
        }

        public override void Close()
        {
            base.Close();
        }

        /// <summary>
        /// 继承自父类（处理每一包原始数据，先进入原始数据队列，然后在Framework里面进行解包）
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        public override bool ProcessPacket(byte[] arr)
        {
            //Framework.QueueRawData.Enqueue(arr); //入队
            return true;
        }
    }
}
