using ServerCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    // 세션은 대리자의 역할을 한다. 
    // 즉, 더미클라이언트에 서버세션 스크립트를 만들고,
    // 서버쪽에 클라이언트 세션스크립트를 만든 이유는
    // 클라쪽에 서버의 대리자가 서버 세션이고,
    // 반대로 서버쪽에 클라의 대리자가 클라이언트 세션이다.
    // 각자의 대리자가 되어 요청을 처리하는 역할
    class ClientSession : PacketSession
    {
        public int SessionId { get; set; } // 세션 ID
        public GameRoom Room { get; set; } // 현재 어떤 방에 있는지
        public override void OnConnected(EndPoint endPoint)
        {
            // 연결된 클라이언트의 EndPoint를 로그로 남긴다.
            Console.WriteLine($"OnConnected : {endPoint}");
            // 서버에 클라이언트가 접속을 했다면 강제로 채팅방에 들어오게 만듬
            // 하지만 실제 게임에서는 클라이언트 쪽에서 모든 리소스 업데이트가 완료 되었을 때 
            // 서버에 신호를 보내고 그때 채팅방에 들어오는 작업을 해줘야 한다.

            ServerProgram.Room.Enter(this); // 방에 들어간다.
            /*               
            Packet packet = new Packet() { size = 4, packetId = 7 };

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
            */
            Thread.Sleep(5000);
            Disconnect();
        }

        // buffer : 1개의 유효한(완전한) 패킷
        // 처음 2바이트 : 패킷의 사이즈
        // 다음 2바이트 : 패킷의 ID
        // 1개의 완전한 패킷을 받았을 때 어떤 처리를 할지 결정
        public override void OnRecvPacket(ArraySegment<byte> buffer)
        {
            PacketManager.Instance.OnRecvPacket(this, buffer);

            // 데이터 수신 처리 흐름
            // 서버 측에서는 PacketSession을 상속받은 ClientSession이 사용됩니다.
            // 수신된 데이터는 먼저 OnRecvComplected()에서 RecvBuffer를 통해 읽혀지고,
            // PacketSession의 sealed OnRecv()가 호출되어, 도착한 바이트 스트림을 반복적으로 검사하여 완전한 패킷을 분리합니다.
            // 분리된 한 개의 패킷 단위(ArraySegment<byte>)는 OnRecvPacket()의 인자로 전달됩니다.

            // OnRecvPacket()에서는 전달된 패킷의 첫 2바이트(헤더)를 읽어 패킷 사이즈, 그 다음 2바이트를 읽어 패킷 ID를 확인합니다.
            // 예를 들어, switch-case 문으로 PacketId를 확인하면, 만약 패킷 ID가 PlayerInfoReq이면, 새 PlayerInfoReq 객체를 생성하고,
            // 그 객체의 Read() 메서드를 호출해 받은 데이터(역직렬화)를 수행합니다.
            // 이후, 디버깅용 로그 메시지(예: "Recive Package size : {size}, ID : {id}")를 출력하여,
            // 패킷이 잘 수신되었음을 보여줍니다.

            // 즉, 서버 측 ClientSession에서는 클라이언트로부터 온 패킷 데이터를 OnRecvPacket()에서 역직렬화하여,
            // PlayerInfoReq와 같은 구체적인 패킷 처리 로직을 실행하게 됩니다.

            // Serialization #2 수업 정리
            // 서버(ClientSession) 측
            // OnRecvPacket():
            // → 클라이언트(또는 더미 클라이언트에서 보낸)로부터 패킷 데이터가 도착하면,
            // → PacketSession의 OnRecv() 로직을 거쳐 OnRecvPacket()으로 전달됨
            // → 전달된 패킷의 헤더를 분석하고, PacketId를 확인하여,
            // → 만약 PlayerInfoReq 패킷이면, PlayerInfoReq의 Read()를 통해 역직렬화하고,
            // → 처리된 내용을 로그로 출력하거나 추가 작업 수행

        }
        // 중요한 선수 작업이다.
        // 패킷을 설계하기 앞서서 2바이트로 패킷의 사이즈를 먼저 확인 한 다음에
        // 전체 패킷을 조립해서 넘겨주는 작업까지 완료하게 됨

        public override void OnDisconnected(EndPoint endPoint)
        {
            SessionManager.Instance.Remove(this);
            
            // 방에서 나간다.
            if (Room != null)
            {
                Room.Leave(this);
                Room = null;
            }
                
            
            Console.WriteLine($"OnDisconnected : {endPoint}");
        }



        public override void OnSend(int numOfBytes)
        {
            Console.WriteLine($"Transferred bytes : {numOfBytes}");
        }
        

    }
}