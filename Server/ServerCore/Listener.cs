using System;
using System.Net;
using System.Net.Sockets;

namespace ServerCore
{
    // Listener는 서버 입장에서 클라이언트의 연락을 대기 및 연결 수락 (AcceptAsync())의 역할을 함.
    public class Listener
    {
        Socket _listenSoket;

        // 세션을 어떤 방식으로 누구를 만들어줄지를 정의
        // Session 객체를 반환하는 함수를 담는 변수
        Func<Session> _sessionFactory;

        public void Init(IPEndPoint endPoint, Func<Session> sessionFactory)
        {
            // 세션을 어떻게 생성할지 결정하는 팩토리 함수를 저장
            _sessionFactory += sessionFactory;
            // 리스닝 소켓 생성
            _listenSoket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            // IP와 포트를 바인딩 – 서버의 "주소와 전화번호" 지정
            _listenSoket.Bind(endPoint);
            // 클라이언트 연결 대기를 시작 (backlog 10으로 설정)
            _listenSoket.Listen(10);

            // 현재 문지기를 한명만 고용을 했기 때문에 
            // 너무 많은 사람이 한번에 접속을 하면 다소 느릴 수도 있다
            // 해서 이 부분을 늘려주면 된다.
            // 클라이언트의 연결 요청을 비동기로 기다리기 위해 여러 Accept 대기 작업을 등록한다.
            for (int i = 0; i < 10; i++)
            {
                SocketAsyncEventArgs args = new SocketAsyncEventArgs();
                // 연결 완료 시 실행될 이벤트 핸들러 등록
                args.Completed += new EventHandler<SocketAsyncEventArgs>(OnAcceptCompleted);
                // 비동기 연결 요청(접속 대기) 등록
                RegisterAccept(args);
            }
        }

        // 등록을 예약
        private void RegisterAccept(SocketAsyncEventArgs args)
        {
            // 이전 연결 정보 초기화
            args.AcceptSocket = null;
            // 비동기 Accept 시작: 클라이언트 연결 요청을 OS에 전달한다.
            bool pending = _listenSoket.AcceptAsync(args);

            // 의문점
            // 만일 계속 pending이 false가 되어서 
            // OnAcceptCompleted를 호출하고 다시 RegisterAccept를 호출하는 식으로
            // 무한 반복이 되면 어떻게 될까? => 실제로 무한 반복이 일어나면 stack over flow가 발생함
            // 우선 Backlog를 10명을 걸어놔서 현실적으로 계속 stack over flow가 발생하지 않고 
            // 그리고 pending이 계속 false가 되는 상황은 현실적으로 잘 일어나지는 않는다.
            // 그래도 우려가 되면 수정을 하긴 해야 함
            
            // 만약 pending이 false라면, 즉 연결 요청이 즉시 완료되었다면...
            if (pending == false)
            {
                // 즉시 OnAcceptCompleted()를 호출하여 연결 처리
                OnAcceptCompleted(null, args);
            }
        }

        // 클라이언트가 연결되면 호출됨
        private void OnAcceptCompleted(object sender, SocketAsyncEventArgs args)
        {
            if (args.SocketError == SocketError.Success)
            {
                // Program에 있다가 이곳으로 옮긴 이유
                // Session에서 만든 OnConnected를 이곳에서 실행 시키기 위해서 
                // 즉 코어(엔진)에서 만들기 위함
                // 코드 관리도 좀더 나아짐
                // session이 시작 되는 시점이 엔진 외부로 노출 되는 게
                // 좀 말이 안되기도 함
                // 그리고 결론적으로 Session은 엔진 외부에서 만드는 게 맞음

                // 여기서 새로운 세션 객체를 생성함
                Session session = _sessionFactory.Invoke(); // 어떤 종류의 세션을 만들지 결정하고, 그 인스턴스를 생성
                
                // 생성된 Session에게 연결된 클라이언트 소켓을 넘겨준다.
                session.Start(args.AcceptSocket); // 소켓 연결 및 리시브 등록

                // 클라이언트가 접속했을 때 실행할 동작을 호출 (예: GameSession의 OnConnected)
                session.OnConnected(args.AcceptSocket.RemoteEndPoint);
                // 실제 클라이언트가 연결된 후 첫 번째 작업(예: 환영 메시지 전송, 로그 기록 등)을 수행하도록 한다.

                // 참고로 Invoke() 함수란?
                // Invoke()는 델리게이트(delegate) 또는 Func / Action 타입에서 내부에 등록된 함수를 "실행"하는 함수.
                // 이건 결국 다음과 똑같다.
                // Session session = new GameSession();
                // 단지 “함수를 직접 호출하지 않고, 함수 포인터(delegate)를 통해 호출”한 것.
                //  왜 이렇게 쓰냐?
                // - 코드를 유연하게 만들기 위해서!
                // Listener 입장에선 어떤 세션을 생성할지 몰라도,
                // 나중에 외부에서 함수만 등록해주면 알아서 생성 가능하게 됨.
            }
            else
            {
                System.Console.WriteLine(args.SocketError.ToString());
            }
            // 다음 클라이언트 연결을 위해 재등록
            RegisterAccept(args);
        }
    }
}