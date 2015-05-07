using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;

namespace AsyncSocketServer
{
    class ThroughputSocketProtocol : BaseSocketProtocol
    {
        public ThroughputSocketProtocol(AsyncSocketServer asyncSocketServer, AsyncSocketUserToken asyncSocketUserToken)
            : base(asyncSocketServer, asyncSocketUserToken)
        {
            m_socketFlag = "Throughput";
        }

        
        public override void Close()
        {
            base.Close();
        }

        /// <summary>
        /// 处理分完包的数据（去掉命令头以后的数据）
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public override bool ProcessCommand(byte[] buffer, int offset, int count)
        {
            string strData = Encoding.UTF8.GetString(buffer, offset, count);
            Analysis.WaitingDealQueue.Enqueue(strData);//加入全局队列
            return true;
            //ThroughputSocketCommand command = StrToCommand(m_incomingDataParser.Command);
            //m_outgoingDataAssembler.Clear();
            //m_outgoingDataAssembler.AddResponse();
            //m_outgoingDataAssembler.AddCommand(m_incomingDataParser.Command);
            //if (command == ThroughputSocketCommand.CyclePacket)
            //    return DoCyclePacket(buffer, offset, count);
            //else
            //{
            //    Program.Logger.Error("Unknow command: " + m_incomingDataParser.Command);
            //    return false;
            //}
        }

        public ThroughputSocketCommand StrToCommand(string command)
        {
            if (command.Equals(ProtocolKey.CyclePacket, StringComparison.CurrentCultureIgnoreCase))
                return ThroughputSocketCommand.CyclePacket;
            else
                return ThroughputSocketCommand.None;
        }

        /// <summary>
        /// 处理接收到的消息
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public bool DoCyclePacket(byte[] buffer, int offset, int count)
        {
            int cycleCount = 0;
            if (m_incomingDataParser.GetValue(ProtocolKey.Count, ref cycleCount))
            {
                m_outgoingDataAssembler.AddSuccess();
                cycleCount = cycleCount + 1;
                m_outgoingDataAssembler.AddValue(ProtocolKey.Count, cycleCount);
            }
            else
                m_outgoingDataAssembler.AddFailure(ProtocolCode.ParameterError, "");
            return DoSendResult(buffer, offset, count);
        }
    }
}
