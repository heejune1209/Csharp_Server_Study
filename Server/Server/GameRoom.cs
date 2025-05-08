using System;
using System.Collections.Generic;
using System.Text;
using ServerCore;

namespace Server
{
    class GameRoom : IJobQueue
    {
        List<ClientSession> _sessions = new List<ClientSession>();
        JobQueue _jobQueue = new JobQueue();
        List<ArraySegment<byte>> _pendingList = new List<ArraySegment<byte>>();
        public void Push(Action job)
        {
            _jobQueue.Push(job);
        }

        public void Flush()
        {
            foreach (ClientSession s in _sessions)
            {
                s.Send(_pendingList);
            }

            // System.Console.WriteLine($"Flushed {_pendingList.Count} items");
            _pendingList.Clear();
        }

        // Broadcast를 해야하는 다양한 패킷에서 사용할 인터페이스이기 때문에
        // ArraySegment만 받도록 해서 좀더 범용적으로 사용할 수 있도록 한다.
        public void Broadcast(ArraySegment<byte> segment)
        {
            _pendingList.Add(segment);
        }

        public void Enter(ClientSession session)
        {
            // 플레이어 추가
            _sessions.Add(session);
            session.Room = this;

            // 새로 들어온 클라한테 모든 플레이어 목록 전송
            // 모든 플레이어 목록을 전달하는 패킷
            S_PlayerList players = new S_PlayerList();
            foreach (ClientSession s in _sessions)
            {
                players.players.Add(new S_PlayerList.Player()
                {
                    isSelf = (s == session),
                    playerId = s.SessionId,
                    posX = s.PosX,
                    posY = s.PosY,
                    posZ = s.PosZ,
                });
            }
            session.Send(players.Write());

            // 신입생 입장을 모두에게 알린다.
            S_BroadcastEnterGame enter = new S_BroadcastEnterGame();
            // 신입생 정보
            enter.playerId = session.SessionId;
            // 신입생의 처음 위치
            enter.posX = 0;
            enter.posY = 0;
            enter.posZ = 0;

            Broadcast(enter.Write());
        }
        public void Leave(ClientSession session)
        {
            // 플레이어 제거
            _sessions.Remove(session);

            // 모두에게 알린다.
            S_BroadcastLeaveGame leave = new S_BroadcastLeaveGame();
            leave.playerId = session.SessionId;
            Broadcast(leave.Write());
        }

        // 내가 이동하는 패킷이 왔다고 가정
        public void Move(ClientSession session, C_Move packet)
        {
            // 좌표 바꿔주고
            session.PosX = packet.posX;
            session.PosY = packet.posY;
            session.PosZ = packet.posZ;

            // 모두에게 알린다 
            S_BroadcastMove move = new S_BroadcastMove();
            move.playerId = session.SessionId;
            move.posX = session.PosX;
            move.posY = session.PosY;
            move.posZ = session.PosZ;

            Broadcast(move.Write());
        }
    }
}