using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AsyncSocketServer
{
    /// <summary>
    /// 力创热表协议
    /// </summary>
    public  class HeatLiChuang : BaseSocketProtocol
    {
        public HeatLiChuang(AsyncSocketServer asyncSocketServer, AsyncSocketUserToken asyncSocketUserToken)
            : base(asyncSocketServer, asyncSocketUserToken)
        {
            m_socketFlag = "Control";
        }

        public override void Close()
        {
            base.Close();
        }

        /// <summary>
        /// 集成自父类
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        public override bool ProcessPacket(byte[] buffer)
        {
            CjdHeat heat = new CjdHeat();
            //int commandLen = BitConverter.ToInt32(buffer, offset); //取出命令长度
            //分包完成后，把内存数组是UTF-8编码转换为Unicode，即为C#的string，后续的处理就都可以基于string进行处理，比较方便
            string tmpStr = Encoding.UTF8.GetString(buffer);
            bool blnDecode = m_incomingDataParser.DecodeProtocolText(tmpStr);
            return blnDecode;
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
