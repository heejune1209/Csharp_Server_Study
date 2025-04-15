using System;
using System.Collections.Generic;
using System.Text;
using ServerCore;

namespace Server
{
    // 예약 작업은 JobTimerElem 구조체에 저장
    // 여기에는 실행 시각(execTick)과 실행할 Action이 포함되어 있습니다.
    // 이 구조체는 IComparable<T>를 구현하여,
    // 우선순위(실행 시간이 빠른 순서) 기반으로 정렬되도록 합니다.
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
    // 목적: 예약된 작업(일감, job)을 특정 시점이 되었을 때 실행하는 역할을 합니다.
    // JobTimer 클래스는 내부에 PriorityQueue<JobTimerElem>를 두어 예약된 작업들을 관리합니다.
    class JobTimer
    {
        // 실행 시간이 작은 순서대로 Queue에 쌓이게 된다.
        PriorityQueue<JobTimerElem> _pq = new PriorityQueue<JobTimerElem>();
        object _lock = new object();
        public static JobTimer Instance { get; } = new JobTimer();

        // action : 실행해야하는 job
        // tickAfter : 몇틱 후에 실행이 되어야 하는지 예약 시간
        // default가 0인 이유 : 바로 실행이 되어야 하는 상황에서는 default
        // 현재 시간(Environment.TickCount)에 예약 딜레이(tickAfter)를 더해
        // 실행 시점을 계산하여 JobTimerElem에 저장 후, PriorityQueue에 삽입합니다.
        public void Push(Action action, int tickAfter = 0)
        {
            JobTimerElem job;
            // System.Environment.TickCount : 현재 시간
            // System.Environment.TickCount + tickAfter : 현재 시간 + 예약시간 = 실제로 실행되기를 원하는 시간
            job.execTick = Environment.TickCount + tickAfter;
            job.action = action;

            // PriorityQueue<JobTimerElem> _pq는
            // 공용 데이터이기 때문에 동시 접근을 제어를 위해 lock을 걸어줌
            lock (_lock)
            {
                _pq.Push(job);
            }
        }

        // JobTimer가 들고 있는 PriorityQueue를 비워주는 인터페이스
        // 실행 시간이 되었을 때 자동으로 실행을 시켜준다.
        // 현재 시간과 비교하여 실행 시간이 도래한 작업들을 우선순위 큐에서 꺼내서 실행합니다.
        // 큐에서 하나의 job을 꺼낼 때마다 lock을 걸어 안전하게 작업을 진행합니다.
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
        // JobTimer 사용 예시:
        // ServerProgram에서는 주기적으로 방(Room) 내의 메시지를 전송하는 작업(Flush)이 필요할 때
        // JobTimer에 해당 작업을 예약하고, 반복 호출되도록 합니다.
        // 예를 들어, JobTimer.Instance.Push(FlushRoom, 250);와 같이
        // 일정 간격마다 Flush를 예약하고, Main 루프에서 JobTimer.Instance.Flush(); 를 호출하여
        // 예약된 작업을 실행하도록 합니다.
    }
}