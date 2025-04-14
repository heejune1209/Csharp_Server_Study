using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ServerCore
{
    // 궁금증
    // 서버를 만들고 있고 서버란 Listener 즉 클라이언트의 접속을 대기하는 프로그램인데
    // 왜 커넥터가 필요할까?
    // 두가지 이유
    // 1. ServerCore의 경우 서버를 메인 용도로 만들고 있지만
    // Connect, Recieve, Send 하는 부분은 공용으로 사용하는 것이 좋다
    // 2. Server를 나중에 Contents로 올릴 때 MMO의 경우 Server를 하나 짜리로 만들지
    // 아니면 분산처리를 해서 어떤 서버는 NPC AI만 담당하는 역할을 하고 
    // 또 어떤 서버는 나머지 몬스터 관리나 필드 아이템 같은 나머지 컨텐츠를 관리를 할지
    // 이렇게 분할해서 만드는 경우가 있다. 이런 경우에 메인 서버로 작동하는 프로그램이
    // 있겠지만 반대로 다른 서버에 Connect하는 방식으로 연결이 되어야 한다.
    // 결국에는 분산 서버를 구현한다는 것은 서버가 서로 연결되기 위해서 Connect가 필요하다.
    // 즉, 한쪽은 Listener 상태 다른 한쪽은 Connect 상태가 되어야 한다.(서버 <-> 서버)

    // Connector는 클라이언트 입장에서 서버에 연결 요청 (ConnectAsync())을 하는 역할.
    public class Connector
    {
        // Connect 한 순간 어떤 Session을 만들어줄지를 인자로 받아와서 결정
        Func<Session> _sessionFactory;
        public void Connect(IPEndPoint endPoint, Func<Session> sessionFactory, int count = 1)
        {
            // count 만큼의 소켓을 생성하여 비동기 연결을 시도한다.
            for (int i = 0; i < count; i++)
            {
                // 세션 팩토리 함수를 통해 세션을 생성
                _sessionFactory = sessionFactory;
                // 소켓을 생성하고, 비동기 연결을 위한 SocketAsyncEventArgs를 준비
                Socket socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                SocketAsyncEventArgs args = new SocketAsyncEventArgs();
                args.Completed += OnConnectedComplected; // 연결 완료 시 호출될 콜백 등록
                args.RemoteEndPoint = endPoint; // 연결할 서버의 주소 설정
                args.UserToken = socket;

                RegisterConnect(args);
                // 소켓을 UserToken에 등록
                // user 정보를 등록할 때 소켓의 정보도 함께 등록이 가능
                // UserToken이 object 타입이기 때문에 어떤 데이터든 저장이 가능

                // socket을 멤버 변수로 받아서 Register할 때 사용하지 않는 이유는
                // Connect를 한번만 하는 경우도 있겠지만 Listener에서 뺑뺑이 돌면서
                // 1000명이든 10000명이든 계속 받을 수 있는 것 처럼
                // 한명만 받고 끝내는 게 아니라 여러 명(여러 클라이언트)과 동시에 연결할 수 있어야 하기 때문에 
                // 멤버 변수로 받지 않고 이벤트를 통해 인자로 전달하는 것이 좋다.

                // 커넥터는 여러 개의 소켓을 동시에 ConnectAsync() 시도해야함.
                // 멤버 변수 하나에만 소켓을 저장하면? → 덮어쓰기됨 ❌
                // 그래서 각각의 args 안에 있는 UserToken에 각각의 소켓을 개별로 보관해야 함 ✅
                // 그리고 리스너와 달리 OS가 소켓을 기억 안 해주기 때문.

                // 그럼 반대로 리스너랑 세션에서는 커넥터와 반대로 멤버변수로 받아야 하는 이유는 
                // Session은 하나의 클라이언트를 담당하는 단위이기 때문이고, 
                // Session은 클라이언트마다 1:1로 매칭되는 객체이다.
                // 그래서 내부에서 SocketAsyncEventArgs를 재사용할 수 있고,
                // Socket도 멤버로 들고 있어도 괜찮음

                // 그리고 두번째 이유는 ConnectAsync() 콜백에서 소켓을 직접 꺼낼 방법이 없음
                // ConnectAsync()는 완료되더라도 args 안에 소켓 정보가 자동으로 채워지지 않음.
                // 따라서 내가 만든 소켓을 UserToken에 미리 넣어둬야 콜백에서 꺼낼 수 있다.

                // 그러면 Listener는 왜 UserToken 없이도 괜찮은가?
                // 리스너는 AcceptAsync()를 통해서 연결된 소켓이 자동으로 들어옴
                // - args.AcceptSocket 속성에 연결된 클라이언트 소켓이 자동 세팅됨.
                // - 즉, OS가 알아서 해주니까 굳이 UserToken 쓸 필요가 없음!

                // 그리고 Listener는 자기 자신을 위한 listenSocket(즉, 서버 리스닝 소켓)은 멤버 변수로 갖고,
                // '클라이언트 소켓'을 멤버 변수로 저장하지 않는다"는 의미

                // 즉, 요약하자면 “서로 통신하기 위한 자기만의 소켓을 하나씩 가지고 있고,
                // 근데 커넥터는 리스너와 세션과 달리 연결하려는 애들이 많으니까 자기만의 소켓을 멤버 변수로 선언해서 쓰지 않고,
                // 자기만의 소켓을 하나씩 계속 만드는 느낌”
                // 이게 딱 비동기 네트워크 소켓 구조의 핵심을 완벽히 짚은 표현.

                //args.UserToken = socket; // 소켓을 UserToken에 등록
                // args.UserToken이란 내 마음대로 쓸 수 있는 "보관함" 같은 느낌
                // 타입은 object이기 때문에 어떤 객체든 넣을 수 있다.(Session, ID, 유저 정보 등등)
                //  왜 필요해?
                // SocketAsyncEventArgs는 작업 완료 시 콜백(Completed)을 호출하지만
                // → 그 안에 "누가 요청한 작업인지" 정보가 없어.
                // 그래서 우리가 직접 args.UserToken에
                // **관련된 객체(예: Session, ClientID 등)**를 담아두는 거야.

                //RegisterConnect(args);

                // 이 과정에서 Connector는 각 연결 시도마다 별도의 Socket을 생성하고,
                // 이를 args.UserToken에 저장함으로써, 나중에 연결 완료 콜백에서 해당 소켓을 가져올 수 있게 한다.
                // 여러 연결 요청을 동시에 처리할 수 있도록 독립적으로 소켓 객체들이 생성된다.
            }
        }
        void RegisterConnect(SocketAsyncEventArgs args)
        {
            // UserToken이 object 타입이기 때문에 Socket으로 타입을 바꿔줌
            Socket socket = args.UserToken as Socket; 
            if (socket == null)
            {
                return;
            }
            bool pending = socket.ConnectAsync(args);
            if (pending == false)
            {
                OnConnectedComplected(null, args);
            }
        }

        // ConnectAsync() 완료 후:
        // Connector의 OnConnectedComplected() 콜백이 실행된다.
        void OnConnectedComplected(object sender, SocketAsyncEventArgs args)
        {
            if (args.SocketError == SocketError.Success)
            {
                // 이렇게 Session이 현재 abstract로 만들었기 때문에 
                // 어떤 Session을 만들어야하는지 알아야 한다.
                // 이것을 _sessionFactory를 통해 받아온다.
                // 세션 팩토리 함수를 호출해  (예: () => new GameSession())를 실행하여,
                // 새로운 Session(예: GameSession) 인스턴스를 생성
                Session session = _sessionFactory.Invoke();

                // session을 start하기 위해서는 socket이 필요한데
                // 이유는 session에서 register를 할 때 socket이 필요함
                // 즉 현재 연결한 socket으로 register를 한다.
                // UserToken에 있는 socket을 사용해도 동일하게 동작은 할것이다.
                // 그래도 ConnectSocket이 좀더 세련되어 보임
                
                // 새로 생성한 Session 객체에 현재 연결된 소켓을 전달하여 
                // 해당 클라이언트와의 통신을 시작하도록 한다.
                session.Start(args.ConnectSocket);

                // 생성된 Session 객체의 Start() 메서드는
                // 전달받은 연결 소켓 (args.ConnectSocket)을 내부 멤버 변수에 저장하고,
                // 비동기 네트워크 수신(ReceiveAsync)을 위한 초기 준비를 시작한다.

                // 연결이 성공했다는 것을 Session 내부에 알리기 위해 OnConnected 호출
                session.OnConnected(args.RemoteEndPoint);
            }
            else
            {
                System.Console.WriteLine($"OnConectedComplected Fail : {args.SocketError}");
            }
        }
    }
}