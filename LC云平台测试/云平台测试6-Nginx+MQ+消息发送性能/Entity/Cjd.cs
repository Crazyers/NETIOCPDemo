using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Entity
{
    /// <summary>
    /// 数据库中的采集点
    /// </summary>
    public class Cjd
    {
        public string Address { get; set; }
        public int AreaID { get; set; }//累计流量
        public int BuildID { get; set; }//累计流量
        public int UnitID { get; set; }//累计流量
        public int HourseID { get; set; }//累计流量
        public int UserID { get; set; }//累计流量
        public int UserRealName { get; set; }//累计流量
    }
}
