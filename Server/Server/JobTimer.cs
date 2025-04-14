using System;
using System.Collections.Generic;
using System.Text;
using ServerCore;

namespace Server
{
    // 하나의 일감 단위
    struct JobTimerElem : IComparable<JobTimerElem>
    {
        public int execTick; // 실행시간
        public Action action;

        // 실행 시간을
        public int CompareTo(JobTimerElem other)
        {
            // execTick이 작은 순서대로 튀어나와야 한다.
            // 비교를 해야하는 순서가 어떻게 되는지?
            // 순서가 헷갈릴 수도 있는데 추천은 일단 해보고 에러가 뜨면 수정하는 것도 좋은 방법
            return other.execTick - execTick;
        }
    }

    // job 예약 시스템
    class JobTimer
    {
        // 실행 시간이 작은 순서대로 Queue에 쌓이게 된다.
        PriorityQueue<JobTimerElem> _pq = new PriorityQueue<JobTimerElem>();
        object _lock = new object();
        public static JobTimer Instance { get; } = new JobTimer();

        // action : 실행해야하는 job
        // tickAfter : 몇틱 후에 실행이 되어야 하는지 예약 시간
        // default가 0인 이유 : 바로 실행이 되어야 하는 상황에서는 default
        public void Push(Action action, int tickAfter = 0)
        {
            JobTimerElem job;
            // System.Environment.TickCount : 현재 시간
            // System.Environment.TickCount + tickAfter : 현재 시간 + 예약시간 = 실제로 실행되기를 원하는 시간
            job.execTick = Environment.TickCount + tickAfter;
            job.action = action;

            // PriorityQueue<JobTimerElem> _pq는 공용 데이터이기 때문에 lock을 걸어줌
            lock (_lock)
            {
                _pq.Push(job);
            }
        }

        // JobTimer가 들고 있는 PriorityQueue를 비워주는 인터페이스
        // 실행 시간이 되었을 때 자동으로 실행을 시켜준다.
        public void Flush()
        {
            while (true)
            {
                int now = Environment.TickCount;
                JobTimerElem job;
                lock (_lock)
                {
                    if (_pq.Count == 0)
                        break; // lock을 나가는 의미가 아니라 while문을 나간다는 의미
                    job = _pq.Peek();

                    // 현재 job을 실행하는 시간이 현재 시간보다 클 때 => 아직 실행 시간이 아닐 때
                    if (job.execTick > now)
                        break;

                    // 여기까지 오면 일단 실제로 job을 실행시켜야 한다.
                    // 여기서 job에 굳이 다시 넣어줄 필요가 없는게 
                    // 위에서 Peek()을 했을 때 이미 들어갔다.
                    _pq.Pop();
                }
                job.action.Invoke();
            }
        }
    }
}