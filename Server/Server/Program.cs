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
    
    // 패킷으로 보내기 위해서는 사이즈를 최대한 압축하는 것이 좋다.
    // 스노우 볼 효과가 나타날 수 있음
    public class Packet
    {
        public ushort size; // 2byte
        // 패킷을 구분하기 위한 ID
        // ID만으로는 문제가 있는 게 
        // 패킷에 따라 사이즈가 동적으로 변할 수가 있다.
        public ushort packetId; // 2byte
    }
    public class LoginOkPacket : Packet
    {
        // 캐릭터의 정보를 List로 들고 있다면?
        // LoginOkPacket의 사이즈가 유동적으로 변할 수 있음
    }

    class GameSession : PacketSession
    {
        public override void OnConnected(EndPoint endPoint)
        {
            // 연결된 클라이언트의 EndPoint를 로그로 남긴다.
            System.Console.WriteLine($"OnConnected : {endPoint}");

            Packet packet = new Packet() { size = 100, packetId = 10 };

            // SendBufferHelper 호출
            // 이 호출은 현재 쓰레드에 할당된 SendBuffer(크기가 ChunkSize인 큰 버퍼)가 있는지 확인한다.
            // 만약 없다면 새 SendBuffer를 생성한다.
            // 그리고 reserveSize(여기서는 4096 바이트)를 위해 버퍼 내의 사용 가능한 공간을 예약한 ArraySegment<byte>를 반환한다.
            ArraySegment<byte> openSegment = SendBufferHelper.Open(4096);
            byte[] buffer = BitConverter.GetBytes(packet.size);
            byte[] buffer2 = BitConverter.GetBytes(packet.packetId);

            // buffer : 복사 할 source
            // openSegment.Array : 붙여넣을 Array
            // openSegment.Offset : 복사해서 넣을 위치
            // buffer.Length : 복사 할 크기
            // 데이터 복사
            // 반환된 openSegment에 BitConverter로 변환한 Knight의 hp와 attack값을 Array.Copy()로 복사한다.
            Array.Copy(buffer, 0, openSegment.Array, openSegment.Offset, buffer.Length);

            // openSegment.Offset + buffer.Length : buffer에서 복사하고 난 다음 위치에 복사
            Array.Copy(buffer2, 0, openSegment.Array, openSegment.Offset + buffer.Length, buffer2.Length);

            // 얼마만큼의 버퍼를 사용했는지 추적
            // 즉, SendBuffer를 다 사용하고 난 다음에 몇 바이트를 사용했는지를 확인

            // SendBufferHelper.Close()를 호출
            ArraySegment<byte> sendBuff = SendBufferHelper.Close(buffer.Length + buffer2.Length);
            // 여기서 totalDataSize는 복사한 바이트 수의 총합이다.
            // Close()는 실제로 사용한 영역을 확정(커서 이동)하고, 그 구간을 ArraySegment<byte>로 반환한다.

            // 최종적으로 sendBuff (즉, 복사한 버퍼의 실제 사용 영역)를 인자로 하여 Session의 Send() 메서드를 호출해서 전송을 요청한다.
            Send(sendBuff);
            Thread.Sleep(1000);
            Disconnect();
        }

        // buffer : 1개의 유효한(완전한) 패킷
        // 처음 2바이트 : 패킷의 사이즈
        // 다음 2바이트 : 패킷의 ID
        // 1개의 완전한 패킷을 받았을 때 어떤 처리를 할지 결정
        public override void OnRecvPacket(ArraySegment<byte> buffer)
        {
            ushort size = BitConverter.ToUInt16(buffer.Array, buffer.Offset);
            ushort id = BitConverter.ToUInt16(buffer.Array, buffer.Offset + 2);
            Console.WriteLine($"Recive Package size : {size}, ID : {id}");
        }
        // 중요한 선수 작업이다.
        // 패킷을 설계하기 앞서서 2바이트로 패킷의 사이즈를 먼저 확인 한 다음에
        // 전체 패킷을 조립해서 넘겨주는 작업까지 완료하게 됨

        public override void OnDisconnected(EndPoint endPoint)
        {
            Console.WriteLine($"OnDisconnected : {endPoint}");
        }

        

        public override void OnSend(int numOfBytes)
        {
            Console.WriteLine($"Transferred bytes : {numOfBytes}");
        }
    }
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