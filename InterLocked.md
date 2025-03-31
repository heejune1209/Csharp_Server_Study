경합 조건

```csharp
using System.Threading.Tasks;

namespace ServerCore
{
    class Program
    {
        static int number = 0;

        static void Thread_1()
        {
            for (int i = 0; i < 100_000; i++)
            {
                number++;
                /*
                int temp = number; // 0
                temp += 1; // 1
                number = temp; // number = 1
                */
            }
        }

        static void Thread_2()
        {
            for (int i = 0; i < 100_000; i++)
            {
                number--;
                /*
                int temp = number; // 0
                temp -= 1; // 1
                number = temp; // number = 1
                */
            }
       

        static void Main(string[] args)
        {
            Task t1 = new Task(Thread_1);
            Task t2 = new Task(Thread_2);
            t1.Start();
            t2.Start();

            Task.WaitAll(t1, t2);
            System.Console.WriteLine(number);
        }
    }
}
```

멀티 쓰레드 환경에서 10만을 더하고 빼는 연산을 실행했을 때 

0이 아니라 이상한 값이 나오는 경우가 발생한다.

이유는,

++ 연산이 아래와 같이

```csharp
number++;
/*
int temp = number; // 0
temp += 1; // 1
number = temp; // number = 1
*/
```

3단계로 쪼개서 작업이 진행이 되는 데

멀티 쓰레드 환경에서 `number = temp;` 이 연산이 될 때 

number의 값이 이미 다른 쓰레드에서 값을 바꾼 상태라면

해당 연산이 중복이 되기 때문이다. 

즉, number의 값이 동기화 되지 않은 값(num- 의 결과가 진행되지 않은 값)을 가지고 와서 할당을 해주기 때문에 연산이 중복이 될 수 있다.

```csharp
using System;
using System.Threading.Tasks;
using System.Threading;

namespace ServerCore
{
    class Program
    {
        static int number = 0;

        static void Thread_1()
        {
            // atomic == 원자성 
            // 더이상 쪼갤 수가 없다.
            // 어떤 동작이 한번에 일어나야한다?
            
            for (int i = 0; i < 100_000; i++)
            {
                Interlocked.Increment(ref number); // 성능에서 손해가 크다. 작업을 쪼개지 않고 한번에 일어나도록 해준다.                
                // number ++ 
            }
        }

        static void Thread_2()
        {
            for (int i = 0; i < 100_000; i++)
            {
                Interlocked.Decrement(ref number); // 작업을 쪼개지 않고 한번에 일어나도록 해준다.
            }
        }
        
        static void Main(string[] args)
        {
            Task t1 = new Task(Thread_1);
            Task t2 = new Task(Thread_2);
            t1.Start();
            t2.Start();

            Task.WaitAll(t1, t2);
            System.Console.WriteLine(number);
        }
    }
}
```

위 문제를 해결하기위해 Interlock 계열의 함수를 사용하게 된다.

이때 등장하는 개념이 atomic(원자)이란 개념인데

원자의 더이상 쪼갤 수 없는 성격과 같이 더이상 쪼갤 수 없는 단위로 한번에 실행할 수 있도록 해주는 개념이다.

++ 연산이 3단계에 거쳐서 쪼개져서 실행되는 문제를 해결해줄 수 있는 방법이다.

왜 이상한 값이 나올까? -21344 등등..

원자적으로 덧셈과 뺄셈을 하는 방법을 사용

Interlocked 계열의 함수에서는 메모리 배리어를 간접적으로 사용하고 있다.

따라서 가시성 문제는 일어나지가 않는다(= 메모리를 캐쉬에 들고 있는  것이 아니라 메인 메모리로 바로 보낸다).

-> volatile을 잊고 살아도 된다.

[**경합 조건] : Race Condition**

All or Nothing ⇒ 전부 실행되거나 아에 실행이 되지 않거나의 문제, 즉 순서 보장

동시 다발로 실행을 하면 최종 승자가 먼저 실행을 하고

실행을 되는 것이 보장 되기 때문에 나머지 연산은 대기 후 실행 됨
그러다 보니까 당연히 일반적인 넘버 뿔뿔이랑 넘버 마이너스 마이너스 하는 것보다는 훨씬 더 느릴 것이다.  애당초 이런 식으로 하게 되면은 우리가 기존에 얘기했던 그 캐시의 개념은 사실 진짜 쓸모없게 된다.

결국에는 우리 고급 식당 예제로 다시 돌아오면 아까 같은 경우에는 동시다발적으로 경합을 해가지고 애들(스레드)이 동시에 콜라 배달하는 작업을 해가지고 문제가 된건데 이제는 사실 그 원자성을 추가한다면 콜라를 주문 현황에서 없애고 그걸 배달하는 모든 일련의 작업들을 원자적으로 한 번에 일어나게 만들어 준다는 개념이 된다. 

그러니까 결국에는 얘네들이 주문 현황에 콜라가 뜬 걸 보자마자 서로 열심히 달리기 시합을 해가지고 맨 먼저 도착한 애만 이렇게 꺼내가지고 이거를 실제 배달할 권리를 얻게 된다. 그러면 결국엔 나머지 두 명은 늦게 도착했으면 허탕을 치는 그런 개념이 된다.

**[ref를 붙은 것과 아닌 버전의 미묘한 차이]**
```csharp
for (int i = 0; i < 100_000; i++)
{   
    // 그리고 Interactive Increment를 보면은 여기다가 넘버를 그냥 넣어주는 게 아니라 레퍼런스로 넘버를 넣어주고 있다.
    // 즉, 참조값으로 넣어주고 있다는 것은 결국 이 넘버라는 수치 자체를 여기다가 넣어주는 게 아니라 이 넘버버의 주소값을 넣어주고 있다고 
    // 생각을 하면 되는데 이렇게 된 이유에서도 곰곰히 생각을 해볼 필요가 있다.
    // 만약 Interlocked.Increment(number); 이렇게 했으면 값을 지금 복사해가지고 여기 인크래먼트에다가 넣어준다는 얘기가 된다.
    // 근데 이렇게 하면은 말이 안되는게 애당초에 넘버의 값을 우리가 갖고 오는 순간에 이미 다른 애가 접근해가지고 
    // 그 값을 이미 다른 값으로 수정을 했을 수도 있다. 그러니까 레이스 컨디션 문제가 해결이 되지 않는다는 문제가 되고, 그렇다는 것은
    // 이렇게 ref를 붙여가지고 레퍼런스를 붙였다는 것은 이제 어떻게 해석을 하면 되냐면은 
    // 이 number가 지금 어떤 값인지 나는 알지 못하지만 여기 있는 값을 참조해가지고 
    // 즉, 여기 주소에 가가지고 거기 안에 있는 수치를 어떤 값인지는 모르겠지만 무조건 1을 늘려줘라는 뜻이 된다.
    // ref를 붙은 것과 아닌 버전의 미묘한 차이를 깨우쳐야 한다.
        
    int prev = number;
    Interlocked.Increment(ref number); 
    int next =number;
    // 만약에 이런 상황일때 싱글스레드마인드로 생각하면 Interlocked.Increment가 1을 증가시키는 거니까 프리브에서 1을 더하면 넥스트가 나올것 같은데 당연히 그렇지 않다.
    // 애당초 이 넘버라는 애는 스레드끼리 공유해서 사용하고 있으니까 얘를 이렇게 꺼내와서 사용하는게 말이 안된다.
    // 왜냐하면 우리가 꺼내와서 쓰는 순간에도 누군가가 얘를 이런 식으로 건드려 가지고 얘가 값이 바뀔 수도 있다는 얘기가 되는 것이다.
    // 근데 그렇다는 것은 또 하나 굉장히 궁금한게 그러면은 여기 증가된 값이 얼마인지를 내가 이렇게 축출하고 싶은데 이렇게 int prev = number 사용하는 거는 말이 안된다고 했었다.
    // 그렇기 때문에 Increment 보면은 리턴 값이 있는 것이다. 그래서 Increment가 반환하는 값은 실제로 얘가 인크래멘트 된 다음에 실제로 바뀐 값, 
    // 그러니까 int aftervalue = Interlocked.Increment(ref number); 이렇게 추출하면 100퍼센트 맞는 값을 반환하겠지만, 나중에 궁금하다고 number를 빼가지고 작업하는 것은 사실 말이 안된다는 뜻.

}
```