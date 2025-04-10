using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using static System.Collections.Specialized.BitVector32;
using ServerCore;

namespace Server
{
    
    class ServerProgram
    {
        // 해당 리스너는 프로그램 전체에서 하나의 인스턴스로 공유.
        // 어디서든 접근할 수 있게 할수 있다.
        static Listener _listener = new Listener();
        static void Main(string[] args)
        {
            // 서버의 IP와 포트를 결정
            string host = Dns.GetHostName();
            IPHostEntry ipHost = Dns.GetHostEntry(host);
            IPAddress ipAddr = ipHost.AddressList[0];
            IPEndPoint endPoint = new IPEndPoint(ipAddr, 7777);

            // 문지기 배치
            // 이때, Init()에 세션 생성 팩토리 함수(예: () => new GameSession())도 함께 전달하여
            // 클라이언트가 접속할 경우 세션을 어떤 방식으로 만들지를 결정
            _listener.Init(endPoint, () => { return new ClientSession(); }); // 세션을 만드는 함수 등록
            Console.WriteLine("Listening...");

            // 프로그램이 종료되지 않도록 임시록 남겨둠
            // 여기서 메인쓰레드는 무한 반복을 돌고 있지만
            // 어떻게 OnAcceptHandler가 콜백함수가 실행이 되는 걸까?
            // AcceptAsync()를 실행할 때 SocketAsyncEventArgs를 넣어주면 
            // 콜백 함수는 쓰레드 풀에서 실행이 됨
            // 따라서 Listener의 OnAcceptCompleted를 레드존이라 생각하고
            // 메인 쓰레드와 경합 조건이 발생하지 않도록 고려해야 한다.      
            while (true)
            {

            }

        }
    }
}