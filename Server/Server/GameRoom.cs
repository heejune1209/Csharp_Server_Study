using System;
using System.Collections.Generic;
using System.Text;
using ServerCore;

namespace Server
{
    class GameRoom : IJobQueue
    {
        // 채팅 방에는 클라이언트 세션이 옹기종기 모여 있다.
        // Dic로 ID와 Client를 묶어도 괜찮다.
        List<ClientSession> _sessions = new List<ClientSession>();
        object _lock = new object();
        JobQueue _jobQueue = new JobQueue();
        List<ArraySegment<byte>> _pendingList = new List<ArraySegment<byte>>();

        public void Push(Action job)
        {
            _jobQueue.Push(job);
        }
        // 락을 잡지 않는 이유 JobQueue를 사용하기 때문에
        public void Flush()
        {
            // N ^ 2
            foreach (ClientSession s in _sessions)
            {
                s.Send(_pendingList);
            }

            Console.WriteLine($"Flushed {_pendingList.Count} items");
            _pendingList.Clear();
        }
        
        // Broadcast는 모든 클라이언트 세션에게 메시지를 전송하는 메서드입니다.
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

            // N ^ 2
            // 사용자가 늘어날수록 여기있는 부분이 부담이 커짐
            //foreach (ClientSession s in _sessions)
            //{
            //    s.Send(segment);
            //}
            
            // 패킷을 바로 보내지 않고 일단 저장을 해놓는다.
            _pendingList.Add(segment);
        }

        // 방에 들어가는 메서드입니다.  
        public void Enter(ClientSession session)
        {
            // 한번에 하나의 쓰레드만 통과되도록 lock을 걸어준다.
            // 즉 하나의 쓰레드가 Enter를 하고 있는 상황이라면 잠시 기다려주는 작업을 해주는 것이다.            
            _sessions.Add(session);
            // 현재 룸이 자기 자신임을 전달
            session.Room = this;
        }
        // 방에서 나가는 메서드입니다.
        public void Leave(ClientSession session)
        {
            _sessions.Remove(session);
        }
    }
}

// 중요한 사실
// JobQueue를 사용하기 전에는 GameRoom에서 Broadcast, Enter, Leave 할 때
// 모두 lock을 걸어주고 있었지만 JobQueue를 사용한다는 것은
// 결국 JobQueue에서 한번에 한명만 작업을 한다는 것이 보장이 되기 때문에
// GameRoom에서 lock을 사용할 필요가 없어지게 된다.