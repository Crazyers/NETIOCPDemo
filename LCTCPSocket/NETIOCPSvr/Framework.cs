using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AsyncSocketServer
{
    public class Framework
    {
        public static Dictionary<string, Cjd> DicCjd = new Dictionary<string, Cjd>(); //所有的采集点
        public static ConcurrentQueue<CjdHeat> QueueWaitingCjdHeats = new ConcurrentQueue<CjdHeat>(); //全局的采集点队列
        public static List<CjdHeat> ListCjdHeats = new List<CjdHeat>(10000);//正要存入数据库的列表

        public static void Save2DB()
        {
            
        }
    }
}
