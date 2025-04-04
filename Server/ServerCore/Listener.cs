using System;
using System.Net;
using System.Net.Sockets;

namespace ServerCore
{
    class Listener
    {
        // 문지기
        Socket _listenSoket;

        Action<Socket> _OnAcceptHandler;

        public void Init(IPEndPoint endPoint, Action<Socket> OnAcceptHandler)
        {
            _OnAcceptHandler += OnAcceptHandler;
            _listenSoket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            _listenSoket.Bind(endPoint);

            // 영업 시작
            // Backlog는 최대 대기수
            _listenSoket.Listen(10);

            // 현재 문지기를 한명만 고용을 했기 때문에 
            // 너무 많은 사람이 한번에 접속을 하면 다소 느릴 수도 있다
            // 해서 이 부분을 늘려주면 된다.
            // 이렇게 직원을 늘려주면 비동기로 직원들이 Receive 처리를 하게 된다.
            // 낚시대를 여러개로 만들어도 상관없음
            for (int i = 0; i < 10; i++)
            {
                SocketAsyncEventArgs args = new SocketAsyncEventArgs();
                args.Completed += new EventHandler<SocketAsyncEventArgs>(OnAcceptCompleted);
                RegisterAccept(args);
            }
        }

        // 등록을 예약
        private void RegisterAccept(SocketAsyncEventArgs args)
        {
            args.AcceptSocket = null;

            bool pending = _listenSoket.AcceptAsync(args);

            // 의문점
            // 만일 계속 pending이 false가 되어서 
            // OnAcceptCompleted를 호출하고 다시 RegisterAccept를 호출하는 식으로
            // 무한 반복이 되면 어떻게 될까? => 실제로 무한 반복이 일어나면 stack over flow가 발생함
            // 우선 Backlog를 10명을 걸어놔서 현실적으로 계속 stack over flow가 발생하지 않고 
            // 그리고 pending이 계속 false가 되는 상황은 현실적으로 잘 일어나지는 않는다.
            // 그래도 우려가 되면 수정을 하긴 해야 함
            if (pending == false)
            {
                OnAcceptCompleted(null, args);
            }
        }

        private void OnAcceptCompleted(object sender, SocketAsyncEventArgs args)
        {
            if (args.SocketError == SocketError.Success)
            {
                _OnAcceptHandler.Invoke(args.AcceptSocket);
            }
            else
            {
                Console.WriteLine(args.SocketError.ToString());
            }

            RegisterAccept(args);
        }
    }
}