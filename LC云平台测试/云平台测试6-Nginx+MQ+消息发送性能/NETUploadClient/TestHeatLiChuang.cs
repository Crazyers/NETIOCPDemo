using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NETUploadClient.SyncSocketProtocol;

namespace NETUploadClient
{
    public class TestHeatLiChuang
    {
        private readonly Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        private int intThroughputSendPoint { get; set; } //多少个采集器
        private int intParallelThroughPutNum { get; set; } //并行多少进程
        private int intThroughputPackage { get; set; } //每个包的大小，单位字节
        private int intThroughputSendInterval { get; set; } //多长时间发一次包，单位毫秒
        private int intThroughputSendCount { get; set; } //多长时间发一次包，单位毫秒
        private string strLissenIp { get; set; }//连接的服务器IP
        private int intLissenPort { get; set; }//连接的服务器IP
        private List<Task> tasks;

        private double tmp = 0;
        public static int IntConnectedNum = 0; //已经连接上的
        private byte[] readBuffer;

        public TestHeatLiChuang()
        {
            intThroughputSendPoint = int.Parse(config.AppSettings.Settings["ThroughputSendPoint"].Value);
            intParallelThroughPutNum = int.Parse(config.AppSettings.Settings["ThroughputParallelNum"].Value);
            intThroughputPackage = int.Parse(config.AppSettings.Settings["ThroughputPackage"].Value);
            intThroughputSendInterval = int.Parse(config.AppSettings.Settings["ThroughputSendInterval"].Value);
            intThroughputSendCount = int.Parse(config.AppSettings.Settings["ThroughputSendCount"].Value);
            strLissenIp = config.AppSettings.Settings["LissenIP"].Value;
            intLissenPort = int.Parse(config.AppSettings.Settings["Port"].Value);
            tasks = new List<Task>(intParallelThroughPutNum); //生成多少个线程
        }

        /// <summary>
        /// 初始化，线程池等
        /// </summary>
        public void Init()
        {
            string str = "68 20 38 04 21 08 00 59 42 81 2E 90 1F 00 05 19 28 53 00 05 19 28 53 00 17 00 00 00 00 35 00 00 00 00 2C 42 33 18 00 03 19 00 10 19 00 61 27 00 26 57 23 31 03 15 20 03 01 F7 16";
            var tmpArr = str.Split(' ');
            readBuffer = new byte[tmpArr.Count()];
            for (int i = 0; i < tmpArr.Count(); i++)
            {
                string strTmp = tmpArr[i];
                int intTmp = Convert.ToInt32(strTmp, 16);//先转成10进制
                readBuffer[i] = Convert.ToByte(intTmp);
            }

            //1000个，现在是200个线程，每个线程5个Socket，也就是共1000个Socket
            for (int index = 0; index < intParallelThroughPutNum; index++)
            {
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
            ClientHeatLiChuangProtocol liChuangSocket1 = new ClientHeatLiChuangProtocol();
            ClientHeatLiChuangProtocol liChuangSocket2 = new ClientHeatLiChuangProtocol();
            ClientHeatLiChuangProtocol liChuangSocket3= new ClientHeatLiChuangProtocol();
            ClientHeatLiChuangProtocol liChuangSocket4 = new ClientHeatLiChuangProtocol();
            ClientHeatLiChuangProtocol liChuangSocket5 = new ClientHeatLiChuangProtocol();
         
            liChuangSocket1.Connect(strLissenIp, intLissenPort);
            liChuangSocket2.Connect(strLissenIp, intLissenPort);
            liChuangSocket3.Connect(strLissenIp, intLissenPort);
            liChuangSocket4.Connect(strLissenIp, intLissenPort);
            liChuangSocket5.Connect(strLissenIp, intLissenPort);
            Console.WriteLine("Connect Server Success---" + IntConnectedNum);
            int intCount = 0;
            //循环发送
            //while (true)
            //{
            //    liChuangSocket1.SendData(readBuffer, 0, 0);
            //    liChuangSocket2.SendData(readBuffer, 0, 0);
            //    liChuangSocket3.SendData(readBuffer, 0, 0);
            //    liChuangSocket4.SendData(readBuffer, 0, 0);
            //    liChuangSocket5.SendData(readBuffer, 0, 0);
             
            //    //Console.WriteLine("Send msg finished!");
            //    //多长时间发一次
            //    Thread.Sleep(1000);
            //    intCount++;
            //}
        }
    }
}
    

