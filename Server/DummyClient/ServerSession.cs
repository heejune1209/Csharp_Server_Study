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
    


    class ServerSession : PacketSession
    {
        // 연결이 성립되면, ServerSession의 OnConnected() 함수가 호출
        public override void OnConnected(EndPoint endPoint)
        {
            Console.WriteLine($"OnConnected : {endPoint}");

            // PlayerInfoReq 패킷 생성
            //C_PlayerInfoReq packet = new C_PlayerInfoReq() { playerId = 1001, name = "ABCD" };
            //var skill = new C_PlayerInfoReq.Skill() { id = 101, level = 1, duration = 3.0f };
            //skill.attributes.Add(new C_PlayerInfoReq.Skill.Attribute() { att = 77 });
            //packet.skills.Add(skill);
            //packet.skills.Add(new C_PlayerInfoReq.Skill() { id = 201, level = 2, duration = 4.0f });
            //packet.skills.Add(new C_PlayerInfoReq.Skill() { id = 301, level = 3, duration = 5.0f });
            //packet.skills.Add(new C_PlayerInfoReq.Skill() { id = 401, level = 4, duration = 6.0f });

            // for (int i = 0; i < 5; i++)
            {
                // 직렬화
                // 이때, 패킷 직렬화 과정은 Packet 클래스의 Write() 메서드와 SendBufferHelper / SendBuffer를 사용합니다.
                // SendBuffer를 통해 보낼 패킷의 정보를 하나의 ArraySegment에 밀어 넣은 다음에 해당 값을 반환
                // Write함수 안에서 얼마만큼의 버퍼를 사용했는지 추적
                // 즉, SendBuffer를 다 사용하고 난 다음에 몇 바이트를 사용했는지를 확인
                //ArraySegment<byte> s = packet.Write();
                // Write() 내부에서는 SendBufferHelper를 이용해 큰 버퍼(Chunk)에 예약(Open)된 영역을 가져오고,
                // BitConverter를 사용하여 packet.size와 packet.packetId 등의 값을 해당 영역에 복사합니다.
                // 마지막에 SendBufferHelper.Close()를 호출해
                // 사용 영역(직렬화된 패킷)을 확정한 다음, ArraySegment<byte> 형태로 반환합니다.

                // 직렬화된 패킷 데이터를 인자로 하여 Session.Send()가 호출
                //if (s != null)
                //    Send(s);
            }
        }
        // 이렇게 PlayerInfoReq packet을 만들어서 버퍼에다가 밀어넣는 작업을 직렬화라고 한다.
        // 그리고 직렬화를 해서 Send를 한 다음에
        // 반대쪽에 서버에서도 역직렬화를 해가지고 버퍼에 있는 거를 꺼내서 쓰는 작업을 해봤다.

        // 이번엔 직렬화 하는 과정을 자동화하는데 있어서, 다른 인터페이스로 만들어보았다


        public override void OnDisconnected(EndPoint endPoint)
        {
            Console.WriteLine($"OnDisconnected : {endPoint}");
        }

        public override void OnRecvPacket(ArraySegment<byte> buffer)
        {
            //string recvData = Encoding.UTF8.GetString(buffer.Array, buffer.Offset, buffer.Count);
            //System.Console.WriteLine($"From Server : {recvData}");
            //// 일반적인 경우라면 모든 데이터를 처리 했기 때문에 전체 갯수를 처리했다고 알림
            //return buffer.Count;

            PacketManager.Instance.OnRecvPacket(this, buffer);
        }

        public override void OnSend(int numOfBytes)
        {
            // Console.WriteLine($"Transferred bytes : {numOfBytes}");
        }
    }
}
