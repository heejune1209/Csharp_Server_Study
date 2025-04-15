using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace ServerCore
{
    public interface IJobQueue
    {
        // 일감을 밀어넣어줌
        // 여러 작업(일감, task)을 큐에 저장해서 순차적으로 실행할 수 있도록 하는 인터페이스
        // JobQueue 클래스가 이 인터페이스를 구현하며, Push 메서드를 통해 작업을 추가
        void Push(Action job);
    }

    // "Job Queue"는 작업(task)이나 작업 항목(job)을 저장하고,
    // 순차적 또는 우선순위 기반으로 처리하며,
    // 비동기 및 분산 처리 환경에서 작업 부하를 효율적으로 관리하는데 사용되는 핵심 메커니즘

    // 즉, 몬스터, 스킬, 유저 등 모든 객체들에게 일감을 순차적으로 진행할 수 있는
    // JobQueue를 할당해서 진행을 하면 어느정도 편하게 심리스 게임을 만들 수 있다.

    // 목적: 여러 작업(일감, task)을 순차적으로 실행하도록 큐잉하는 역할입니다.
    public class JobQueue : IJobQueue
    {
        Queue<Action> _jobQueue = new Queue<Action>();
        object _lock = new object();

        // flush : 물을 내려 주다.
        // 현재 JobQueue에서 실행 중인(플러시 중인) 작업이 있는지 여부를 나타냅니다.

        // Push()에서 _flush가 false인 경우:
        // 작업을 Push할 때, 아직 작업 처리(Flush)가 진행 중이지 않으면, 현재 Push를 호출한 쓰레드가 직접 Flush()를 시작합니다.
        // Push() 내에서, 작업을 큐에 추가한 후 만약 _flush가 false라면
        // 해당 쓰레드는 _flush를 true로 설정하고 flush = true 로 만들어 Flush() 호출하게 됩니다.
        // 이 쓰레드가 Flush() 를 실행하면서 큐에 들어있는 모든 작업들을 하나씩 꺼내어 실행합니다.
        // 그리고 Flush를 실행하면서 Pop()내에서 만약 큐가 비어있다면_flush를 false로 설정해준다

        // _flush가 true인 경우:
        // 이미 다른 쓰레드가 Flush()를 진행 중인 상태라면,
        // Push()를 호출한 쓰레드는 단순히 큐에 작업을 추가한 뒤 즉시 종료합니다.
        // 왜냐하면 현재 Flush()를 수행 중인 쓰레드가 곧바로 큐를 확인해서 새로 추가된 작업들도 처리하게 되기 때문입니다.
        // 즉, 작업 추가 후 별도로 Flush()를 호출할 필요가 없으므로,
        // 현재 진행 중인 flush 쓰레드가 큐를 계속 모니터링하여 모든 작업이 처리될 때까지 실행합니다.

        // Pop()에서 큐가 비어있을 때 _flush를 false로 변경하여
        // 다음 Push 시 플러시를 다시 시작할 수 있도록 합니다.
        bool _flush = false;
        
        // 외부에서 작업(일감)을 큐에 추가합니다.
        // 만약 현재 큐가 비어있었거나 실행 중인 플러시 작업이 없으면(Push 시 _flush가 false인 경우)
        // 현재 작업을 실행하도록 Flush()를 호출합니다.
        public void Push(Action job)
        {
            bool flush = false;

            // _flush가 false라면 내가 얘를 이제 실행해야한다는 것을 
            // flush가 true인 것을 통해 알 수 있음
            // push를 할 때 경우에 따라서 맨 처음으로 JobQueue에 일감을 밀어넣었다면 (_flush == false)
            // 실제로 실행까지 담당한다.
            // 그리고 flush를 진행하고 있는 쓰레드가 있다면(flush == true) 일감만 등록하고 빠져나온다.
            // 그리고 JobQueue에 있는 모든 일감을 처리했다면 다음에 Push 하는 쓰레드가 일감을 처리할 수 있도록
            // 다시금 오픈해준다.(_flush == false)

            // 동작 과정:
            // lock 블록 내에서 작업을 큐에 추가합니다.
            // _flush 플래그가 false이면 — 즉, 현재 실행 중인 작업이 없는 상태 —
            // flush 로컬 변수를 true로 설정하고, _flush도 true로 변경합니다.
            // lock 블록을 빠져나온 후 flush 값이 true이면 Flush() 메서드를 호출하여 큐에 저장된 작업들을 실행합니다.
            lock (_lock)
            {
                _jobQueue.Enqueue(job);

                if (_flush == false)
                {
                    flush = _flush = true;
                }
            }

            if (flush)
            {
                Flush();
            }
        }

        // Flush는 lock을 잡지 않고(한명의 쓰레드만 작업을 하고) Pop만 lock을 잡는 이유
        // 전체 Flush() 메서드에 lock을 걸면, Flush()가 오래 실행되는 동안 lock이 해제되지 않으므로
        // 다른 스레드들이 Push()를 하려고 할 때 계속 기다려야 하게 됩니다. 그래서 시스템의 동시성이 떨어지고, 성능 저하로 이어질 수 있습니다. 
        // 대신에, 해당 코드에서는 Push()와 Pop() 메서드 각각에서만 lock을 사용함으로써,
        // Push()가 실행될 때 잠깐만 lock을 잡고 작업을 추가하고 즉시 lock을 해제합니다.
        // Flush()는 반복문 전체에 걸쳐 지속적으로 lock을 잡지 않고, 각 작업을 꺼내는(Pop()) 순간마다만 단기적인 lock만을 사용합니다.
        // 이렇게 하면 Flush()가 실행되는 동안에도 다른 스레드는 짧은 시간 동안만 lock을 획득해서 작업을 추가할 수 있게 됩니다.
        
        // 하나의 쓰레드가 Pop을 하는 동안 다른 쓰레드가 push를 하게 될 때 
        // 즉, JobQueue를 동기화 시켜주기 위함이다.
        // 그리고 Flush는 실질적으로 한명의 쓰레드가 담당하게 된다.
        // Flush는 큐에 저장된 모든 작업을 하나씩 꺼내서 실행하는 메서드입니다.

        // 동작 과정:
        // 1. while 루프에서 Pop()을 호출하여 큐에서 작업을 하나 꺼냅니다.
        // 2. 꺼낸 작업이 null이면(즉, 큐가 비어있다면) Flush를 종료하고 반환합니다.
        // 3. null이 아니라면, action.Invoke() 를 통해 실제 작업을 실행합니다.
        void Flush()
        {
            while (true)
            {
                // 저장된 일감을 모두 가지고 와서 실행
                Action action = Pop();
                if (action == null)
                {
                    return;
                }
                action.Invoke();
            }
        }

        // _jobQueue에서 한 작업을 꺼내 반환합니다.
        // 동작 과정
        // 1. lock 블록 내에서 큐의 갯수를 체크합니다.
        // 2. 만약 큐가 비어있다면, _flush 플래그를 false로 변경하고 null을 반환합니다.
        // → 이렇게 하면 Flush() 메서드가 종료되고, 다음 Push() 호출 시 새로운 Flush 실행 여부가 결정됩니다.
        // 3. 큐가 비어있지 않으면 _jobQueue.Dequeue()를 통해 첫 번째 작업을 꺼내 반환합니다.
        Action Pop()
        {
            lock (_lock)
            {
                if (_jobQueue.Count == 0)
                { 
                    // 다른 쓰레드가 진입할 수 있도록 오픈
                    _flush = false;
                    return null;
                }
                return _jobQueue.Dequeue();
            }
        }
    }
    // 사용 예시 및 전체 흐름
    // 사용 예:
    // - 예를 들어, GameRoom 같은 객체에서 여러 클라이언트에 메시지를 전송하는 작업을 하나의 작업으로 만들고,
    // 이를 JobQueue에 Push()로 추가할 수 있습니다.
    // - JobTimer 같은 예약 작업도 내부적으로 JobQueue를 사용하여 지정된 시간이 되면 작업을 실행합니다.

    // 전체 흐름 요약:
    // 1. 외부 코드(예: GameRoom, JobTimer 등)에서 JobQueue.Push(job)을 호출하여 작업(일감)을 추가합니다.
    // 2. Push() 메서드 내부에서 락을 통해 _jobQueue에 작업이 추가되고, _flush 상태에 따라 Flush() 호출 여부가 결정됩니다.
    
    // 2-1. 첫 번째 작업 Push() 호출 (Flush가 false인 경우):
    // 쓰레드 A가 Push()를 호출하면, 큐에 작업을 추가하고 _flush가 false이므로
    // → _flush를 true로 설정하고, Flush()를 호출합니다.
    // 쓰레드 A는 Flush() 루프 안에서 큐에서 작업을 하나씩 꺼내서 실행합니다.
    
    // 2-2 다른 쓰레드에서 새 작업 Push() 호출 (Flush가 true인 경우):
    // 쓰레드 B가 Push()를 호출하면, 큐에 작업을 추가하지만 이미 _flush가 true이므로,
    // Flush()를 호출하지 않습니다.
    // 그러면 쓰레드 A가 Flush() 루프를 계속 돌아가며 큐에 있는 작업을 처리하고, B가 추가한 작업도 처리합니다.
    
    // 3. Flush() 가 호출되면 while 루프를 통해 Pop()하여 큐에 저장된 작업들을 하나씩 실행합니다.
    
    // 4. Pop() 내에서 락을 사용하여 작업을 안전하게 제거하고, 큐가 비어 있으면 _flush 플래그를 false로 설정합니다.
}