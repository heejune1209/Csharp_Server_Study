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
        public string name;
        public PlayerInfoReq()
        {
            this.packetId = (ushort)PacketId.PlayerInfoReq;
        }

        public override void Read(ArraySegment<byte> segment)
        {
            ushort count = 0;
            ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
            count += sizeof(ushort);
            count += sizeof(ushort);

            this.playerId = BitConverter.ToInt64(s.Slice(count, s.Length - count));

            count += sizeof(long);

            // string
            // s.Slice(count, s.Length - count) : byte array에서 string "크기" 값 있는 부분을 집어서 ushort로 변환
            ushort nameLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
            count += sizeof(ushort);

            // GetString : byte에서 string으로 변환을 해주는 과정
            // byte array에서 "실제 string의 값"이 있는 부분을 집어서 string 타입으로 변환
            this.name = Encoding.Unicode.GetString(s.Slice(count, nameLen));
        }

        public override ArraySegment<byte> Write()
        {
            ArraySegment<byte> segment = SendBufferHelper.Open(4096);
            ushort count = 0;
            bool success = true;

            Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);
            count += sizeof(ushort);

            // Slice => Span을 사용할 때 현재 있는 범위에 일부분을 다시 선택할 때 사용
            // count : 시작 위치
            // s.Length - count : 범위(시작 위치에서부터 해당 범위 만큼을 찝어준다.)
            // 어떻게 동작? : Span으로 만든 바이트 Array에서 Slice한 부분에 packetId를 넣어준다.
            success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.packetId);
            count += sizeof(ushort);
            success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.playerId);
            count += sizeof(long);
            // count에 대해서 TryWriteBytes를 한 다음 해당 크기를 count에 반영하지 않는 이유
            // 가장 처음에 했던 count += sizeof(ushort)가 해당 count를 의미함

            // string
            // UTF-8, UTF-16인지는 case by case이다. 프로젝트마다 다름
            // string의 length가 얼마인지를 2바이트 짜리로 만들 필요가 있고(2바이트인 이유는 UTF-8이기 때문).
            // 해당 크기를 byte array로 이어 보내도록 해보자
            // 즉 string의 크기를 먼저 확인하고 실제 string 값을 보내는 방법을 사용

            // string.Length를 쓰면 string의 크기 값은 알 수 있으나
            // Length를 byte array로 변환을 하면 Length는 int 타입이므로 8바이트 크기로 보내게 된다.
            // 따라서 Length를 직접적으로 사용할 수는 없고 string을 UTF-16 기준으로 바이트 배열의 크기를 나타낼 수 있는 방법을 찾게 됨
            // => UTF-16을 사용하기 위해서는 Encoding.Unicode로 접근하면 된다.

            // GetBytes의 다른 버전
            // this.name : 대상 소스
            // 0 : 대상의 시작점
            // this.name.Length : 대상의 길이
            // segment.Array : 소스를 넣을 byte array
            // segment.Offset + count : 소스를 넣을 위치
            // return : string을 byte array로 변한 한 후 크기
            // segment.Offset + count + sizeof(ushort)의 의미 : string의 값을 먼저 byte array에 넣고 있기 때문에 
            // string의 크기를 넣어줄 공간을 먼저 만들어 주고 그 다음에 string의 값을 밀어넣어주기 위해서이다.
            ushort nameLen = (ushort)Encoding.Unicode.GetBytes(this.name, 0, this.name.Length, segment.Array, segment.Offset + count + sizeof(ushort));
            success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), nameLen);
            count += sizeof(ushort);
            count += nameLen;
            

            success &= BitConverter.TryWriteBytes(s, count);

            if (success == false)
            {
                return null;
            }

            return SendBufferHelper.Close(count);
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
        // 각자의 대리자가 되어 요청을 처리하는 역할
        public class ClientSession : PacketSession
        {
            public override void OnConnected(EndPoint endPoint)
            {
                // 연결된 클라이언트의 EndPoint를 로그로 남긴다.
                System.Console.WriteLine($"OnConnected : {endPoint}");
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
                            System.Console.WriteLine($"PlayerInfoReq : {p.playerId} {p.name}");
                        }
                        break;
                }
                System.Console.WriteLine($"Recive Package size : {size}, ID : {packetId}");

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
                Console.WriteLine($"OnDisconnected : {endPoint}");
            }



            public override void OnSend(int numOfBytes)
            {
                Console.WriteLine($"Transferred bytes : {numOfBytes}");
            }
        }

    }
}