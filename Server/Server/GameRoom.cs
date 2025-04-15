using System;
using System.Collections.Generic;
using System.Text;
using ServerCore;

namespace Server
{
    // GameRoom은 여러 클라이언트 세션(ClientSession)들이 속하는 채팅방이나 게임룸을 나타냄.
    class GameRoom : IJobQueue
    {
        // 채팅 방에는 클라이언트 세션이 옹기종기 모여 있다.
        // Dic로 ID와 Client를 묶어도 괜찮다.
        
        // 클라이언트 세션들을 저장하는 리스트
        List<ClientSession> _sessions = new List<ClientSession>();
        object _lock = new object();
        JobQueue _jobQueue = new JobQueue();
        // 메시지 전송을 위해 사용되는 _pendingList가 있는데,
        // 이는 모아서 한 번에 브로드캐스트할 패킷들을 임시로 저장합니다.
        List<ArraySegment<byte>> _pendingList = new List<ArraySegment<byte>>();

        public void Push(Action job)
        {
            _jobQueue.Push(job);
        }

        // 락을 잡지 않는 이유 JobQueue를 사용하기 때문에
        // _pendingList에 저장된 메시지 패킷들을 모든 세션에 대해
        // Send() 호출로 보내고, 그 후 리스트를 Clear합니다.
        // 이 Flush 작업은 JobQueue를 통해 순차적으로 처리되므로,
        // 별도의 lock 없이 안전하게 실행할 수 있습니다.
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
        // 특정 클라이언트의 메시지를 받아서,
        // 방에 있는 모든 클라이언트에게 전송할 메시지 패킷(S_Chat 등)을 생성합니다.
        // 모든 클라이언트에 즉시 전송하는 대신, 메시지를 바로 보내지 않고
        // _pendingList에 추가하여 나중에 일괄처리(Flush)하도록 설계했습니다. 
        public void Broadcast(ClientSession session, string chat)
        {
            // 방에 있는 모든 클라이언트에게 전송할 메시지 패킷(S_Chat 등)을 생성
            S_Chat packet = new S_Chat();
            packet.playerId = session.SessionId;
            packet.chat = $"{chat} + I am {packet.playerId}";
            
            // 직렬화
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

        // Enter(ClientSession session) & Leave(ClientSession session)
        // 방에 입장/퇴장 시, 클라이언트 세션 리스트에 세션을 추가하거나 제거합니다.
        // 이때, 간단한 lock(_lock)을 사용하거나 JobQueue에 등록하여 순차 처리할 수도 있습니다.

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
// 결국 JobQueue에서 한번에 한명만 작업을 한다는 것
// (순차적으로(single-threaded) 실행하도록 보장)이 보장이 되기 때문에
// GameRoom에서 lock을 사용할 필요가 없어지게 된다.

// 즉, GameRoom은 작업들을 JobQueue에 넣어두고,
// JobQueue가 내부적으로 락을 사용하여 작업들을 순차적으로 처리하므로,
// GameRoom에서는 별도의 락을 사용할 필요가 없게 되는 것입니다.

// 구체적으로 설명하면 
// 1. JobQueue를 통한 작업 단일화:
// - 게임룸(GameRoom) 내부에서는 채팅 메시지 브로드캐스트나 방 입장/퇴장 같은 작업들을
// 바로 실행하지 않고, 대신 Push() 메서드를 통해 작업(예, Broadcast 요청)을 JobQueue에 등록
// - JobQueue는 등록된 작업들을 한 번에 하나씩 순차적으로 실행합니다.
// - 즉, 한 시점에 여러 스레드가 동시에 게임룸의 내부 상태
// (예, 클라이언트 리스트나 전송 대기 리스트)에 접근하는 일이 발생하지 않도록 합니다.

// 2. 동시성 제어:
// - 일반적으로 공유 자원(예: 리스트)에 동시 접근이 일어나면
// 여러 스레드가 동시에 상태를 변경할 위험이 있기 때문에 lock을 사용합니다.
// - 그러나 이 코드에서는 JobQueue 시스템이 "일을 하나씩 처리"하도록 설계되어 있기 때문에,
// 이미 작업 실행 자체가 직렬화(serialized)되어 있습니다.
// - 따라서 별도의 lock 없이도, 순차적 실행 덕분에 충돌이 발생하지 않으므로
// 락을 사용하지 않아도 안전합니다.

// 3. 효율성과 코드 간소화:
// - lock을 매번 사용하면 오버헤드가 발생하고, 코드도 복잡해지므로,
// 단일 스레드에서 작업이 실행될 경우에는 이를 피하는 것이 좋습니다.
// - 게임룸 내 작업들은 JobQueue를 통해 하나씩 처리되므로, 별도의 동기화(lock)를 하지 않아도
// 전체 시스템의 일관성을 유지할 수 있습니다.