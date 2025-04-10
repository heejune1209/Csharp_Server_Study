using ServerCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DummyClient
{
    // 패킷 헤더
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

    class ServerSession : Session
    {
        public override void OnConnected(EndPoint endPoint)
        {
            System.Console.WriteLine($"OnConnected : {endPoint}");

            PlayerInfoReq packet = new PlayerInfoReq() { playerId = 1001 };

            // for (int i = 0; i < 5; i++)
            {
                // SendBuffer를 통해 보낼 패킷의 정보를 하나의 ArraySegment에 밀어 넣은 다음에 해당 값을 반환
                // Write함수 안에서 얼마만큼의 버퍼를 사용했는지 추적
                // 즉, SendBuffer를 다 사용하고 난 다음에 몇 바이트를 사용했는지를 확인
                ArraySegment<byte> s = packet.Write();

                if (s != null)
                    Send(s);
            }
        }
        // 이렇게 PlayerInfoReq packet을 만들어서 버퍼에다가 밀어넣는 작업을 직렬화라고 한다.
        // 그리고 직렬화를 해서 Send를 한 다음에
        // 반대쪽에 서버에서도 역직렬화를 해가지고 버퍼에 있는 거를 꺼내서 쓰는 작업을 해봤다.

        // 이번엔 직렬화 하는 과정을 자동화하는데 있어서, 다른 인터페이스로 만들어보았다


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
}
