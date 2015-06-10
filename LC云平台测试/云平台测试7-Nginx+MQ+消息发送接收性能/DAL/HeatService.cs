using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using Entity;

namespace DAL
{
    public class HeatService
    {
        /// <summary>
        /// 使用SQLbulkCopy批量存入数据库
        /// </summary>
        /// <param name="dtEndTable"></param>
        public static void BulkSave2DB(DataTable dtEndTable)
        {
            DBHelper.BulkSave2DB(dtEndTable);
        }

        /// <summary>
        /// 从数据库里获取所有的采集点
        /// </summary>
        /// <returns></returns>
        public static void GetAllCjdsFromDB()
        {
            for (int i = 0; i < 10000; i++)
            {
                //都是造的假数据,造出一个一万个用户的实时数据字典。
                CjdHeat heat = new CjdHeat();
                heat.Address = i.ToString();
                heat.AreaID = i;
                heat.BackWaterTemperature = i * 11;
                heat.BuildID = i * 10;

                heat.CurrentHeat = i * 2 + i;
                heat.FlowRate = i * 2 / 3;
                heat.HourseID = i * 11;
                heat.LastHeat = i * 2 + i - 1;

                heat.SupplyWaterTemperature = 10 + i;
                heat.TemperateDifference = 10;
                heat.ThermalPower = i;
                heat.TotalTime = 10000;

                heat.UnitID = i * 1;
                heat.UserID = i;
                heat.UserRealName = "用户" + i;
                //KeyValuePair<string, CjdHeat> pair = new KeyValuePair<string, CjdHeat>(i.ToString(), heat);
                //DicHeatRealTime.TryAdd(heat.Address, heat);//实时数据字典
            }
        }

        /// <summary>
        /// 插入一条数据
        /// </summary>
        /// <param name="heat"></param>
        public static void InsertIntoOneHeatIntoTmpTable(CjdHeat heat)
        {
            //从数据库里读，不自己构建了
            DataTable   dtEndTable = DBHelper.GetDataTable("select top 1 * from Measure_heat");
            DataRow tmpRow = dtEndTable.NewRow();
            tmpRow["表地址"] = heat.Address;
            tmpRow["上次抄表热量"] = heat.LastHeat;
            tmpRow["当前热量"] = heat.CurrentHeat;

            tmpRow["热功率"] = heat.ThermalPower;
            tmpRow["瞬时流量"] = heat.FlowRate;
            tmpRow["累计流量"] = heat.TotalFlow;
            tmpRow["供水温度"] = heat.SupplyWaterTemperature;

            tmpRow["回水温度"] = heat.BackWaterTemperature;
            tmpRow["温差"] = heat.TemperateDifference;
            tmpRow["累计工作时间"] = heat.TotalTime;
            tmpRow["实时时间"] = DateTime.Now.ToString();

            tmpRow["采集时间"] = DateTime.Now.ToString();
            tmpRow["单价"] = 1;
            tmpRow["通讯状态"] = 1;
            tmpRow["社区编号"] = heat.AreaID;

            tmpRow["楼房编号"] = heat.BuildID;
            tmpRow["楼层"] = 1;
            tmpRow["单元编号"] = heat.UnitID;
            tmpRow["房间号"] = heat.HourseID;

            tmpRow["户主编号"] = heat.UserID;
            tmpRow["户主姓名"] = heat.UserRealName;
            ;
            tmpRow["室温"] = 19;
            tmpRow["设定室温"] = 19;

            tmpRow["阀门状态"] = 1;
            dtEndTable.Rows.Add(tmpRow);
        }
    }
}
