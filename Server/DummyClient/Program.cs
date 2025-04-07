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

            while(true)
            {
                // 휴대폰 설정
                // ProtocolType.Tcp로 결정을 하면 SocketType도 Stream으로 자동으로 결정이 된다.
                Socket socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                try
                {
                    // 문지기한테 입장 문의를 함
                    // 상대방의 주소를 넣어줘야함
                    // 문지기한테 연락이 닿을 때까지 대기
                    socket.Connect(endPoint);

                    // 서버의 정보를 출력
                    // 연결한 대상의 서버의 정보
                    Console.WriteLine($"Connected to {socket.RemoteEndPoint.ToString()}"); // RemoteEndPoint는 우리가 연결한 반대쪽 대상

                    // 서버로 보낸다
                    for (int i = 0; i < 5; i++)
                    {
                        // 서버로 보낼 데이터
                        byte[] sendBuff = Encoding.UTF8.GetBytes($"Hello World! {i}");
                        int sendByte = socket.Send(sendBuff);
                    }                    
                    
                    // 서버로부터 받는다
                    byte[] recvBuff = new byte[1024];
                    int recvBytes = socket.Receive(recvBuff);
                    string recvData = Encoding.UTF8.GetString(recvBuff, 0, recvBytes);
                    Console.WriteLine($"[From server] {recvData}");

                    // 나간다
                    socket.Shutdown(SocketShutdown.Both);
                    socket.Close();
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