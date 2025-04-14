using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using Server;
using ServerCore;

// 수동으로 관리 => 특정 패킷 타입이 도착했을 때 호출할 함수를 정의하는 역할을 함
class PacketHandler
{
    // PlayerInfoReqHandler는 패킷 처리 메서드 중 하나로,
    // 플레이어 정보 요청(Packet ID가 PlayerInfoReq인 패킷)이 도착했을 때 호출됩니다.
    // PacketSession session: 패킷을 수신한 세션 객체(네트워크 연결의 대리자 역할)를 받습니다.
    // IPacket packet: 수신한 패킷을 추상 인터페이스(IPacket) 형태로 받는데,
    // 여기서 실제로는 PlayerInfoReq 형식이어야 합니다.
    public static void C_ChatHandler(PacketSession session, IPacket packet)
    {
        C_Chat chatPacket = packet as C_Chat;
        ClientSession clientSession = session as ClientSession;
        if (clientSession.Room == null)
            return;

        // Room에 접속한 모든 클라이언트 세션에게 메시지를 보냄
        clientSession.Room.Broadcast(clientSession, chatPacket.chat);

    }


}