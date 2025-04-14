using ServerCore;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace DummyClient
{
    
    class ClientProgram
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
            // 커넥터는 클라이언트 입장에서 서버에 연결 요청 (ConnectAsync())을 하는 역할.
            // 100명의 세션을 생성할 수 있도록 설정
            connector.Connect(endPoint, () => { return SessionManager.Instance.Generate(); }, 100);

            while (true)
            {               

                try
                {
                    SessionManager.Instance.SendForEach();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }

                Thread.Sleep(250); // 1초 대기
            }
        }
    }
}