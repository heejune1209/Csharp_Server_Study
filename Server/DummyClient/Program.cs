using ServerCore;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace DummyClient
{
    
    class Program
    {
        static void Main(string[] args)
        {
            // 식당의 주소를 찾는 과정은 동일함
            string host = Dns.GetHostName();
            IPHostEntry ipHost = Dns.GetHostEntry(host);
            IPAddress ipAddr = ipHost.AddressList[0];
            IPEndPoint endPoint = new IPEndPoint(ipAddr, 7777);

            Connector connector = new Connector();
            // 의존성 주입
            connector.Connect(endPoint, () => { return new ServerSession(); });

            while (true)
            {               

                try
                {
                    
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }

                Thread.Sleep(100); // 1초 대기
            }
        }
    }
}