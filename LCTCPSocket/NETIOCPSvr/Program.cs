using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.AccessControl;
using System.Net;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using System.Configuration;
using System.IO;
using log4net.Filter;

[assembly: log4net.Config.XmlConfigurator(Watch = true)]

namespace AsyncSocketServer
{    
    public class Program
    {
        public static ILog Logger;
        public static AsyncSocketServer AsyncSocketSvr;
        public static string FileDirectory;
        
        static void Main(string[] args)
        {
            DateTime currentTime = DateTime.Now;
            log4net.GlobalContext.Properties["LogDir"] = currentTime.ToString("yyyyMM");
            log4net.GlobalContext.Properties["LogFileName"] = "_SocketAsyncServer" + currentTime.ToString("yyyyMMdd");
            Logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            FileDirectory = config.AppSettings.Settings["FileDirectory"].Value;
            if (FileDirectory == "")
                FileDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Files");
            if (!Directory.Exists(FileDirectory))
                Directory.CreateDirectory(FileDirectory);
            int port = 0;
            if (!(int.TryParse(config.AppSettings.Settings["Port"].Value, out port)))
                port = 9999;
            int parallelNum = 0;
            if (!(int.TryParse(config.AppSettings.Settings["ParallelNum"].Value, out parallelNum)))
                parallelNum = 8000;
            int socketTimeOutMS = 0;
            if (!(int.TryParse(config.AppSettings.Settings["SocketTimeOutMS"].Value, out socketTimeOutMS)))
                socketTimeOutMS = 5 * 60 * 1000;

            AsyncSocketSvr = new AsyncSocketServer(parallelNum);
            AsyncSocketSvr.SocketTimeOutMS = socketTimeOutMS;
            AsyncSocketSvr.Init();
            //IPEndPoint listenPoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), port);
            // IPEndPoint listenPoint = new IPEndPoint(IPAddress.Parse("172.16.12.105"), port);172.16.6.114
            //IPEndPoint listenPoint = new IPEndPoint(IPAddress.Parse("172.16.6.113"), port);
            IPEndPoint listenPoint = new IPEndPoint(IPAddress.Parse("172.16.6.102"), port);
            //IPEndPoint listenPoint = new IPEndPoint(IPAddress.Parse("172.16.6.114"), port);
            AsyncSocketSvr.Start(listenPoint);

            //运行一个线程，监控全局队列的数目
            Analysis.DealWithData();
            Analysis.Analisis();
           
            Console.WriteLine("Press any key to terminate the server process....");
            Console.ReadKey();
        }
    }

    public class Analysis
    {
        public static ConcurrentQueue<string> WaitingDealQueue = new ConcurrentQueue<string>();//用于存储经过处理后的,待处理的数据的队列
        public static List<string> DealingList = new List<string>(10000);
        public static int IntTotalMsg = 0;

        /// <summary>
        /// 一个循环，用于处理数据
        /// </summary>
        public static void DealWithData()
        {
            Task task = new Task(() =>
            {
                while (true)
                {
                    while (WaitingDealQueue.Count>0)
                    {
                        string strTmp = "";
                        WaitingDealQueue.TryDequeue(out strTmp);
                        DealingList.Add(strTmp);//出队一个插入待存入列表
                        if (DealingList.Count==10000)//如果到达1000个，则存储数据库，更新实时数据
                        {
                            Save2DB();
                            CompareAndUpdateRealTimeData();
                            DealingList.Clear();//把待存入清空
                        }
                    }
                }
            });
            task.Start();

        }

        /// <summary>
        /// 分析数据，队列的大小，socket的个数等
        /// </summary>
        public static void Analisis()
        {
            Task task = new Task(() =>
            {
                int IntOldTotalMsg = 0;//上一秒的消息综述
                while (true)
                {
                    int intTmp = IntTotalMsg - IntOldTotalMsg;
                    Program.Logger.ErrorFormat("Socket数目: {0}, 缓冲队列:{1},消息数目:{2}",
                        Program.AsyncSocketSvr.AsyncSocketUserTokenList.Count(), WaitingDealQueue.Count, intTmp);
                    IntOldTotalMsg = IntTotalMsg;
                    Thread.Sleep(1000);
                }
            });
            task.Start();
        }

        /// <summary>
        /// 模拟保存数据库，保存一次花费1s
        /// </summary>
        private static void Save2DB()
        {
            Thread.Sleep(500);
        }

        /// <summary>
        /// 比对最新数据，更新实时数据，每次0.5s
        /// </summary>
        private static void CompareAndUpdateRealTimeData()
        {
            Thread.Sleep(500);
        }
    }
}