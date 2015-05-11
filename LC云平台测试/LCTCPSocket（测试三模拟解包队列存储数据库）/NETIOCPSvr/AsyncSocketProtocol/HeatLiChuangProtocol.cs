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
        /// 继承自父类（处理每一包原始数据）
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        public override bool ProcessPacket(byte[] arr)
        {
            CjdHeat heat = new CjdHeat();
            int i = 0;
            string tempstr = "";
			float sum = 0; //2011.2.21 lwf 增加每块表的校验，发现集抄器中的单个表的数据没有校验，并且结束码不对
			if (arr[0] == 0x68 & arr[1] == 0x50) //2013.07.11 lwf 增加，主要是兼容一代的温控考虑的
			{
				return false;
			}

			for (i = 6; i >= 2; i--) //解析出地址
			{
                heat.Address = heat.Address + arr[i].ToString("X2");
			}

			tempstr = "";
			for (i = 15; i <= 18; i++)
			{
				tempstr =arr[i].ToString("X2")+ tempstr;
			}
		    tempstr = tempstr.Substring(0, 6) + "." + tempstr.Substring(tempstr.Length - 2, 2); //lwf 2010.11.20
            heat.LastHeat = Convert.ToDouble(tempstr);
			
			//当前热量  JudgeDw(Arr(21))
			tempstr = "";
			for (i = 20; i <= 23; i++)
			{
				tempstr =arr[i].ToString("X2") + tempstr;
			}
			tempstr = tempstr.Substring(0, 6) + "." + tempstr.Substring(tempstr.Length - 2, 2); //lwf 2010.11.20
            heat.CurrentHeat = Convert.ToDouble(tempstr);
			
			//热功率
			tempstr = "";
			for (i = 25; i <= 28; i++)
			{
                tempstr = arr[i].ToString("X2") + tempstr;
			}
			tempstr = tempstr.Substring(0, 6) + "." + tempstr.Substring(tempstr.Length - 2, 2); //lwf 2010.11.20
            heat.ThermalPower = Convert.ToDouble(tempstr);
			
			//瞬时流量  JudgeDw(Arr(31))
			tempstr = "";
			for (i = 30; i <= 33; i++)
			{
                tempstr = arr[i].ToString("X2") + tempstr;
			}
		    tempstr = tempstr.Substring(0, 5) + "." + tempstr.Substring(tempstr.Length - 3, 3); //lwf 2010.11.20 11.23号发现协议是截取四位小数，实际上是截取三位小数
            heat.FlowRate = Convert.ToDouble(tempstr);
			
			//累计流量   JudgeDw(Arr(36))
			tempstr = "";
			for (i = 35; i <= 38; i++)
			{
                tempstr = arr[i].ToString("X2") + tempstr;
			}
		    tempstr = tempstr.Substring(0, 6) + "." + tempstr.Substring(tempstr.Length - 2, 2); //lwf 2010.11.20
            heat.TotalFlow = Convert.ToDouble(tempstr);
			
			
			//供水温度
			tempstr = "";
			for (i = 39; i <= 41; i++)
			{
                tempstr = arr[i].ToString("X2") + tempstr;
			}
			tempstr = tempstr.Substring(0, 4) + "." + tempstr.Substring(tempstr.Length - 2, 2); //lwf 2010.11.20
            heat.SupplyWaterTemperature = Convert.ToDouble(tempstr);
			
			//回水温度
			tempstr = "";
			for (i = 42; i <= 44; i++)
			{
                tempstr = arr[i].ToString("X2") + tempstr;
			}
			tempstr = tempstr.Substring(0, 4) + "." + tempstr.Substring(tempstr.Length - 2, 2); //lwf 2010.11.20
            heat.BackWaterTemperature = Convert.ToDouble(tempstr);
            heat.TemperateDifference = 10.1;
			
			//累计工作时间
			tempstr = "";
			for (i = 45; i <= 47; i++)
			{
                tempstr = arr[i].ToString("X2") + tempstr;
			}
            heat.TotalTime = Convert.ToDouble(tempstr);
			
			//实时时间
			tempstr = "";
			for (i = 48; i <= 54; i++)
			{
                tempstr = arr[i].ToString("X2") + tempstr;
			}

            heat.AreaID = 1;
            heat.BuildID = 2;
            heat.UnitID = 3;
            heat.HourseID = 4;
            heat.UserRealName = "张三";
            heat.UserID = 5;
            //入队
            Framework.QueueWaitingCjdHeats.Enqueue(heat);
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
