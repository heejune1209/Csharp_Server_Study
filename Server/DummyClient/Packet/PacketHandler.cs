using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using DummyClient;
using ServerCore;

// 수동으로 관리 => 특정 패킷 타입이 도착했을 때 호출할 함수를 정의하는 역할을 함
class PacketHandler
{
    // PlayerInfoReqHandler는 패킷 처리 메서드 중 하나로,
    // 플레이어 정보 요청(Packet ID가 PlayerInfoReq인 패킷)이 도착했을 때 호출됩니다.
    // PacketSession session: 패킷을 수신한 세션 객체(네트워크 연결의 대리자 역할)를 받습니다.
    // IPacket packet: 수신한 패킷을 추상 인터페이스(IPacket) 형태로 받는데,
    // 여기서 실제로는 PlayerInfoReq 형식이어야 합니다.
    

    public static void S_ChatHandler(PacketSession session, IPacket packet)
    {
        S_Chat chatPacket = packet as S_Chat;
        ServerSession serverSession = session as ServerSession;

        // 더미 클라이언트가 10개 => 모든 메시지를 다 출력하지 않고 
        // playerId가 1일 경우에만 콘솔 로그에 출력
        // if (chatPacket.playerId == 1)
        {
            //Console.WriteLine(chatPacket.chat);
        }
    }
}