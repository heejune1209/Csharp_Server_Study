using System;
using System.Threading;

namespace ServerCore
{
    internal class Program
    {
        static void MainThread(object state)
        {
            for (int i = 0; i < 5; i++) 
                Console.WriteLine("Hello, Thread!");
        }
        static void Main(string[] args)
        {
            // 쓰레드 생성
            // Main 직원 -> 이 프로세스에 원래 있는 직원
            // Thread 생성 => 한명의 직원을 더 고용해서 MainThread라는 일을 하도록 시킴
            // 쓰레드를 생성하는 것은 정직원을 고용하는 것과 비슷함
            // 너무 부담이 됨
            // 쓰레드가 단순한 작업을 한다면 굳이 쓰레드를 새로 만들 필요가 없음
            // 언제 쓰레드가 종료 후 소멸?
            // Main() 함수가 종료되면 종료 후 소멸

            // SetMinThreads, SetMaxThreads로 최소, 최대 스레드 개수를 지정할 수 있음. 두번째 인자는 IO 관련
            ThreadPool.SetMinThreads(1, 1);
            ThreadPool.SetMaxThreads(5, 5);
            // QueueUserWorkItem는 다음 작업을 할 수 있는 스레드를 할당해준다.
            // SetMinThreads, SetMaxThreads로 최소, 최대 스레드를 지정해주므로, 최대 스레드 이상을 넘어갈 때 스레드를 요청하면 스레드가 끝날 때까지 기다렸다가 할당해준다.
            // 따라서 금방 일이 끝난다는 보장이 있으면 좋지만, 아니라면 스레드 할당을 보장받지 못한다.

            // [ThreadPool] : 직원들의 대기 집합소
            //ThreadPool.QueueUserWorkItem(MainThread);
            // 콜백함수같은 동작 방식
            // ThreadPool -> static 함수들로 이루어짐
            // 쓰레드를 생성하지 않고도 가볍게 사용할 수 있음
            // MainThread가 background에서 돔
            // (Thread.IsBackground = true -> Main()이 종료되면 쓰레드도 종료)
            // ThreadPool의 원리
            // 이미 직원들이 마련된 상태이고 대기 중인 상태
            // 할 일을 넘겨주면 일을 하고 다시 대기 중으로 돌아감
            // 유동적으로 기다리는 직원을 사용함
            // 이것을 Thread Pooling이라고 한다.
            // 창고에 넣다가 필요하면 꺼내 쓰는 원리 -> Pooling

            // [ThreadPool 개수]
            // 쓰레드 개수
            // 위와 같이 직원을 1000명 고용을 할 수 있지만 
            // 이렇게 한다고 해서 1000배의 호율이 나오지는 않음. 이는 CPU 코어의 개수와 연관이 있음
            // 그래서 CPU 코어의 개수와 쓰레드의 개수를 맞추는 것이 좋음
            // 따라서 아래와 같은 방법은 최악의 경우이다.
            // 쓰레드를 왔다갔다 하는 작업이 일을 시키는 작업보다 더 오래걸림
            // 일을 하는 것이 결국 더 중요
            // 최대로 돌아가는 쓰레드 개수를 제한 -> 쓰레드 풀
            // 쓰레드 풀에서 돌아가는 작업이 만약 무한 반복되는 상황이라면
            // 해당 쓰레드는 더이상 못쓰기 때문에 무조건 쓰레드 풀을 사용하는 것은 좋지 않음
            // 그래서 쓰레드 풀은 간단한 일을 넘기기에 좋음

            // [ThreadPool의 단점]
            // 쓰레드 풀이 먹통이 되는 현상
            // SetMinThreads 쓰레드 최소 개수를 설정, SetMaxThreads 쓰레드 최대 개수
            // SetMinThreads(or SetMaxThreads)의 첫번째 매개변수 => 이 일을 할 대상
            // 아래에서 5개의 ThreadPool이 모두 무한반복을 하고 있는데 어떻게 될까?
            // 모든 인력을 사용중에 있어서 아래 쓰레드를 실행 못함
            // 위와 같이 쓰레드 풀이 좋기는 하지만 모두 계속 일을 하고 있다면
            // 전체 프로그램이 먹통이 되는 일이 발생할 수 있다.

            // [Task]
            // 위의 ThreadPool의 단점을 극복하기 위해 등장한 것이 Task이다.
            // Task도 ThreadPool을 통해 작업을 진행함
            // 단 ThreadPool과 차이는 옵션을 넣을 수 있다.
            // ex. TaskCreationOptions.LongRunning => 오래 걸리는 작업이라는 것을 알려줌
            // 위 옵션을 넣어주면 ThreadPool을 모두 사용 중에 있더라도 ThreadPool을 통해 MainThread()가 돌아간다.
            // ⇒ ThreadPool의 단점 커버
            // ThreadPool을 뽑아서 처리하는 게 아니라 별도로 Task를 생성한 후 작업을 하는 것이라고 생각
            // Thread와 ThreadPool의 장점을 모아놓은 느낌
            // Task는 직원을 고용한다기 보다 일감 단위를 우리가 정의해서 사용하는 것에 가까움
            for (int i = 0; i < 5; i++)
            {
                // TaskCreationOptions.LongRunning 옵션을 사용해서 
                // 해당 작업이 오래 걸린다는 것을 알려주면 스레드를 5개
                // 아래 ThreadPool의 작업이 정상적으로 실행된다.
                Task task = new Task(() => { while (true) { } }, TaskCreationOptions.LongRunning);
                task.Start();
            }
            ThreadPool.QueueUserWorkItem(MainThread);
            /*
            for (int i = 0; i < 5; i++) // 만약 i<4로 스레드를 4개만 썻다면 MainThread가 실행됨
            {
                ThreadPool.QueueUserWorkItem((obj) => { while (true) { } }); // 람다식
            }

            ThreadPool.QueueUserWorkItem(MainThread);
            // ThreadPool.QueueUserWorkItem(MainThread);로 요청한 스레드는 최대 스레드 수를 초과했기 때문에 할당받지 못한다.
            */
            /*
            for (int i=0; i < 1000; i++)
            {
                Thread t = new Thread(MainThread); // 정직원 고용
                
                // [Thread 이름] 
                t.Name = "Test Thread"; // 쓰레드를 이름으로 구분하는 것도 괜찮은 방법
                t.IsBackground = true; // Foreground 스레드가 끝나면 같이 종료
                // 쓰레드가 위와 같이 BackGround에서 실행되도록(IsBackground = true) 하면
                // Main()이 종료 되었을 때 while문이 도는 것과 상관 없이 쓰레드가 종료됨 
                // 만일 IsBackground == false 라면
                // Main()이 종료 되더라도 쓰레드는 계속 동작을 하게 된다.            
                // IsBackground의 default 값은 false이다.
                
                t.Start();
                //Console.WriteLine("스레드 조인 대기중");
                
                // [Thread가 끝날 때까지 기다리고 싶을 때]
                //t.Join(); // 스레드가 끝나기를 기다림
                // Thread가 끝날 때까지 기다리고 싶을 때? ⇒ Join() 메서드를 활용. Join은 C++에도 존재 
                // Main 함수에서 Thread 객체인 t를 생성했다. 이것에 MainThread라는 메소드를 지정했다.
                // Thread.Start 메소드를 통해 스레드를 시작해주었고, Thread.Join 메소드를 통해 스레드가 끝나기를 기다렸다.
                
                //Console.WriteLine("Hello, World!");
            }
            */
            while (true)
            {

            }
        }
    }
}
