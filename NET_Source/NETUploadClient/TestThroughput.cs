using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NETUploadClient.SyncSocketProtocol;

namespace NETUploadClient
{
    /// <summary>
    /// 测试并发量
    /// </summary>
    public class TestThroughput
    {
        private readonly Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        private int intThroughputSendPoint { get; set; } //多少个采集器
        private int intParallelThroughPutNum { get; set; } //并行多少进程
        private int intThroughputPackage { get; set; } //每个包的大小，单位字节
        private int intThroughputSendInterval { get; set; } //多长时间发一次包，单位毫秒
        private int intThroughputSendCount { get; set; } //多长时间发一次包，单位毫秒
        private List<Task> tasks;
        private List<ClientThroughputSocket> sockets;

        private double tmp = 0;
        private int intConnectedNum = 0;//已经连接上的

        public TestThroughput()
        {
            intThroughputSendPoint = int.Parse(config.AppSettings.Settings["ThroughputSendPoint"].Value);
            intParallelThroughPutNum = int.Parse(config.AppSettings.Settings["ThroughputParallelNum"].Value);
            intThroughputPackage = int.Parse(config.AppSettings.Settings["ThroughputPackage"].Value);
            intThroughputSendInterval = int.Parse(config.AppSettings.Settings["ThroughputSendInterval"].Value);
            intThroughputSendCount = int.Parse(config.AppSettings.Settings["ThroughputSendCount"].Value);
            tasks = new List<Task>(intParallelThroughPutNum); //生成多少个线程
        }

        /// <summary>
        /// 初始化，线程池等
        /// </summary>
        public void Init()
        {
            //1000个
            for (int index = 0; index < intParallelThroughPutNum; index++)
            {
                intConnectedNum = index;
                //Console.WriteLine(index);
                Task task = new Task(() =>
                {
                    InitTask();
                });
                task.Start();
                //tasks.Add(task);//把所有的任务 都初始化好
            }
        }

        protected void InitTask()
        {
            Console.WriteLine("Starting Connect Server");
            ClientThroughputSocket throughputSocket = new ClientThroughputSocket();
            //throughputSocket.Connect("172.16.6.114", 9999);
            //throughputSocket.Connect("172.16.6.113", 9999);
            throughputSocket.Connect("172.16.6.102", 9999);
            Console.WriteLine("Connect Server Success---" + intConnectedNum);
            //throughputSocket.DoActive();
            //throughputSocket.DoLogin("admin", "admin");
            //Console.WriteLine("Login Server Success");
            intConnectedNum++;

            int intCount = 0;
            //循环发送
            while (true)
            {
                    byte[] readBuffer = new byte[intThroughputPackage];
                    //初始化
                    for (int j = 0; j < readBuffer.Length; j++)
                    {
                        readBuffer[j] = 1;
                    }
                    if (!throughputSocket.DoData(readBuffer, 0, intThroughputPackage))
                        throw new Exception(throughputSocket.ErrorString);
                    Console.WriteLine("Send msg finished!");
                    //多长时间发一次
                    Thread.Sleep(1000);
                    intCount++;
                //}
            }
        }
    }
}
    

