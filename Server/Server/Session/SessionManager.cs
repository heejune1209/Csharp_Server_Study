using System;
using System.Net;
using System.Threading;
using ServerCore;
using System.Collections.Generic;

namespace Server
{
    // ServerCore에서 작업해도 괜찮다. => 단순 취향 차이
    // 생성된 클라이언트 세션은 SessionManager에 의해 관리되어,
    // 각 세션에 고유한 ID를 부여하고, 필요 시 찾거나 제거할 수 있도록 합니다.
    class SessionManager
    {
        static SessionManager _instance = new SessionManager();
        public static SessionManager Instance { get { return _instance; } }

        // 티켓 아이디
        int _sessionId = 0;
        Dictionary<int, ClientSession> _sessions = new Dictionary<int, ClientSession>();
        object _lock = new object();

        // Session 생성 및 ID 발급
        public ClientSession Generate()
        {
            lock (_lock)
            {
                int sessionId = ++_sessionId;
                // 나중에 Pooling 할 생각으로 이미 만들어져 있는 Session들을 반환하고 싶다면
                // 초반에 이렇게 만든 Session을 Queue에 저장하고 있다가
                // 하나씩 뽑는 방법도 있다.
                // 여기서는 요청이 왔을 때 동적으로 생성하는 방법을 사용
                ClientSession session = new ClientSession();
                session.SessionId = sessionId;
                _sessions.Add(sessionId, session);

                Console.WriteLine($"Connected : {sessionId}");
                return session;
            }
        }

        // 세션을 찾는 메서드
        public ClientSession Find(int id)
        {
            lock (_lock)
            {
                ClientSession session = null;
                _sessions.TryGetValue(id, out session);
                return session;
            }
        }
        // 세션을 제거하는 메서드
        public void Remove(ClientSession session)
        {
            lock (_lock)
            {
                _sessions.Remove(session.SessionId);
            }
        }
    }
}