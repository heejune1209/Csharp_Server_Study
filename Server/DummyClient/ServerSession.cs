using ServerCore;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DummyClient
{
    // 패킷 자동화를 하기 위해서 패킷 클래스를 지움 
    //public abstract class Packet
    //{
    //    public ushort size; // 2
    //    public ushort packetId; // 2
    //    public abstract ArraySegment<byte> Write();
    //    public abstract void Read(ArraySegment<byte> s);
    //}
    // PlayerInfoReq 클래스
    // Packet을 상속받아, 플레이어 요청에 필요한 정보를 담습니다.
    // 예를 들어 playerId를 필드로 가지고 있으며,
    // Write() 메서드에서는 자신의 데이터를 바이트 배열로 직렬화하고,
    // Read() 메서드에서는 받은 바이트 배열에서 데이터를 추출합니다.
    class PlayerInfoReq
    {
        public long playerId; // 8바이트
        public string name;

        public struct SkillInfo
        {
            public int id; // 4바이트
            public short level; // 2바이트
            public float duration; // 4바이트
            // 스킬 하나 마다 byte array에 밀어 넣어주기 위한 인터페이스
            // return value가 boolean 타입인 이유 : TryWriteBytes와 인터페이스를 맞추기 위해서
            public bool Write(Span<byte> s, ref ushort count)
            {
                // SkillInfo가 들고 있는 데이터 들을 하나씩 밀어넣어주는 작업을 해준다.
                bool success = true;
                success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), id);
                count += sizeof(int);
                success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), level);
                count += sizeof(short);
                success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), duration);
                count += sizeof(float);

                return success;
            }
            public void Read(ReadOnlySpan<byte> s, ref ushort count)
            {
                id = BitConverter.ToInt32(s.Slice(count, s.Length - count));
                count += sizeof(int);
                level = BitConverter.ToInt16(s.Slice(count, s.Length - count));
                count += sizeof(short);
                // float 타입은 ToSingle이다(double은 ToDouble).
                duration = BitConverter.ToSingle(s.Slice(count, s.Length - count));
                count += sizeof(float);
            }
        }
        public List<SkillInfo> skills = new List<SkillInfo>();




        // Read는 서버에 있는 클라이언트 세션에서 클라이언트가 보낸 패킷을 받았을 때
        // byte array로 보낸 패킷을 읽어서 패킷 아이디에 따라 패킷의 정보를 읽고(== 역직렬화)
        // 새롭게 생성한 해당 패킷의 객체에 할당하는 과정이다.
        // Read는 받은 패킷을 하나씩 까보면서 값을 할당하고 있기 때문에 
        // 조심해야할 필요가 있다.
        public void Read(ArraySegment<byte> segment)
        {
            // 읽기 시작할 위치를 나타내는 변수
            ushort count = 0;
            // ushort size = BitConverter.ToUInt16(s.Array, s.Offset);
            // segment에서 ReadOnlySpan<byte> s를 생성하여, byte 데이터를 안전하게 읽는다.
            ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
            
            // 먼저, 헤더에 포함된 size 정보를 건너뛰도록 2바이트(ushort 크기)를 count만큼 증가
            // 첫 2바이트는 패킷의 전체 크기를 나타내는데, 여기서는 파싱 로직에서 이미 다뤄졌다고 가정하여 건너뛰고 있습니다.
            count += sizeof(ushort);
            // 이후, 헤더의 두 번째 필드인 packetId도 건너뛰기 위해 2바이트 증가
            // 다음 2바이트는 packetId 정보로, 이 역시 이미 PacketSession에서 처리가 되었거나,
            // 필요하지 않은 경우 건너뛰도록 합니다.
            count += sizeof(ushort);

            // 전달 받은 패킷에 있던 playerId를 자신의 playerId에 할당
            // ReadOnlySpan
            // byte array에서 내가 원하는 범위에 있는 값만 읽어서 역직렬화 한다.
            // s.Array : 대상 byte array
            // s.Offset + count : 시작 위치
            // s.Count - count : 원하는 범위 => 전달 받은 byte array 크기 - 앞에서 빼는 패킷 데이터 크기 
            // => 실제로 읽어야할 byte array 크기
            // byte array의 크기가 실제로 읽어야할 byte array 크기와 다르면 Exception을 뱉어 낸다.
            // 위 Exception을 통해 패킷 조작을 의심할 수 있고 바로 Disconnect 시킬 수 있다.

            // 이제 실제 데이터 중 playerId 값을 읽음  
            // BitConverter.ToInt64()를 사용하여, 현재 count 위치부터 8바이트를 정수(long)로 변환  
            // s.Slice(count, s.Length - count)는 현재 위치부터 남은 전체 바이트 범위를 의미
            // 현재 count 위치에서 8바이트를 long 타입으로 변환하여 playerId에 저장합니다.
            this.playerId = BitConverter.ToInt64(s.Slice(count, s.Length - count));
            // playerId를 읽은 후, 8바이트만큼 count를 증가시켜 다음 데이터의 시작 위치로 이동합니다.
            count += sizeof(long);

            // 문자열 (name) 처리
            // string
            // s.Slice(count, s.Length - count) : byte array에서 string "크기" 값 있는 부분을 집어서 ushort로 변환
            // 다음, 이름의 길이(ushort, 2바이트)를 읽음
            // 현재 count 위치에서 2바이트를 읽어 문자열의 길이를 결정합니다.
            ushort nameLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
            // 문자열 길이를 읽은 후, count를 2바이트 증가시켜 문자열 데이터의 시작 위치로 이동합니다.
            count += sizeof(ushort);

            // GetString : byte에서 string으로 변환을 해주는 과정
            // byte array에서 "실제 string의 값"이 있는 부분을 집어서 string 타입으로 변환
            // 마지막으로, 실제 문자열 데이터를 읽어옴  
            // Encoding.Unicode.GetString()를 사용하여, s의 현재 count 위치에서 nameLen 바이트 만큼 읽어 string으로 변환
            this.name = Encoding.Unicode.GetString(s.Slice(count, nameLen));
            // 지정된 길이만큼의 바이트 데이터를 Unicode 인코딩으로 string으로 변환하여 name에 저장합니다.
            count += nameLen;

            // skill list
            // ToUInt16 : unsigned short
            // ToInt16 : (signed) short
            // 스킬의 갯수를 추출
            ushort skillLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
            count += sizeof(ushort);

            // 혹시 skills에 원치 않는 값이 들어 갔을 경우를 위해 Clear
            skills.Clear();

            for (int i = 0; i < skillLen; i++)
            {
                SkillInfo skill = new SkillInfo();
                // 새로 생성한 skill에 전달 받은 패킷의 정보를 역직렬화
                skill.Read(s, ref count);
                // 해당 SkillInfo를 패킷의 List<SkillInfo>에 추가
                skills.Add(skill);
            }
        }

        // SendBuffer를 통해 보낼 패킷의 정보를 하나의 ArraySegment에 밀어 넣은 다음에 해당 값을 반환
        // write의 경우 패킷에 원하는 값은 넣는 모든 과정을 직접 컨트롤 하고 있기 때문에 별 문제가 없다.
        // 직렬화 과정
        public ArraySegment<byte> Write()
        {
            // 버퍼 예약
            // SendBufferHelper를 호출해,
            // 현재 할당된 SendBuffer에서 4096바이트의 공간을 예약(할당)하고, ArraySegment를 반환합니다.
            ArraySegment<byte> segment = SendBufferHelper.Open(4096);
            ushort count = 0;
            bool success = true;

            // 패킷 크기 공간 확보
            // Span<byte>를 통해 segment 내의 버퍼를 작업 대상으로 만듦.
            // 예약된 버퍼를 Span으로 생성하여, byte 단위 작업(직렬화)을 편리하게 수행합니다.
            Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);
            // 맨 앞 부분에 패킷의 크기를 저장할 공간(2바이트)을 미리 비워 둠 (나중에 전체 크기를 씁니다)
            // 나중에 전체 사용된 count 값을 기록할 것입니다.
            count += sizeof(ushort);

            // BitConverter : byte array를 다른 타입으로 변환
            // TryWriteBytes
            // new Span<byte>(s.Array, s.Offset, s.Count) : Destination, 바이트 데이터를 넣을 공간
            // packet.size : 넣을 바이트 값
            // return : 성공 여부
            // &= : Try가 한번이라도 실패한다면 false
            // 실패하는 경우의 수 : Count가 2(즉 만든 공간이 2바이트)이고 size가 8이라면 에러가 발생
            // => 공간이 없어서
            // GetBytes를 하면 안정성은 올라가지만 속도가 느려진다. 
            // 조금 더 최적화를 할 수 있는 방법으로 TryWriteBytes를 사용

            // Slice => Span을 사용할 때 현재 있는 범위에 일부분을 다시 선택할 때 사용
            // count : 시작 위치
            // s.Length - count : 범위(시작 위치에서부터 해당 범위 만큼을 찝어준다.)
            // 어떻게 동작? : Span으로 만든 바이트 Array에서 Slice한 부분에 packetId를 넣어준다.

            // packetId 직렬화
            // packetId 값을 직렬화하여, 현재 count 위치에 기록
            // 현재 count 위치부터 남은 영역에 packetId를 바이트 배열로 기록.
            success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.PlayerInfoReq);
            // packetId를 기록한 후, count를 2바이트 증가시킵니다.
            count += sizeof(ushort);
            // playerId 값을 직렬화하여 현재 count 위치에 playerId(8바이트)를 기록합니다.
            success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.playerId);
            // playerId를 기록 후, count를 8바이트 증가시킵니다.
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

            /*
            // GetByteCount : 원하는 데이터를 Byte로 바꾼 후 크기를 알려준다.
            ushort nameLen = (ushort)Encoding.Unicode.GetByteCount(this.name);
            success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), nameLen);
            count += sizeof(ushort);

            // 실제 string의 값을 넣어주는 과정
            // Copy
            // Encoding.Unicode.GetBytes(this.name) : 복사할 대상 소스
            // 0 : 복사할 대상 소스의 시작 위치
            // segment.Array : 붙여 넣을 대상 소스
            // count : 붙여 넣을 대상 소스의 위치 
            // nameLen : 붙여 넣을 대상 소스의 크기
            Array.Copy(Encoding.Unicode.GetBytes(this.name), 0, segment.Array, count, nameLen);
            count += nameLen;
            success &= BitConverter.TryWriteBytes(s, count);
            */

            // GetBytes의 다른 버전
            // this.name : 대상 소스
            // 0 : 대상의 시작점
            // this.name.Length : 대상의 길이
            // segment.Array : 소스를 넣을 byte array
            // segment.Offset + count : 소스를 넣을 위치
            // return : string을 byte array로 변한 한 후 크기
            // segment.Offset + count + sizeof(ushort)의 의미 : string의 값을 먼저 byte array에 넣고 있기 때문에 
            // string의 크기를 넣어줄 공간을 먼저 만들어 주고 그 다음에 string의 값을 밀어넣어주기 위해서이다.

            // 문자열(name)을 직렬화  
            // 먼저, Encoding.Unicode.GetBytes()를 사용해, this.name을 바이트 배열로 변환하면서,
            // 실제 버퍼(segment.Array, segment.Offset +count + sizeof(ushort))에 복사하고,
            // 그 길이(바이트 수)를 nameLen으로 측정합니다.
            // 여기서 이름 길이를 저장할 공간도 미리 확보합니다.
            ushort nameLen = (ushort)Encoding.Unicode.GetBytes(this.name, 0, this.name.Length, segment.Array, segment.Offset + count + sizeof(ushort));
            // nameLen을 직렬화하여 nameLen 값을 현재 count 위치에 기록합니다. (문자열의 바이트 길이를 저장)
            success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), nameLen);
            // 이름의 길이를 기록한 후, count를 2바이트 증가시킵니다.
            count += sizeof(ushort);
            // 이름 데이터가 차지하는 바이트 수 만큼 count를 추가로 증가시킵니다.
            count += nameLen;
            // 이렇게 하면 위에서 했던것 보다 더 효율적으로 한방에 처리가 된다

            // 스킬 하나마다 byte array에 밀어 넣어줘야 한다.
            // skill
            // (ushort)skills.Count : 스킬(list)이 들고 있는 갯수의 크기를 byte array에 밀어 넣어준다.
            // Count가 int 타입이기 때문에 2바이트 크기로 변환해 준 다음 밀어 넣어 준다.
            success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)skills.Count);
            count += sizeof(ushort);

            foreach (SkillInfo skill in skills)
            {
                // ref로 count를 늘려주기 때문에 굳이 한번 더 늘려줄 필요가 없다.
                success &= skill.Write(s, ref count);
            }


            // 패킷 전체 크기 기록
            // 마지막으로, 처음에 비워둔 패킷 전체 크기 영역에 실제 사용한 count 값을 기록  
            // 이로써, 패킷의 첫 2바이트에는 전체 패킷의 크기가 기록되게 됩니다.
            success &= BitConverter.TryWriteBytes(s, count);

            // 직렬화 과정이 하나라도 실패하면 null 반환
            if (success == false)
            {
                return null;
            }
            // 사용한 영역의 크기를 최종 확정하고, ArraySegment<byte>로 반환
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
    public enum PacketID
    {
        PlayerInfoReq = 1,
        PlayerInfoOk = 2,
    }

    class ServerSession : Session
    {
        // 연결이 성립되면, ServerSession의 OnConnected() 함수가 호출
        public override void OnConnected(EndPoint endPoint)
        {
            System.Console.WriteLine($"OnConnected : {endPoint}");

            // PlayerInfoReq 패킷 생성
            PlayerInfoReq packet = new PlayerInfoReq() { playerId = 1001, name = "ABCD" };

            packet.skills.Add(new PlayerInfoReq.SkillInfo() { id = 101, level = 1, duration = 3.0f });
            packet.skills.Add(new PlayerInfoReq.SkillInfo() { id = 201, level = 2, duration = 4.0f });
            packet.skills.Add(new PlayerInfoReq.SkillInfo() { id = 301, level = 3, duration = 5.0f });
            packet.skills.Add(new PlayerInfoReq.SkillInfo() { id = 401, level = 4, duration = 6.0f });

            // for (int i = 0; i < 5; i++)
            {
                // 직렬화
                // 이때, 패킷 직렬화 과정은 Packet 클래스의 Write() 메서드와 SendBufferHelper / SendBuffer를 사용합니다.
                // SendBuffer를 통해 보낼 패킷의 정보를 하나의 ArraySegment에 밀어 넣은 다음에 해당 값을 반환
                // Write함수 안에서 얼마만큼의 버퍼를 사용했는지 추적
                // 즉, SendBuffer를 다 사용하고 난 다음에 몇 바이트를 사용했는지를 확인
                ArraySegment<byte> s = packet.Write();
                // Write() 내부에서는 SendBufferHelper를 이용해 큰 버퍼(Chunk)에 예약(Open)된 영역을 가져오고,
                // BitConverter를 사용하여 packet.size와 packet.packetId 등의 값을 해당 영역에 복사합니다.
                // 마지막에 SendBufferHelper.Close()를 호출해
                // 사용 영역(직렬화된 패킷)을 확정한 다음, ArraySegment<byte> 형태로 반환합니다.

                // 직렬화된 패킷 데이터를 인자로 하여 Session.Send()가 호출
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
