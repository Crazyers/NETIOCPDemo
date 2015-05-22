using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace AsyncSocketServer
{
    /// <summary>
    /// 力创热表协议
    /// </summary>
    public  class HeatLiChuangProtocol : BaseSocketProtocol
    {
        public HeatLiChuangProtocol(AsyncSocketServer asyncSocketServer, AsyncSocketUserToken asyncSocketUserToken)
            : base(asyncSocketServer, asyncSocketUserToken)
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
            Framework.QueueRawData.Enqueue(arr); //入队
            return true;
        }


        /// <summary>
        /// 处理分完包的数据，
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public override bool ProcessCommand(byte[] buffer, int offset, int count) 
        {
            ControlSocketCommand command = StrToCommand(m_incomingDataParser.Command);
            m_outgoingDataAssembler.Clear();
            m_outgoingDataAssembler.AddResponse();
            m_outgoingDataAssembler.AddCommand(m_incomingDataParser.Command);
            if (!CheckLogined(command)) //检测登录
            {
                m_outgoingDataAssembler.AddFailure(ProtocolCode.UserHasLogined, "");
                return DoSendResult();
            }
            if (command == ControlSocketCommand.Login)
                return DoLogin();
            else if (command == ControlSocketCommand.Active)
                return DoActive();
            else
            {
                Program.Logger.Error("Unknow command: " + m_incomingDataParser.Command);
                return false;
            }
        }

        public ControlSocketCommand StrToCommand(string command)
        {
            if (command.Equals(ProtocolKey.Active, StringComparison.CurrentCultureIgnoreCase))
                return ControlSocketCommand.Active;
            else if (command.Equals(ProtocolKey.Login, StringComparison.CurrentCultureIgnoreCase))
                return ControlSocketCommand.Login;
            else if (command.Equals(ProtocolKey.GetClients, StringComparison.CurrentCultureIgnoreCase))
                return ControlSocketCommand.GetClients;
            else
                return ControlSocketCommand.None;
        }

        public bool CheckLogined(ControlSocketCommand command)
        {
            if ((command == ControlSocketCommand.Login) | (command == ControlSocketCommand.Active))
                return true;
            else
                return m_logined;
        }
    }
}
