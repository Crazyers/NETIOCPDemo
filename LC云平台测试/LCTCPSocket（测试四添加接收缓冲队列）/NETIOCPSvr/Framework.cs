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
        public static ConcurrentQueue<byte[]> QueueRawData = new ConcurrentQueue<byte[]>(); //tcp刚接收到的原始数据队列
        public static ConcurrentQueue<CjdHeat> QueueWaitingCjdHeats = new ConcurrentQueue<CjdHeat>(); //全局的采集点队列
        //public static List<CjdHeat> ListCjdHeats = new List<CjdHeat>(10000); //正要存入数据库的列表
        public static DataTable dtEndTable = new DataTable();
        public static ConcurrentDictionary<string, CjdHeat> DicHeatRealTime = new ConcurrentDictionary<string, CjdHeat>(); //<表地址,热量>

        public static void Start()
        {
            GetAllCjdsFromDB();
            InitDt();
            AnalysisRawData(); //从未解包队列内出队，解包
            DealWithData(); //待存储队列里的数据，出队。
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
                heat.BackWaterTemperature = i*11;
                heat.BuildID = i*10;

                heat.CurrentHeat = i*2 + i;
                heat.FlowRate = i*2/3;
                heat.HourseID = i*11;
                heat.LastHeat = i * 2 + i-1;

                heat.SupplyWaterTemperature = 10 + i;
                heat.TemperateDifference = 10;
                heat.ThermalPower = i;
                heat.TotalTime = 10000;

                heat.UnitID = i*1;
                heat.UserID = i;
                heat.UserRealName = "用户" + i;
                //KeyValuePair<string, CjdHeat> pair = new KeyValuePair<string, CjdHeat>(i.ToString(), heat);
                DicHeatRealTime.TryAdd(heat.Address, heat);
            }
        }

        /// <summary>
        /// 使用SqlBulkCopy存入数据库
        /// </summary>
        public static void Save2DB()
        {
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
            dtEndTable.Clear(); //清空数据
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
            tmpRow["户主姓名"] = heat.UserRealName;
            ;
            tmpRow["室温"] = 19;
            tmpRow["设定室温"] = 19;

            tmpRow["阀门状态"] = 1;
            dtEndTable.Rows.Add(tmpRow);
        }

        /// <summary>
        /// 初始化Sqlbulkcopy的数据库格式
        /// </summary>
        /// <returns></returns>
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

        public static void AnalysisRawData()
        {
            Task task = new Task(() =>
            {
                while (true)
                {
                    while (QueueRawData.Count > 0)
                    {
                        byte[] arr;
                        QueueRawData.TryDequeue(out arr);
                        CjdHeat heat = new CjdHeat();
                        int i = 0;
                        string tempstr = "";
                        float sum = 0; //2011.2.21 lwf 增加每块表的校验，发现集抄器中的单个表的数据没有校验，并且结束码不对
                        //if (arr[0] == 0x68 & arr[1] == 0x50) //2013.07.11 lwf 增加，主要是兼容一代的温控考虑的
                        //{
                        //    return false;
                        //}

                        for (i = 6; i >= 2; i--) //解析出地址
                        {
                            //heat.Address = heat.Address + arr[i].ToString("X2");
                            heat.Address = (DateTime.Now.Ticks%10000).ToString(); //把地址平均分配在0~10000内
                        }

                        tempstr = "";
                        for (i = 15; i <= 18; i++)
                        {
                            tempstr = arr[i].ToString("X2") + tempstr;
                        }
                        tempstr = tempstr.Substring(0, 6) + "." + tempstr.Substring(tempstr.Length - 2, 2); //lwf 2010.11.20
                        heat.LastHeat = Convert.ToDouble(tempstr);

                        //当前热量  JudgeDw(Arr(21))
                        tempstr = "";
                        for (i = 20; i <= 23; i++)
                        {
                            tempstr = arr[i].ToString("X2") + tempstr;
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
                        //入队，待处理队列
                        QueueWaitingCjdHeats.Enqueue(heat);
                    }
                }
            });
            task.Start();
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
                        if (dtEndTable.Rows.Count < 10000) //小于10000则向数据库内插入数据
                        {
                            CjdHeat heat;
                            QueueWaitingCjdHeats.TryDequeue(out heat);
                            InsertIntoOneHeatIntoTmpTable(heat);//存入Datatable，等待存入数据库
                            DicHeatRealTime.TryUpdate(heat.Address, heat, DicHeatRealTime[heat.Address]);//更新实时数据Dic
                            //ListCjdHeats.Add(heat); //出队一个插入待存入列表,用于比对
                        }
                        else //如果到达10000个，则存储数据库，更新实时数据
                        {
                            DateTime dtmStart = DateTime.Now;
                            Save2DB();
                            //CompareAndUpdateRealTimeData();
                            //ListCjdHeats.Clear(); //把待存入清空
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

        /// <summary>
        /// 把待存储的10000条数据，转成datatable。然后使用BulkCopy插入数据库。
        /// </summary>
        /// <returns></returns>
        //private DataTable List2DataTable()
        //{
            //DataTable dtEndTable = InitDt();
            //foreach (CjdHeat heat in ListCjdHeats)
            //{
            //    DataRow tmpRow = dtEndTable.NewRow();
            //    tmpRow["表地址"] = heat.Address;
            //    tmpRow["上次抄表热量"] = heat.LastHeat;
            //    tmpRow["当前热量"] = heat.CurrentHeat;

            //    tmpRow["热功率"] = heat.ThermalPower;
            //    tmpRow["瞬时流量"] = heat.FlowRate;
            //    tmpRow["累计流量"] = heat.TotalFlow;
            //    tmpRow["供水温度"] = heat.SupplyWaterTemperature;

            //    tmpRow["回水温度"] = heat.BackWaterTemperature;
            //    tmpRow["温差"] = heat.TemperateDifference;
            //    tmpRow["累计工作时间"] = heat.TotalTime;
            //    tmpRow["实时时间"] = DateTime.Now.ToString();

            //    tmpRow["采集时间"] = DateTime.Now.ToString();
            //    tmpRow["单价"] = 1;
            //    tmpRow["通讯状态"] = 1;
            //    tmpRow["社区编号"] = heat.AreaID;

            //    tmpRow["楼房编号"] = heat.BuildID;
            //    tmpRow["楼层"] = 1;
            //    tmpRow["单元编号"] = heat.UnitID;
            //    tmpRow["房间号"] = heat.HourseID;

            //    tmpRow["户主编号"] = heat.UserID;
            //    tmpRow["户主姓名"] = heat.UserRealName;
            //    ;
            //    tmpRow["室温"] = 19;
            //    tmpRow["设定室温"] = 19;

            //    tmpRow["阀门状态"] = 1;
            //    dtEndTable.Rows.Add(tmpRow);
            //}

            //return dtEndTable;
        //}

        #endregion
    }
}