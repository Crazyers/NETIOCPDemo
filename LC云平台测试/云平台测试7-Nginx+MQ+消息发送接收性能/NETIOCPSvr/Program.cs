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
using Apache.NMS;
using Apache.NMS.ActiveMQ;
using AsyncSocketServer.AsyncSocketCore;
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
        public static int TotalCount = 0;

        //ActiveMQ消息队列的生产者和消费者。
        public static IMessageProducer Producer;
        public static IMessageConsumer Consumer;

        private static void Main(string[] args)
        {
            //日志
            InitLog();

            //消息队列
            InitActiveMQ();

            //服务端主程序
            InitServer();
            
            //控制台
            ManageClosing();
        }

        //利用异步方式进行发送
        //当1w个socket时笔记本电脑发送一次需要2.4s，117新电脑发送一次循环需要0.309s，
        private static void TestAsyncSend()
        {
            byte[] readBuffer;
            string str = "68 20 38 04 21 08 00 59 42 81 2E 90 1F 00 05 19 28 53 00 05 19 28 53 00 17 00 00 00 00 35 00 00 00 00 2C 42 33 18 00 03 19 00 10 19 00 61 27 00 26 57 23 31 03 15 20 03 01 F7 16";
            var tmpArr = str.Split(' ');
            readBuffer = new byte[tmpArr.Count()];
            for (int i = 0; i < tmpArr.Count(); i++)
            {
                string strTmp = tmpArr[i];
                int intTmp = Convert.ToInt32(strTmp, 16); //先转成10进制
                readBuffer[i] = Convert.ToByte(intTmp);
            }

            Logger.ErrorFormat("开始发送数据");
            Task task = new Task(() =>
            {
                AsyncSocketUserToken[] userTokenArray = null;
                //循环不停的发送数据
                AsyncSocketSvr.AsyncSocketUserTokenList.CopyList(ref userTokenArray);
                Monitor.SendBeginTime = DateTime.Now;
                for (int i = 0; i < userTokenArray.Length; i++)
                {
                    AsyncSocketUserToken token = userTokenArray[i];
                    AsyncSocketSvr.SendAsyncEventByServerCall(token.ConnectSocket, token.SendEventArgs, readBuffer, 0, readBuffer.Length);
                    Monitor.IntTotalMsg++;
                }
                Monitor.SendFinishedTime = DateTime.Now;
                Logger.ErrorFormat("Socket: {0},发送一次循环时间/毫秒:{1},", userTokenArray.Length, (Monitor.SendFinishedTime - Monitor.SendBeginTime).TotalMilliseconds);
                Thread.Sleep(2000);
            });
            task.Start();

            //Logger.ErrorFormat("发送数据结束，花费时间: {0}毫秒", (time2-time1).TotalMilliseconds);
        }

        //利用同步方式进行发送
        private static void TestSyncSend()
        {
            byte[] readBuffer;
            string str = "68 20 38 04 21 08 00 59 42 81 2E 90 1F 00 05 19 28 53 00 05 19 28 53 00 17 00 00 00 00 35 00 00 00 00 2C 42 33 18 00 03 19 00 10 19 00 61 27 00 26 57 23 31 03 15 20 03 01 F7 16";
            var tmpArr = str.Split(' ');
            readBuffer = new byte[tmpArr.Count()];
            for (int i = 0; i < tmpArr.Count(); i++)
            {
                string strTmp = tmpArr[i];
                int intTmp = Convert.ToInt32(strTmp, 16); //先转成10进制
                readBuffer[i] = Convert.ToByte(intTmp);
            }

            Logger.ErrorFormat("开始发送数据");
            Task task = new Task(() =>
            {
                AsyncSocketUserToken[] userTokenArray = null;
                //循环不停的发送数据
                AsyncSocketSvr.AsyncSocketUserTokenList.CopyList(ref userTokenArray);
                Monitor.SendBeginTime = DateTime.Now;
                for (int i = 0; i < userTokenArray.Length; i++)
                {
                    AsyncSocketUserToken token = userTokenArray[i];
                    AsyncSocketSvr.Send(token, readBuffer);
                    Monitor.IntTotalMsg++;
                }
                Monitor.SendFinishedTime = DateTime.Now;
                Logger.ErrorFormat("Socket: {0},发送一次循环时间/毫秒:{1},", userTokenArray.Length, (Monitor.SendFinishedTime - Monitor.SendBeginTime).TotalMilliseconds);
                Thread.Sleep(2000);
            });
            task.Start();

            //Logger.ErrorFormat("发送数据结束，花费时间: {0}毫秒", (time2-time1).TotalMilliseconds);
        }

        private static void InitLog()
        {
            DateTime currentTime = DateTime.Now;
            GlobalContext.Properties["LogDir"] = currentTime.ToString("yyyyMM");
            GlobalContext.Properties["LogFileName"] = "_SocketAsyncServer" + currentTime.ToString("yyyyMMdd");
            Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        }

        private static void InitServer()
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            int port = Convert.ToInt32(config.AppSettings.Settings["Port"].Value);
            int parallelNum = Convert.ToInt32(config.AppSettings.Settings["ParallelNum"].Value);
            ConStr = config.AppSettings.Settings["ConnectionString"].Value;
            string strLissenIP = config.AppSettings.Settings["LissenIP"].Value;
            FileDirectory = config.AppSettings.Settings["FileDirectory"].Value;
            if (FileDirectory == "")
                FileDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Files");
            if (!Directory.Exists(FileDirectory))
                Directory.CreateDirectory(FileDirectory);

            int socketTimeOutMs = 0;
            if (!(int.TryParse(config.AppSettings.Settings["SocketTimeOutMS"].Value, out socketTimeOutMs)))
                socketTimeOutMs = 5*60*1000;

            AsyncSocketSvr = new AsyncSocketServer(parallelNum);
            AsyncSocketSvr.SocketTimeOutMS = socketTimeOutMs;
            AsyncSocketSvr.Init();

            IPEndPoint listenPoint = new IPEndPoint(IPAddress.Parse(strLissenIP), port);
            AsyncSocketSvr.Start(listenPoint);
        }

        private static void InitActiveMQ()
        {
            IConnectionFactory factory = new ConnectionFactory("tcp://172.16.6.114:61616/");
            IConnection connection = factory.CreateConnection();
            ISession session = connection.CreateSession();
            Producer = session.CreateProducer(new Apache.NMS.ActiveMQ.Commands.ActiveMQTopic("testing"));
        }

        private static void ManageClosing()
        {
            while (true)
            {
                string theEntry = Console.ReadLine().ToUpper();
                switch (theEntry)
                {
                    case "A":
                        Console.WriteLine("Number of connections = " + AsyncSocketSvr.AsyncSocketUserTokenList.Count());
                        break;
                    case "B"://异步发送
                        Monitor.IntReceivedMsg = 0;
                        Monitor.Analisis();
                        TestAsyncSend(); //测试一次发送
                        break;
                    case "C"://同步发送
                        Monitor.IntReceivedMsg = 0;
                        Monitor.Analisis();
                        TestSyncSend(); //测试一次发送
                        break;
                    case "D":
                        Logger.ErrorFormat("收到消息个数：{0}", Monitor.IntReceivedMsg);
                        break;
                    default:
                        Console.WriteLine("Unrecognized entry");
                        break;
                }
            }
        }
    }

    public class Monitor
    {
        public static DateTime SendBeginTime;
        public static DateTime SendFinishedTime;
        public static DateTime ReceiveFinishedTime;
        public static int IntReceivedMsg = 0;
        public static double IntTotalMsg = 0; //启动后共接收消息数目
        public static int SaveCounts = 0; //存储次数
        public static double DblSaveTotalTime = 0; //累计存储时间
        public static double DblCurSaveTime = 0; //当前存储时间


        /// <summary>
        /// 分析数据，队列的大小，socket的个数等
        /// </summary>
        public static void Analisis()
        {
            Task task = new Task(() =>
            {
                double IntOldTotalMsg = 0; //上一秒的消息综述

                while (true)
                {
                    if (IntReceivedMsg == Program.AsyncSocketSvr.AsyncSocketUserTokenList.Count())
                    {
                            ReceiveFinishedTime = DateTime.Now;
                            Program.Logger.ErrorFormat("消息全部接收完成{0}, 发送花费时间/毫秒{1}， 接收花费时间/毫秒{2}",
                                IntReceivedMsg,
                                (SendFinishedTime - SendBeginTime).TotalMilliseconds,
                                (ReceiveFinishedTime - SendFinishedTime).TotalMilliseconds);
                            break;
                    }
                    else
                    {
                        Thread.Sleep(50);
                       
                        //double intTmp = IntTotalMsg - IntOldTotalMsg;
                        //Program.Logger.ErrorFormat("Socket: {0},发送/s:{1},",Program.AsyncSocketSvr.AsyncSocketUserTokenList.Count(),intTmp);
                        //IntOldTotalMsg = IntTotalMsg;
                        //break;
                    }
                }
            });
            task.Start();
        }
    }
}