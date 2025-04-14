using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ServerCore
{
    public interface IJobQueue
    {
        // 일감을 밀어넣어줌
        void Push(Action job);
    }

    // "Job Queue"는 작업(task)이나 작업 항목(job)을 저장하고,
    // 순차적 또는 우선순위 기반으로 처리하며,
    // 비동기 및 분산 처리 환경에서 작업 부하를 효율적으로 관리하는데 사용되는 핵심 메커니즘

    // 즉, 몬스터, 스킬, 유저 등 모든 객체들에게 일감을 순차적으로 진행할 수 있는
    // JobQueue를 할당해서 진행을 하면 어느정도 편하게 심리스 게임을 만들 수 있다.

    public class JobQueue : IJobQueue
    {
        Queue<Action> _jobQueue = new Queue<Action>();
        object _lock = new object();

        // flush : 물을 내려 주다.
        bool _flush = false;

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
        // 하나의 쓰레드가 Pop을 하는 동안 다른 쓰레드가 push를 하게 될 때 
        // 즉, JobQueue를 동기화 시켜주기 위함이다.
        // 그리고 Flush는 실질적으로 한명의 쓰레드가 담당하게 된다.
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

        Action Pop()
        {
            lock (_lock)
            {
                if (_jobQueue.Count == 0)
                {
                    // 
                    // 다른 쓰레드가 진입할 수 있도록 오픈
                    _flush = false;
                    return null;
                }
                return _jobQueue.Dequeue();
            }
        }
    }
}