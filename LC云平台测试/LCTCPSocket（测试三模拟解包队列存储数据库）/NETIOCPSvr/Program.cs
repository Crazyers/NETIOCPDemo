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
        public static string ConStr; 
        static void Main(string[] args)
        {
            DateTime currentTime = DateTime.Now;
            
            log4net.GlobalContext.Properties["LogDir"] = currentTime.ToString("yyyyMM");
            log4net.GlobalContext.Properties["LogFileName"] = "_SocketAsyncServer" + currentTime.ToString("yyyyMMdd");
            Logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            ConStr = config.AppSettings.Settings["ConnectionString"].Value;
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

            Framework.Start();

            AsyncSocketSvr = new AsyncSocketServer(parallelNum);
            AsyncSocketSvr.SocketTimeOutMS = socketTimeOutMS;
            AsyncSocketSvr.Init();
            //IPEndPoint listenPoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), port);
            // IPEndPoint listenPoint = new IPEndPoint(IPAddress.Parse("172.16.12.105"), port);172.16.6.114
            //IPEndPoint listenPoint = new IPEndPoint(IPAddress.Parse("172.16.6.113"), port);
            IPEndPoint listenPoint = new IPEndPoint(IPAddress.Parse("172.16.6.11"), port);
            //IPEndPoint listenPoint = new IPEndPoint(IPAddress.Parse("172.16.6.102"), port);
            //IPEndPoint listenPoint = new IPEndPoint(IPAddress.Parse("172.16.6.114"), port);
            AsyncSocketSvr.Start(listenPoint);

            //运行一个线程，监控全局队列的数目
            Analysis.Analisis();
           
            Console.WriteLine("Press any key to terminate the server process....");
            Console.ReadKey();
        }
    }

    public class Analysis
    {
        public static double IntTotalMsg = 0;//启动后共接收消息数目
        public static int SaveCounts = 0;//存储次数
        public static double DblSaveTotalTime = 0;//累计存储时间
        public static double DblCurSaveTime = 0;//当前存储时间

        /// <summary>
        /// 分析数据，队列的大小，socket的个数等
        /// </summary>
        public static void Analisis()
        {
            Task task = new Task(() =>
            {
                double IntOldTotalMsg = 0;//上一秒的消息综述
                while (true)
                {
                    double intTmp = IntTotalMsg - IntOldTotalMsg;
                    Program.Logger.ErrorFormat("Socket: {0}, 缓冲队列:{1},每秒接收:{2},Save:{3}ms,平均Save:{4}ms,save:{5}次",
                        Program.AsyncSocketSvr.AsyncSocketUserTokenList.Count(), 
                        Framework.QueueWaitingCjdHeats.Count, 
                        intTmp,
                        DblCurSaveTime.ToString("F2"),
                        (DblSaveTotalTime/SaveCounts).ToString("F2"),
                        SaveCounts);
                    IntOldTotalMsg = IntTotalMsg;
                    Thread.Sleep(1000);
                    //break;
                }
            });
            task.Start();
        }
    }
}