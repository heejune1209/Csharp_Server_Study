using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using ServerCore;

namespace Server
{
    // 수동으로 관리 => 특정 패킷 타입이 도착했을 때 호출할 함수를 정의하는 역할을 함
    class PacketHandler
    {
        // PlayerInfoReqHandler는 패킷 처리 메서드 중 하나로,
        // 플레이어 정보 요청(Packet ID가 PlayerInfoReq인 패킷)이 도착했을 때 호출됩니다.
        // PacketSession session: 패킷을 수신한 세션 객체(네트워크 연결의 대리자 역할)를 받습니다.
        // IPacket packet: 수신한 패킷을 추상 인터페이스(IPacket) 형태로 받는데,
        // 여기서 실제로는 PlayerInfoReq 형식이어야 합니다.
        public static void PlayerInfoReqHandler(PacketSession session, IPacket packet)
        {
            // 받는 패킷을 PlayerInfoReq 형식으로 캐스팅 
            PlayerInfoReq p = packet as PlayerInfoReq;

            // 플레이어 ID와 이름을 출력하여, 패킷에 담긴 데이터를 확인합니다.
            System.Console.WriteLine($"PlayerInfoReq : {p.playerId}, {p.name}");

            // 리스트 처리
            // PlayerInfoReq에 포함된 스킬 정보가 있다면,
            // 각 스킬의 id, level, duration, attributes 값을 반복문을 통해 출력합니다.
            foreach (PlayerInfoReq.Skill skill in p.skills)
            {
                System.Console.WriteLine($"Skill({skill.id})({skill.level})({skill.duration})({skill.attributes})");
            }
        }
    }
}