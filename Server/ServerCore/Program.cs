using System;
using System.Threading.Tasks;
using System.Threading;

namespace ServerCore
{
    class SpinLock
    {
        volatile int _locked = 0; // 1은 true, 0은 false
        public void Acquire()
        {
            while (true)
            {
                // Exchange은 _locked가 바뀌기 전의 값을 리턴해줌
                int original = Interlocked.Exchange(ref _locked, 1); // exchange를 통해 _locked에다가 1을 넣고 리턴 값으로 1을 넣기전의 값을 리턴해줌
                //  _locked가 바뀌기 전의 값이 0이라면
                // lock이 풀린 상태이기 때문에 다른 쓰레드가 접근할 수 있도록 대기함
                // 즉, 원래는 0이였는 데 1로 바뀐 상황을 정확히 추론할 수 있도록 해줌
                if (original == 0)
                    break;
                /*
                // 위 코드를 쉽게 표현하면
                {
                    int original = _locked;
                    _locked = 1;
                    // 위 두줄이 한번에 실행되도록 한게 Exchange이다.
                    if (original == 0)
                        break;
                }
                */
                // 하지만 아래와 같이 쓰는 게 좀더 안정적이다.
                // 왜냐하면 직관적으로 이해를 해보았을 때 
                // lock이 풀린 시점(lock == 0)에서 lock을 차지해줘야 하는 데
                // 위와 같은 방법은 기존 lock의 값을 
                // 그래서 CompareExchange()를 사용해야 한다.
                {
                    if (_locked == 0)
                        _locked = 1;
                }
            }
        }
        // 멀티스레드 환경에서 작업을 하다 보면 
        // 그린 존과 레드 존을 바라보는 매의 눈이 필요하다.
        // 즉, 위의 경우에서 _locked는 여러 쓰레드에서 공유하는 값이기 때문에 조심히 다뤄야하고
        // original의 경우는 stack에 있기 때문에 값을 바꿀 수도 있게 된다.

        public void Release()
        {
            _locked = 0;
        }
    }

    class Program
    {
        static int num = 0;
        static SpinLock _lock = new SpinLock();

        static void Thread_1()
        {
            for (int i = 0; i < 1000; i++)
            {
                _lock.Acquire();
                num++;
                _lock.Release();
            }
        }

        static void Thread_2()
        {
            for (int i = 0; i < 1000; i++)
            {
                _lock.Acquire();
                num--;
                _lock.Release();
            }
        }

        static void Main(string[] args)
        {
            Task t1 = new Task(Thread_1);
            Task t2 = new Task(Thread_2);

            t1.Start();
            t2.Start();

            Task.WaitAll(t1, t2);

            Console.WriteLine(num);
        }
    }
}