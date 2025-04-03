**Lock.cs의 역할**
ReaderWriterLock을 직접 구현함. (기본 .NET의 ReaderWriterLock 안 씀)

ReadLock(), WriteLock() 함수 직접 구현.
```csharp
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ServerCore
{
    // lock 생성 정책
    // 재귀적 락을 허용할지 (No) 
    // => WriteLock을 Acquire한 상태에서 또 다시 재귀적으로 같은 쓰레드에서 Acquire를 시도하는 것을 허용할지 여부를 결정
    // 허용을 안하는 것이 좀더 쉬움
    
    // SpinLock 정책 (5000번 -> Yield) 
    // Yield : 자신의 제어권을 양보 
    class Lock
    {
        // [Unused(0)] [WriteThreadId(15)] [ReadCount(16)]
        // 0000 0000 0000 0000 0000 0000 0000 0000
        // 가장 왼쪽 0 : Unused(0)
        // 가장 왼쪽 1 ~ 15 : WriteThreadId(15)
        // 가장 왼쪽 16 ~ 32 : ReadCount(16)
        // 꿀팁 : 16진수 : 2진수 = F : 11111
        const int EMPTY_FLAG = 0x00000000;
        const int WRITE_MASK = 0x7FFF0000;
        const int READ_MASK = 0x0000FFFF;
        const int MAX_SPIN_COUNT = 5000;

        // ReadCount : ReadLock을 획득했을 때 여러 쓰레드에서 Read를 잡을 수 있음 -> 그것을 카운팅
        // WriteThreadId : WriteThread를 잡고 있는 한개의 쓰레드
        int _flag = EMPTY_FLAG;

        // _flag라는 32비트 정수 하나로 상태를 관리
        // [Unused (1)] [WriteThreadId (15)] [ReadCount (16)]
        // -----------------------------------------------
        // |     0     |     쓰기 스레드 ID     | 읽기 카운트 |
        // 쓰기 잠금: _flag의 상위 15비트를 현재 스레드 ID로 설정
        // 읽기 잠금: _flag의 하위 16비트를 1씩 증가


        public void WriteLock()
        {
            // 아무도 WriteLock or ReadLock을 획득하고 있지 않을 때, 경합해서 소유권을 얻는다.
            int desired = (Thread.CurrentThread.ManagedThreadId << 16) & WRITE_MASK;
            // 현재 스레드 ID를 상위 비트에 넣음.
            // _flag == 0 (아무도 안 쓰는 상태)일 때, 자신의 ID로 세팅 시도.
            // 안 되면 계속 반복 (SpinLock 방식, 5000번 반복 후 Thread.Yield())

            while(true)
            {
                for (int i = 0; i < MAX_SPIN_COUNT; i++)
                {
                    // 시도를 해서 성공하면 return
                    if(Interlocked.CompareExchange(ref _flag, desired, EMPTY_FLAG) == EMPTY_FLAG) // // Interlocked: 멀티스레드 환경에서 원자적(atomic) 연산 제공 → 데이터 경합 방지
                    {
                        return;
                    }

                    // if (_flag == EMPTY_FLAG)
                    // {
                    //     _flag = desired;
                    //      return;
                    // }
                }
                Thread.Yield();
            }
        }
        public void WriteUnlock()
        {
            // 초기 상태로 변경
            Interlocked.Exchange(ref _flag, EMPTY_FLAG);
            // _flag를 0으로 초기화해서 잠금 해제
        }
        public void ReadLock()
        {
            // 아무도 WriteLock을 획득하고 있지 않으면, ReadCount를 1 늘린다.
            // ReadLock 같은 경우는 누구나 접근이 가능하기 때문에 쿨하게 1씩 늘려준다.
            while (true)
            {
                for (int i = 0; i < MAX_SPIN_COUNT; i++)
                {
                    // Lock Free Programming 기초

                    // 만약 누군가 lock을 잡고 있다면(WriteLock) 
                    // expected 값이 내가 원하는 값이 아닐 테니깐  
                    // 아래 if 문에서 실패를 하게 될 것이다.
                    int expected = (_flag & READ_MASK); // READ_MASK 부분만 추출

                    
                    // 대가 예상한 값은 expected고 기대한 값은 expected + 1인데 
                    // 아래 if 문이 성공했다는 의미는 (_flag & WRITE_MASK) == 0과 동일
                    // 즉 flag과 expected가 동일 => flag값을 1 더해줌
                    // 즉 ReadLock을 성공하고 더이상 시도하지 않음
                    if(Interlocked.CompareExchange(ref _flag, expected + 1, expected) == expected)
                    // 쓰기 작업이 없을 때만 읽기 카운트를 증가함.
                    // 여러 스레드가 경합하면, 한 스레드만 성공하고 나머지는 재시도.
                    {
                        return;
                    }
                    // 체크를 하고 1을 늘리는 상황에서 다른 쓰레드에서 접근을 하면 문제가 발생할 수 있음
                    // if ((_flag & WRITE_MASK) == 0)
                    // {
                    //     _flag = _flag + 1;
                    //     return;
                    // }
                }
                Thread.Yield();
            }

            // 위의 예제를 이해하기 위한 시나리오
            // 두개의 쓰레드가 거의 동시에 ReadLock에 진입을 했고 A가 B보다 먼저 진입을 했다면
            // A expected : 0
            // B expected : 0
            // flag == 0 ? A => 1
            // flag == 0 ? B => 1 
            // 이렇게 경합을 하게 됨
            // A가 먼저 실행되었다고 가정하면
            // flag는 1로 바뀜 => A는 성공
            // 그 다음 B는 flag가 이미 바뀌었기 때문에 실패 => 다시 재시도

        }
        public void ReadUnlock()
        {
            // 읽기 카운트를 하나 줄임.
            Interlocked.Decrement(ref _flag);
        }
    }
}
```

```csharp
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ServerCore
{
    // lock 생성 정책
    // 재귀적 락을 허용할지 (Yes)  => WriteLock => WriteLock Ok, WriteLock => ReadLock Ok, ReadLock => WriteLock No
    
    // SpinLock 정책 (5000번 -> Yield) 
    // Yield : 자신의 제어권을 양보 
    class Lock
    {
        // [Unused(0)] [WriteThreadId(15)] [ReadCount(16)]
        // 0000 0000 0000 0000 0000 0000 0000 0000
        // 가장 왼쪽 0 : Unused(0)
        // 가장 왼쪽 1 ~ 15 : WriteThreadId(15)
        // 가장 왼쪽 16 ~ 32 : ReadCount(16)
        // 꿀팁 : 16진수 : 2진수 = F : 11111
        const int EMPTY_FLAG = 0x00000000;
        const int WRITE_MASK = 0x7FFF0000;
        const int READ_MASK = 0x0000FFFF;
        const int MAX_SPIN_COUNT = 5000;

        // ReadCount : ReadLock을 획득했을 때 여러 쓰레드에서 Read를 잡을 수 있음 -> 그것을 카운팅
        // WriteThreadId : WriteThread를 잡고 있는 한개의 쓰레드
        int _flag = EMPTY_FLAG;
        int _writeCount = 0; // 재귀적으로 몇 번 쓰기 락을 잡았는지 카운트

        public void WriteLock()
        {

            // 동일 쓰레드가 WriteLock을 이미 획득하고 있는지 확인
            // 현재 스레드가 이미 WriteLock을 잡고 있으면, 그냥 카운트만 올리고 리턴 (재귀 허용!!)
            int lockThreadID = (_flag & WRITE_MASK) >> 16;
            if (Thread.CurrentThread.ManagedThreadId == lockThreadID)
            {
                _writeCount++;
                return;
            }
            // 아무도 WriteLock or ReadLock을 획득하고 있지 않을 때, 경합해서 소유권을 얻는다.
            int desired = (Thread.CurrentThread.ManagedThreadId << 16) & WRITE_MASK;
            while(true)
            {
                for (int i = 0; i < MAX_SPIN_COUNT; i++)
                {
                    // 시도를 해서 성공하면 return , 다른 스레드가 아무것도 안 하고 있으면
                    if(Interlocked.CompareExchange(ref _flag, desired, EMPTY_FLAG) == EMPTY_FLAG) // Interlocked: 멀티스레드 환경에서 원자적(atomic) 연산 제공 → 데이터 경합 방지
                    {
                        _writeCount = 1;
                        return;
                    }

                    // if (_flag == EMPTY_FLAG)
                    // {
                    //     _flag = desired;
                    //      return;
                    // }
                }
                Thread.Yield();
                // 다른 스레드가 아무것도 안 하고 있을 때, 내 스레드 ID로 _flag를 세팅
                // 이 과정을 "경쟁하면서 SpinLock"이라고 해


            }
        }
        public void WriteUnlock()
        {
            int lockCount = --_writeCount; // 재귀적으로 여러 번 잡았을 수 있으므로, writeCount를 1 줄임
            // 0이 되면 진짜로 락 해제
            if (lockCount == 0)
            {
                // 초기 상태로 변경
                Interlocked.Exchange(ref _flag, EMPTY_FLAG);
            }

        }
        // 읽기 락 부분은 이해가 갈때까지 분석.
        public void ReadLock()
        {

            int lockThreadID = (_flag & WRITE_MASK) >> 16;
            if (Thread.CurrentThread.ManagedThreadId == lockThreadID)
            {
                Interlocked.Increment(ref _flag); // 재귀적으로 Write 중 → Read 가능
                return;
            }
            // 아무도 WriteLock을 획득하고 있지 않으면, ReadCount를 1 늘린다.
            // ReadLock 같은 경우는 누구나 접근이 가능하기 때문에 쿨하게 1씩 늘려준다.
            while (true)
            {
                for (int i = 0; i < MAX_SPIN_COUNT; i++)
                {
                    // Lock Free Programming 기초

                    // 만약 누군가 lock을 잡고 있다면(WriteLock) 
                    // expected 값이 내가 원하는 값이 아닐 테니깐  
                    // 아래 if 문에서 실패를 하게 될 것이다.
                    int expected = (_flag & READ_MASK); // READ_MASK 부분만 추출

                    
                    // 내가 예상한 값은 expected고 기대한 값은 expected + 1인데 
                    // 아래 if 문이 성공했다는 의미는 (_flag & WRITE_MASK) == 0과 동일
                    // 즉 flag과 expected가 동일 => flag값을 1 더해줌
                    // 즉 ReadLock을 성공하고 더이상 시도하지 않음
                    if(Interlocked.CompareExchange(ref _flag, expected + 1, expected) == expected)
                    {
                        return;
                    }
                    // 체크를 하고 1을 늘리는 상황에서 다른 쓰레드에서 접근을 하면 문제가 발생할 수 있음
                    // if ((_flag & WRITE_MASK) == 0)
                    // {
                    //     _flag = _flag + 1;
                    //     return;
                    // }
                }
                Thread.Yield();
            }

            // 위의 예제를 이해하기 위한 시나리오
            // 두개의 쓰레드가 거의 동시에 ReadLock에 진입을 했고 A가 B보다 먼저 진입을 했다면
            // A expected : 0
            // B expected : 0
            // flag == 0 ? A => 1
            // flag == 0 ? B => 1 
            // 이렇게 경합을 하게 됨
            // A가 먼저 실행되었다고 가정하면
            // flag는 1로 바뀜 => A는 성공
            // 그 다음 B는 flag가 이미 바뀌었기 때문에 실패 => 다시 재시도

        }
        public void ReadUnlock()
        {
            Interlocked.Decrement(ref _flag);
        }
    }
}
```

**Program.cs의 역할**
실제로 Lock 클래스의 WriteLock, WriteUnlock을 사용해서 스레드 동시성 테스트함.

count 값을 여러 스레드에서 증가/감소하는 실습.
```csharp
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
```
WritLock, WriteUnlock, ReadLock, ReadUnlock의 구조
ReadLock은 상호배제적이지 않기 때문에 하나하나만 들어가는 것이 아니다
WriteLock은 상호배제적으로 하나씩만 들어간다 (화장실 개념)

//-------------------------------------------------------------------------------

✅ Lock-Free란?
🔄 락(lock)을 쓰지 않고도 동시에 여러 스레드가 안전하게 데이터에 접근할 수 있는 기법

즉, 서로 기다리지 않고 블로킹(blocking) 없이 데이터를 조작할 수 있는 방식
예시로 비교해보자
🔒 일반적인 락 사용
```csharp
lock(_lock)
{
    count++;
}
```
한 스레드가 이 블록을 들어가면, 다른 스레드는 대기함 (block됨)

🔓 Lock-Free 방식
```csharp
Interlocked.Increment(ref count);
```
내부적으로는 하드웨어 수준의 원자 연산(CPU 명령) 을 이용해서,

여러 스레드가 동시에 접근해도 충돌 없이 안전하게 동작함

락을 사용하지 않기 때문에 대기 시간 없음

🧠 왜 Lock-Free가 중요할까?
![Image](https://github.com/user-attachments/assets/c922fc92-c069-459d-bb46-400aea291302)

🔧 C#에서 자주 쓰는 Lock-Free 도구들
![Image](https://github.com/user-attachments/assets/3298c0af-23e4-4e48-b725-23a356dcb4bc)

💡 핵심 메서드 예시: Interlocked.CompareExchange
```csharp
int expected = 0;
int newValue = 1;
int result = Interlocked.CompareExchange(ref _flag, newValue, expected);
```
_flag의 값이 expected면 → newValue로 바꾸고
아니면 아무것도 안 함 (그리고 기존 값을 반환함)

이걸 기반으로 경합 없이 락 없이 자원 제어를 할 수 있음.

🕹️ 게임 서버에서 Lock-Free 예시
채팅 로그 큐, 네트워크 패킷 큐, 오브젝트 풀링 또는 접속 유저 수 카운터 같은 단순 숫자 조작에도 자주 씀
```csharp
Interlocked.Increment(ref connectedUserCount);
```
