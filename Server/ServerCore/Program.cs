using System;
using System.Drawing;
using System.Threading;

namespace ServerCore
{
    internal class Program
    {
        
        static void Main(string[] args)
        {
            int[,] arr = new int[10000, 10000];

            {
                long now = DateTime.Now.Ticks;
                for (int y = 0; y < 10000; y++)
                {
                    for (int x = 0; x < 10000; x++)
                    {
                        arr[y, x] = 1;
                    }
                }
                long end = DateTime.Now.Ticks;
                Console.WriteLine($"(y,x) 순서 걸린 시간 : {end - now}");
            }

            {
                long now = DateTime.Now.Ticks;
                for (int y = 0; y < 10000; y++)
                {
                    for (int x = 0; x < 10000; x++)
                    {
                        arr[x, y] = 1;
                    }
                }
                long end = DateTime.Now.Ticks;
                Console.WriteLine($"(x,y) 순서 걸린 시간 : {end - now}");
            }

            // 수학적으로 보면 동일해야하는 데 과연 결과는?

            // 나의 컴퓨터 기준
            // 1988577
            // 3475538

            // 생각보다 많이 차이가 발생
            // 왜 그럴까?
            // 공간적 (Special Locality)
            // 어떤 변수에 접근할 때 해당 주소 근처에 있는 변수 값에 접근할 확률이 높을 것이다.

            // 5 * 5 배열
            // [1] [1] [1] [1] [1]  
            // [] [] [] [] []  
            // [] [] [] [] []  
            // [] [] [] [] []  
            // [] [] [] [] []  
            // x의 값을 먼저 늘리기 때문에 행의 값을 먼저 할당함
            // 행의 주소가 인접해 있기 때문에 캐쉬에 저장을 해서 
            // 값을 할당하는 방식이라 좀더 빠르다.

            // 5 * 5 배열
            // [1] [] [] [] []  
            // [1] [] [] [] []  
            // [1] [] [] [] []  
            // [1] [] [] [] []  
            // [1] [] [] [] []  
            // x의 값을 먼저 늘리기 때문에 열의 값을 먼저 할당함
            // 주소의 거리가 좀더 멀기 때문에 
            // 값을 할당하는 시간이 조금 더 느리다.
        }
    }
}
