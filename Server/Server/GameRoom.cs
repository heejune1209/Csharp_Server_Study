using System;
using System.Collections.Generic;
using System.Text;

namespace Server
{
    class GameRoom
    {
        // 채팅 방에는 클라이언트 세션이 옹기종기 모여 있다.
        // Dic로 ID와 Client를 묶어도 괜찮다.
        List<ClientSession> _sessions = new List<ClientSession>();
        object _lock = new object();
        public void Broadcast(ClientSession session, string chat)
        {
            S_Chat packet = new S_Chat();
            packet.playerId = session.SessionId;
            packet.chat = $"{chat} + I am {packet.playerId}";
            ArraySegment<byte> segment = packet.Write();

            // 위에 부분은 생성하고 할당하는 과정이기 때문에 굳이 임계구역을 만들지 않아도 괜찮았지만
            // 여기서부터는 공유하는 자원 즉 List<ClientSession> _session에 접근을 해야하기 때문에 
            // lock을 걸어줘야 한다.
            // lock을 잡을 때 핵심은 동시에 여러 쓰레드가 공유 자원에 접근을 하는가이다.
            lock (_lock)
            {
                foreach (ClientSession s in _sessions)
                {
                    s.Send(segment);
                }
            }
        }
        public void Enter(ClientSession session)
        {
            // 한번에 하나의 쓰레드만 통과되도록 lock을 걸어준다.
            // 즉 하나의 쓰레드가 Enter를 하고 있는 상황이라면 잠시 기다려주는 작업을 해주는 것이다.            
            lock (_lock)
            {
                _sessions.Add(session);
                // 현재 룸이 자기 자신임을 전달
                session.Room = this;
            }
        }
        public void Leave(ClientSession session)
        {
            lock (_lock)
            {
                _sessions.Remove(session);
            }
        }
    }
}