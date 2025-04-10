using ServerCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

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
    public abstract class Packet
    {
        public ushort size; // 2
        public ushort packetId; // 2
        public abstract ArraySegment<byte> Write();
        public abstract void Read(ArraySegment<byte> s);
    }

    class PlayerInfoReq : Packet
    {
        public long playerId; // 8
        public PlayerInfoReq()
        {
            // PlayerInfoReq의 PacketID는 이미 정해져 있으므로 생성자를 통해 초기화
            this.packetId = (ushort)PacketId.PlayerInfoReq;
        }


        // Read는 서버에 있는 클라이언트 세션에서 클라이언트가 보낸 패킷을 받았을 때
        // byte array로 보낸 패킷을 읽어서 패킷 아이디에 따라 패킷의 정보를 읽고(== 역직렬화)
        // 새롭게 생성한 해당 패킷의 객체에 할당하는 과정이다.
        // Read는 받은 패킷을 하나씩 까보면서 값을 할당하고 있기 때문에 
        // 조심해야할 필요가 있다.
        public override void Read(ArraySegment<byte> s)
        {
            ushort count = 0;
            // ushort size = BitConverter.ToUInt16(s.Array, s.Offset);
            count += 2;
            // ushort packetId = BitConverter.ToUInt16(s.Array, s.Offset + count);
            count += 2;

            // 전달 받은 패킷에 있던 playerId를 자신의 playerId에 할당
            // ReadOnlySpan
            // byte array에서 내가 원하는 범위에 있는 값만 읽어서 역직렬화 한다.
            // s.Array : 대상 byte array
            // s.Offset + count : 시작 위치
            // s.Count - count : 원하는 범위 => 전달 받은 byte array 크기 - 앞에서 빼는 패킷 데이터 크기 
            // => 실제로 읽어야할 byte array 크기
            // byte array의 크기가 실제로 읽어야할 byte array 크기와 다르면 Exception을 뱉어 낸다.
            // 위 Exception을 통해 패킷 조작을 의심할 수 있고 바로 Disconnect 시킬 수 있다.
            this.playerId = BitConverter.ToInt64(new ReadOnlySpan<byte>(s.Array, s.Offset + count, s.Count - count));
            count += 8;
        }

        // SendBuffer를 통해 보낼 패킷의 정보를 하나의 ArraySegment에 밀어 넣은 다음에 해당 값을 반환
        // write의 경우 패킷에 원하는 값은 넣는 모든 과정을 직접 컨트롤 하고 있기 때문에 별 문제가 없다.       
        public override ArraySegment<byte> Write()
        {
            ArraySegment<byte> s = SendBufferHelper.Open(4096);
            ushort count = 0;
            bool success = true;

            // 클라로 패킷을 전달하기 전에 ArraySegment에 패킷의 정보를 밀어 넣는 작업
            // success &= BitConverter.TryWriteBytes(new Span<byte>(s.Array, s.Offset, s.Count), this.size);
            count += 2;
            success &= BitConverter.TryWriteBytes(new Span<byte>(s.Array, s.Offset + count, s.Count - count), this.packetId);
            count += 2;
            success &= BitConverter.TryWriteBytes(new Span<byte>(s.Array, s.Offset + count, s.Count - count), this.playerId);
            count += 8;
            // 패킷 헤더에 사이즈를 잘못 보낸다면?
            // 서버에서는 사이즈만 잘못 받을 뿐 실제로는 잘 동작한다.
            // 여기서 결론은 패킷의 헤더 정보를 믿어서는 안되고 참고만 하는 값이다.
            success &= BitConverter.TryWriteBytes(new Span<byte>(s.Array, s.Offset, s.Count), count);

            if (success == false)
            {
                return null;
            }

            return SendBufferHelper.Close(count);
        }
        // Write는 클라이언트에 있는 서버 세션에서 서버와 연결이 되었을 때 패킷의 정보를 보내는 때
        // 이때 보내야할 패킷의 정보를 쓰고(== byte array로 직렬화) 해당 패킷의 정보를 send 한다.
    }
    // 서버에서 클라로 답변을 준다.
    // 서버에서 클라로 플레이어 정보를 알고 싶어 
    // 근데 플레이어 정보는 PlayerId로 요청을 했을 때
    // hp와 attack의 정보를 반환
    //class PlayerInfoOk : Packet
    //{
    //    public int hp;
    //    public int attack;
    //}

    // 패킷 아이디로 패킷을 구분
    // 나중에는 자동화를 할 예정
    public enum PacketId
    {
        PlayerInfoReq = 1,
        PlayerInfoOk = 2,
    }

    // 세션은 대리자의 역할을 한다. 
    // 즉, 더미클라이언트에 서버세션 스크립트를 만들고,
    // 서버쪽에 클라이언트 세션스크립트를 만든 이유는
    // 클라쪽에 서버의 대리자가 서버 세션이고,
    // 반대로 서버쪽에 클라의 대리자가 클라이언트 세션이다.
    class ClientSession : PacketSession
    {
        public override void OnConnected(EndPoint endPoint)
        {
            /*
            // 연결된 클라이언트의 EndPoint를 로그로 남긴다.
            System.Console.WriteLine($"OnConnected : {endPoint}");

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
            Thread.Sleep(1000);
            Disconnect();
        }

        // buffer : 1개의 유효한(완전한) 패킷
        // 처음 2바이트 : 패킷의 사이즈
        // 다음 2바이트 : 패킷의 ID
        // 1개의 완전한 패킷을 받았을 때 어떤 처리를 할지 결정
        public override void OnRecvPacket(ArraySegment<byte> buffer)
        {
            ushort count = 0;
            ushort size = BitConverter.ToUInt16(buffer.Array, buffer.Offset);
            count += 2;
            ushort packetId = BitConverter.ToUInt16(buffer.Array, buffer.Offset + count);
            count += 2;

            switch ((PacketId)packetId)
            {
                case PacketId.PlayerInfoReq:
                    {
                        PlayerInfoReq p = new PlayerInfoReq();
                        p.Read(buffer); // 역직렬화
                        //count += 8;
                        System.Console.WriteLine($"PlayerInfoReq : {p.playerId}");
                    }
                    break;
            }
            System.Console.WriteLine($"Recive Package size : {size}, ID : {packetId}");
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
}
