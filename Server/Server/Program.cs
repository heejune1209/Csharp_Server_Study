using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using static System.Collections.Specialized.BitVector32;
using ServerCore;

namespace Server
{
    // 엔진(코어)부분과 컨텐츠 부분을 분리시켜줬다.
    // 컨텐츠 단에서는 Session을 override한 인터페이스들을 사용하는 정도
    // 아래는 컨텐츠 코드들임.

    // 결국 Sever가 컨텐츠를 관리하는 곳이 되는거고 
    // ServerCore가 엔진이 되는 것이다.
    // Server에서는 Session의 인터페이스만 가지고 와서 사용
    class GameSession : Session
    {
        public override void OnConnected(EndPoint endPoint)
        {
            Console.WriteLine($"OnConnected : {endPoint}");
            byte[] sendBuff = Encoding.UTF8.GetBytes("Welcome to MMORPG Server!");
            Send(sendBuff);
            Thread.Sleep(1000);
            Disconnect();
        }

        public override void OnDisconnected(EndPoint endPoint)
        {
            Console.WriteLine($"OnDisconnected : {endPoint}");
        }

        public override void OnRecv(ArraySegment<byte> buffer)
        {
            string recvData = Encoding.UTF8.GetString(buffer.Array, buffer.Offset, buffer.Count);
            Console.WriteLine($"From Server : {recvData}");
        }

        public override void OnSend(int numOfBytes)
        {
            Console.WriteLine($"Transferred bytes : {numOfBytes}");
        }
    }
    class ServerProgram
    {
        static Listener _listener = new Listener();
        static void Main(string[] args)
        {
            string host = Dns.GetHostName();

            IPHostEntry ipHost = Dns.GetHostEntry(host);
            IPAddress ipAddr = ipHost.AddressList[0];
            IPEndPoint endPoint = new IPEndPoint(ipAddr, 7777);

             

            // 문지기 배치
            // 세션을 어떤 방식으로 만들지를 결정
            _listener.Init(endPoint, () => { return new GameSession(); }); // 세션을 만드는 함수 등록
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