using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using System.Configuration;

namespace NETUploadClient
{
    class Program
    {
        public static int PacketSize = 32 * 1024;

        static void Main(string[] args)
        {
            TestHeatLiChuang testThroughput = new TestHeatLiChuang();
            Console.WriteLine("Starting..........");
            testThroughput.Init();
            Console.ReadKey();
        }
    }
}
