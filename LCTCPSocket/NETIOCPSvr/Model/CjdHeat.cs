using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AsyncSocketServer
{
   public class CjdHeat
    {
       public string Address { get; set; }
       public double LastHeat { get; set; }
       public double CurrentHeat { get; set; }//当前热量
       public double ThermalPower { get; set; }//热功率
       public double FlowRate { get; set; }//瞬时流量
       public double SupplyWaterTemperature { get; set; }//供水温度
       public double BackWaterTemperature { get; set; }//回水温度
       public double TemperateDifference  { get; set; }//温差
       public double TotalTime { get; set; }//累计工作时间
       public int AreaID { get; set; }//累计流量
       public int BuildID { get; set; }//累计流量
       public int UnitID { get; set; }//累计流量
       public int HourseID { get; set; }//累计流量
       public int UserID { get; set; }//累计流量
       public int UserRealName { get; set; }//累计流量
    }
}
