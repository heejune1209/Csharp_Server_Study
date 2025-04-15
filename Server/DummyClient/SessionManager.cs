using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using ServerCore;

namespace DummyClient
{
    // 역할 
    // Session ID를 관리하지는 않음
    class SessionManager
    {
        static SessionManager _session = new SessionManager();
        public static SessionManager Instance { get { return _session; } }
        List<ServerSession> _sessions = new List<ServerSession>();
        object _lock = new object();

        // 모든 세션에 패킷을 전송하는 메서드
        public void SendForEach()
        {
            lock (_lock)
            {
                foreach (ServerSession session in _sessions)
                {
                    C_Chat chatPacket = new C_Chat();
                    chatPacket.chat = "Hello Server !";

                    // 전송전에 C_Chat 객체의 정보를 chatPacket.Write() 호출을 통해 직렬화해서
                    // ArraySegment<byte> 형태의 바이트 데이터로 변환합니다.
                    ArraySegment<byte> segment = chatPacket.Write();

                    session.Send(segment);
                }
            }
        }

        public ServerSession Generate()
        {
            lock (_lock)
            {
                ServerSession session = new ServerSession();
                _sessions.Add(session);
                return session;
            }
        }

    }
}