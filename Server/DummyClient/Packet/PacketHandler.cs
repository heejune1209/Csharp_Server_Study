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

    // 더미 클라이언트에서는 꼼꼼하게 관리를 하는 역할을 하는게 아니라
    // 얘는 말 그대로 그냥 이동하는 패킷을 여기서 시뮬레이션 해가지고 보내기만 할 거니까 얘는 그냥 빌드만 
    // 통과할 수 있도록 함수만 만들어주고 별다른 작업은 하지 않을 겁니다
    public static void S_BroadcastEnterGameHandler(PacketSession session, IPacket packet)
    {
        S_BroadcastEnterGame pkt = packet as S_BroadcastEnterGame;

    }
    public static void S_BroadcastLeaveGameHandler(PacketSession session, IPacket packet)
    {
        S_BroadcastEnterGame pkt = packet as S_BroadcastEnterGame;
        ServerSession serverSession = session as ServerSession;

    }

    public static void S_PlayerListHandler(PacketSession session, IPacket packet)
    {
        S_BroadcastEnterGame pkt = packet as S_BroadcastEnterGame;
        ServerSession serverSession = session as ServerSession;

    }
    public static void S_BroadcastMoveHandler(PacketSession session, IPacket packet)
    {
        S_BroadcastEnterGame pkt = packet as S_BroadcastEnterGame;
        ServerSession serverSession = session as ServerSession;

    }
}