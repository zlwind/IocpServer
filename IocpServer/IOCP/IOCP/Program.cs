using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace IOCP
{
    class Program
    {
        static void Main(string[] args)
        {
            byte[] b = new byte[] { 192, 168, 2, 104 };
            IPAddress ip = new IPAddress(b);
            IPEndPoint ie = new IPEndPoint(ip, 8080);
            IocpServer server = new IocpServer(ie,1024);
            server.Start();
          //  server._maxAcceptClient.Release(1);
            Console.WriteLine("服务器已启动....");
            System.Console.ReadLine();
        }
    }
}
