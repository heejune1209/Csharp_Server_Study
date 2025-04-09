using ServerCore;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace DummyClient
{
    public class Packet
    {
        public ushort size; // 2byte
        // 패킷을 구분하기 위한 ID
        // ID만으로는 문제가 있는 게 
        // 패킷에 따라 사이즈가 동적으로 변할 수가 있다.
        public ushort packetId; // 2byte
    }
    class GameSession : Session
    {
        public override void OnConnected(EndPoint endPoint)
        {
            System.Console.WriteLine($"OnConnected : {endPoint}");

            Packet packet = new Packet() { size = 4, packetId = 7 };

            for (int i = 0; i < 5; i++)
            {
                ArraySegment<byte> openSegment = SendBufferHelper.Open(4096);
                byte[] buffer = BitConverter.GetBytes(packet.size);
                byte[] buffer2 = BitConverter.GetBytes(packet.packetId);

                // buffer : 복사 할 source
                // openSegment.Array : 붙여넣을 Array
                // openSegment.Offset : 복사해서 넣을 위치
                // buffer.Length : 복사 할 크기
                Array.Copy(buffer, 0, openSegment.Array, openSegment.Offset, buffer.Length);

                // openSegment.Offset + buffer.Length : buffer에서 복사하고 난 다음 위치에 복사
                Array.Copy(buffer2, 0, openSegment.Array, openSegment.Offset + buffer.Length, buffer2.Length);

                // 얼마만큼의 버퍼를 사용했는지 추적
                // 즉, SendBuffer를 다 사용하고 난 다음에 몇 바이트를 사용했는지를 확인
                ArraySegment<byte> sendBuffer = SendBufferHelper.Close(packet.size);

                Send(sendBuffer);
            }
        }

        public override void OnDisconnected(EndPoint endPoint)
        {
            System.Console.WriteLine($"OnDisconnected : {endPoint}");
        }

        public override int OnRecv(ArraySegment<byte> buffer)
        {
            string recvData = Encoding.UTF8.GetString(buffer.Array, buffer.Offset, buffer.Count);
            System.Console.WriteLine($"From Server : {recvData}");
            // 일반적인 경우라면 모든 데이터를 처리 했기 때문에 전체 갯수를 처리했다고 알림
            return buffer.Count;
        }

        public override void OnSend(int numOfBytes)
        {
            System.Console.WriteLine($"Transferred bytes : {numOfBytes}");
        }
    }
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
            connector.Connect(endPoint, () => { return new GameSession(); });

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