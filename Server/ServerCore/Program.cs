using System.Threading.Tasks;
using System.Threading;

namespace ServerCore
{
    class Program
    {
        // 그냥 전역 변수로 쓰레드 이름을 선언하면 
        // 모든 쓰레드에서 접근이 가능하고 변경 시 다른 쓰레드에도 영향을 주게 된다.
        // 따라서 TLS 영역으로 보관
        // 아래와 같이 래핑을 해서 사용을 하면 된다.
        // 쓰레드 마다 TLS에 접근을 하면 자신만의 공간에 저장이 되기 때문에 
        // 특정 쓰레드에서 쓰레드 이름을 고친다고 해도 다른 쓰레드에는 영향을 주지 않게 된다.
        // 즉, 쓰레드 마다 고유의 영역이 생겼다고 생각하면 된다.
        static ThreadLocal<string> ThreadName = new ThreadLocal<string>(() =>
        {
            // 쓰레드가 새로 실행될 때마다 100프로 확률로 TLS를 생성하는 것이 아니라
            // 상황에 따라 쓰레드 네임의 밸류가 없을 때 생성?
            return $"My Name is {Thread.CurrentThread.ManagedThreadId}";
        });

        static void WhoAmI()
        {
            bool isRepeat = ThreadName.IsValueCreated;
            if (isRepeat)
                Console.WriteLine(ThreadName.Value + " (repeat)");
            else
                Console.WriteLine(ThreadName.Value);
            // repeat으로 출력이 되는 의미는
            // 이미 생성된 쓰레드에서 해당 일감(여기서는 WhoAmI 메서드)을 또 다시 처리한다는 의미
            // 그래서 재사용을 한다고 생각하면 됨
            // 좀더 세부적으로 
            // ThreadName.Value이 null일 때 Action이 콜백되면서 ThreadName.Value 값이 할당됨
        }
        static void Main(string[] args)
        {
            // Parallel Library?
            // Invoke()를 사용하면 Action들 만큼 Task를 생성해서 실행시켜줌
            // 즉, ThreadPool에 있는 Thread들을 하나씩 꺼내서 사용함           
            Parallel.Invoke(WhoAmI, WhoAmI, WhoAmI, WhoAmI, WhoAmI, WhoAmI);
            // 모든 사용이 끝나면 폐기
            ThreadName.Dispose();
        }

        // 응용 방법?
        // 일감들이 어마어마하게 많게 큐에 저장이 되어 있다면
        // 큐에서 하나씩만 꺼내서 처리하는 것이 아니라 
        // 100개씩 한 뭉텅이씩 자신의 공간에 넣고(TLS)
        // 필요할 때마다 하나씩 꺼내서 사용하면 됨
        // 즉 JobQueue에 진입 후 lock을 건 상태에서 최대한 일감을 많이 가지고 와서
        // TLS에 저장하면 좀더 경합을 줄일 수 있어서 부하를 낮출 수 있음
        // 이런 상황이 아니라도 다양한 상황에서 사용이 됨
        // 위의 예와 같이 ThreadName이든 Thread의 고유 ID를 만들든
    }
}