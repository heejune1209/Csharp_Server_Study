﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ServerCore;


namespace Server
{
    
    class ServerProgram
    {
        // 해당 리스너는 프로그램 전체에서 하나의 인스턴스로 공유.
        // 어디서든 접근할 수 있게 할수 있다.
        static Listener _listener = new Listener();
        // 향후 다양한 GameRoom을 생성해서 관리하는 Manager가 등판할 예정
        public static GameRoom Room = new GameRoom();
        // ServerProgram에서는 GameRoom 인스턴스를 전역적으로 관리하며,
        // 주기적으로 JobTimer에 Flush 작업을 예약해 방에 쌓인 메시지를 한 번에 브로드캐스트합니다.
        // 예를 들어 ServerProgram.Room.Push(() => Room.Flush())
        // 또는 JobTimer.Instance.Push(FlushRoom, 250) 형태로 호출합니다.

        static void FlushRoom()
        {
            Room.Push(() => Room.Flush());
            // 일정 간격마다 Flush를 예약
            JobTimer.Instance.Push(FlushRoom, 250);
        }

        static void Main(string[] args)
        {
            
            // 서버의 IP와 포트를 결정
            string host = Dns.GetHostName();
            IPHostEntry ipHost = Dns.GetHostEntry(host);
            IPAddress ipAddr = ipHost.AddressList[0];
            IPEndPoint endPoint = new IPEndPoint(ipAddr, 7777);

            // 문지기 배치
            // 이때, Init()에 세션 생성 팩토리 함수(예: () => new GameSession())도 함께 전달하여
            // 클라이언트가 접속할 경우 세션을 어떤 방식으로 만들지를 결정
            
            // 리스너는 서버편에서 클라이언트의 연락을 대기 및 연결 수락(AcceptAsync())의 역할을 함.

            // Session Manager를 통해서 Session을 발급해주도록 개선할 수도 있다.
            // 그래야 발급한 Session의 갯수와 Session ID를 관리하기 쉽다.
            _listener.Init(endPoint, () => { return SessionManager.Instance.Generate(); }); // 세션을 만드는 함수 등록
            Console.WriteLine("Listening...");

            // 프로그램이 종료되지 않도록 임시록 남겨둠
            // 여기서 메인쓰레드는 무한 반복을 돌고 있지만
            // 어떻게 OnAcceptHandler가 콜백함수가 실행이 되는 걸까?
            // AcceptAsync()를 실행할 때 SocketAsyncEventArgs를 넣어주면 
            // 콜백 함수는 쓰레드 풀에서 실행이 됨
            // 따라서 Listener의 OnAcceptCompleted를 레드존이라 생각하고
            // 메인 쓰레드와 경합 조건이 발생하지 않도록 고려해야 한다.      
            
            // FlushRoom();
            JobTimer.Instance.Push(FlushRoom);

            while (true)
            {
                // 예약된 작업을 실행
                JobTimer.Instance.Flush();
            }

        }
    }
}