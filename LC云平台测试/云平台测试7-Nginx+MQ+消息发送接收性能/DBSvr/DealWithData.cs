using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BLL;
using Entity;

namespace DBSvr
{
       public class DealWithData
    {
           
        public static ConcurrentQueue<CjdHeat> QueueWaitingCjdHeats = new ConcurrentQueue<CjdHeat>(); //全局的采集点队列
        /// <summary>
        /// 处理队列内的数据，存入数据库
        /// </summary>
        public static void DealWith()
        {
            DataTable dtEndTable = new DataTable();
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
                            HeatManager.InsertIntoOneHeatIntoTmpTable(heat);//存入Datatable，等待存入数据库
                            //DicHeatRealTime.TryUpdate(heat.Address, heat, DicHeatRealTime[heat.Address]);//更新实时数据Dic
                            //ListCjdHeats.Add(heat); //出队一个插入待存入列表,用于比对
                        }
                        else //如果到达10000个，则存储数据库，更新实时数据
                        {
                            DateTime dtmStart = DateTime.Now;
                            HeatManager.BulkSave2DB(dtEndTable);
                            //CompareAndUpdateRealTimeData();
                            //ListCjdHeats.Clear(); //把待存入清空
                            DateTime dtmEnd = DateTime.Now;
                            //Analysis.DblCurSaveTime = (dtmEnd - dtmStart).TotalMilliseconds; //毫秒
                            //Analysis.DblSaveTotalTime += Analysis.DblCurSaveTime;
                            //Analysis.SaveCounts++;
                        }
                    }
                }
            });
            task.Start();
        }
    }
}
