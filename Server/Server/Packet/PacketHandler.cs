using System;
using System.Collections.Generic;
using System.Text;
using Server;
using ServerCore;

class PacketHandler
{
    // 클라이언트 쪽에서 나가고 싶다는 패킷을 명시적으로 보냈을 때
    // 알아서 나갈 수 있도록 해준다.
    public static void C_LeaveGameHandler(PacketSession session, IPacket packet)
    {
        ClientSession clientSession = session as ClientSession;
        if (clientSession.Room == null)
            return;

        GameRoom room = clientSession.Room;
        room.Push(() => room.Leave(clientSession));
    }

    public static void C_MoveHandler(PacketSession session, IPacket packet)
    {
        C_Move movePacket = packet as C_Move;
        ClientSession clientSession = session as ClientSession;

        if (clientSession.Room == null)
            return;
        // 클라로부터 수신한 패킷의 정보 출력
        //Console.WriteLine($"{movePacket.posX}, {movePacket.posY}, {movePacket.posZ}");
        GameRoom room = clientSession.Room;
        room.Push(() => room.Move(clientSession, movePacket));
    }
}