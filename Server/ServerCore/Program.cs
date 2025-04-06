using System.Threading.Tasks;
using System.Threading;

namespace ServerCore
{
    class Program
    {
        // lock free programming 기법과 비슷
        static volatile int count = 0; // volatile: _flag의 캐싱 최적화 방지 (항상 최신 값을 보게 함)
        static Lock _lock = new Lock();
        static void Main(string[] args)
        {
            Task t1 = new Task(delegate ()
            {
                for (int i = 0; i < 10000; i++)
                {
                    _lock.WriteLock();
                    _lock.WriteLock();
                    count++;
                    _lock.WriteUnlock();
                    _lock.WriteUnlock();
                    // WriteLock()을 2번 연속 호출한 이유는 → 재귀 락이 안되도록 일부러 실험
                    // → 한 번 더 락을 잡으면 내부적으로 재귀적으로 다시 락 시도하는지 보기 위해

                    // 여기서 writelock끼리 짝을 안맞춰주면 리턴을 안한다. 
                }
            }); // 델레게이트를 사용해서 익명함수 선언
            Task t2 = new Task(delegate ()
            {
                for (int i = 0; i < 10000; i++)
                {
                    _lock.WriteLock();
                    count--;
                    _lock.WriteUnlock();
                    // 만약에 여기랑 위에서 writelock이 아니라 readlock을 하면 리드락은 상호 베타적인 락이 아니기 때문에 이상한 값이 나온다.
                }
            });
            t1.Start();
            t2.Start();

            Task.WaitAll(t1, t2);
            System.Console.WriteLine(count);
        }
    }
}