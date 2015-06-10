using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using DAL;
using Entity;

namespace BLL
{
    public class HeatManager
    {

        /// <summary>
        /// 插入一条数据
        /// </summary>
        /// <param name="heat"></param>
        public static void InsertIntoOneHeatIntoTmpTable(CjdHeat heat)
        {
            HeatService.InsertIntoOneHeatIntoTmpTable(heat);
        }

        /// <summary>
        /// 使用SQLbulkCopy批量存入数据库
        /// </summary>
        /// <param name="dtEndTable"></param>
        public static void BulkSave2DB(DataTable dtEndTable)
        {
            HeatService.BulkSave2DB(dtEndTable);
        }
    }
}
