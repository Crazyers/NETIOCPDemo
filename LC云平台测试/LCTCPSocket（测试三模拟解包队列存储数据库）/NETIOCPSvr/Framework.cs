using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncSocketServer
{
    public class Framework
    {
        public static Dictionary<string, Cjd> DicCjd = new Dictionary<string, Cjd>(); //所有的采集点
        public static ConcurrentQueue<CjdHeat> QueueWaitingCjdHeats = new ConcurrentQueue<CjdHeat>(); //全局的采集点队列
        public static List<CjdHeat> ListCjdHeats = new List<CjdHeat>(10000);//正要存入数据库的列表
        public static DataTable dtEndTable = new DataTable();

        public static void Start()
        {
            GetAllCjdsFromDB();
            InitDt();
            DealWithData();
        }
        /// <summary>
        /// 从数据库里获取所有的采集点
        /// </summary>
        /// <returns></returns>
        public static Dictionary<string, Cjd> GetAllCjdsFromDB()
        {
            return DicCjd;
        }

        /// <summary>
        /// 使用SqlBulkCopy存入数据库
        /// </summary>
        public static void Save2DB(){
            SqlBulkCopy sqlbulk = new SqlBulkCopy(Program.ConStr, SqlBulkCopyOptions.TableLock);
            sqlbulk.BulkCopyTimeout = 240; //2013.3.13刘文峰增加，这样对于大数据量的情况能存储到数据库，否则可能会超时存储失败
            sqlbulk.DestinationTableName = "measure_heat";
            sqlbulk.BatchSize = 10000;

            SqlConnection myconnection = new SqlConnection(Program.ConStr);
            myconnection.Open();
            if (!(dtEndTable == null) && dtEndTable.Rows.Count > 0)
            {
                sqlbulk.WriteToServer(dtEndTable, DataRowState.Added);
            }
            sqlbulk.Close();
            myconnection.Close();
            dtEndTable.Clear();//清空数据
        }

        /// <summary>
        /// 插入一条数据
        /// </summary>
        /// <param name="heat"></param>
        public static void InsertIntoOneHeatIntoTmpTable(CjdHeat heat)
        {
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
            tmpRow["户主姓名"] = heat.UserRealName; ;
            tmpRow["室温"] = 19;
            tmpRow["设定室温"] = 19;

            tmpRow["阀门状态"] = 1;
            dtEndTable.Rows.Add(tmpRow);
        }
      
        public static DataTable InitDt()
        {
            //从数据库里读，不自己构建了
            dtEndTable = DBHelper.GetDataTable("select top 1 * from Measure_heat");
            //dtEndTable.Columns.Add("表地址", typeof(string));
            //dtEndTable.Columns.Add("上次抄表热量", typeof(decimal));
            //dtEndTable.Columns.Add("当前热量", typeof(decimal));

            //dtEndTable.Columns.Add("热功率", typeof(decimal));
            //dtEndTable.Columns.Add("瞬时流量", typeof(decimal));
            //dtEndTable.Columns.Add("累计流量", typeof(decimal));
            //dtEndTable.Columns.Add("供水温度", typeof(decimal));

            //dtEndTable.Columns.Add("回水温度", typeof(decimal));
            //dtEndTable.Columns.Add("温差", typeof(decimal));
            //dtEndTable.Columns.Add("累计工作时间", typeof(decimal));
            //dtEndTable.Columns.Add("实时时间", typeof(string));

            //dtEndTable.Columns.Add("采集时间", typeof(DateTime));
            //dtEndTable.Columns.Add("单价", typeof(decimal));
            //dtEndTable.Columns.Add("通讯状态", typeof(string));
            //dtEndTable.Columns.Add("社区编号", typeof(int));

            //dtEndTable.Columns.Add("楼房编号", typeof(int));
            //dtEndTable.Columns.Add("楼层", typeof(string));
            //dtEndTable.Columns.Add("单元编号", typeof(int));
            //dtEndTable.Columns.Add("房间号", typeof(int));

            //dtEndTable.Columns.Add("户主编号", typeof(int));
            //dtEndTable.Columns.Add("户主姓名", typeof(string));
            //dtEndTable.Columns.Add("室温", typeof(decimal));
            //dtEndTable.Columns.Add("设定室温", typeof(decimal));

            //dtEndTable.Columns.Add("阀门状态", typeof(string));

            return dtEndTable;
        }
        
        /// <summary>
        /// 处理队列内的数据，存入数据库
        /// </summary>
        public static void DealWithData()
        {
            Task task = new Task(() =>
            {
                while (true)
                {
                    while (QueueWaitingCjdHeats.Count > 0)
                    {
                        if (dtEndTable.Rows.Count < 10000)//小于10000则向数据库内插入数据
                        {
                            CjdHeat heat;
                            QueueWaitingCjdHeats.TryDequeue(out heat);
                            InsertIntoOneHeatIntoTmpTable(heat);
                            ListCjdHeats.Add(heat);//出队一个插入待存入列表,用于比对
                        }
                        else //如果到达10000个，则存储数据库，更新实时数据
                        {
                            DateTime dtmStart = DateTime.Now;
                            Save2DB();
                            //CompareAndUpdateRealTimeData();
                            ListCjdHeats.Clear(); //把待存入清空
                            DateTime dtmEnd = DateTime.Now;
                            Analysis.DblCurSaveTime = (dtmEnd - dtmStart).TotalMilliseconds; //毫秒
                            Analysis.DblSaveTotalTime += Analysis.DblCurSaveTime;
                            Analysis.SaveCounts ++;
                        }
                    }
                }
            });
            task.Start();
        }

        #region 
        private DataTable List2DataTable()
        {
            DataTable dtEndTable = InitDt();
            foreach (CjdHeat heat in ListCjdHeats)
            {
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
                tmpRow["户主姓名"] = heat.UserRealName; ;
                tmpRow["室温"] = 19;
                tmpRow["设定室温"] = 19;

                tmpRow["阀门状态"] = 1;
                dtEndTable.Rows.Add(tmpRow);
            }

            return dtEndTable;
        }

#endregion
    }
}
