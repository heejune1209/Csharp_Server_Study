using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ServerCore
{
    class Session
    {
        Socket _socket;
        int _disconnected = 0;
        public void Start(Socket socket)
        {
            _socket = socket;
            SocketAsyncEventArgs recvArgs = new SocketAsyncEventArgs();
            recvArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnRecvComplected);

            // 유저에 대한 정보를 아무거나 전달해줘도 된다. => 식별자로 구분하고 싶거나 연동하고 싶을 때 사용
            // recvArgs.UserToken = 1; // 아니면 이 세션에서 온 애라는 뜻으로 this를 넣어줘도 된다.
            // 이건 Listener 전용일 때 사용
            // recvArgs.AcceptSocket

            // recv를 할 때 buffer를 만들어줬던 것처럼 아래와 같이 SetBuffer를 해야한다.
            // offset이 0이 아닌 경우도 있음. -> 버퍼에 큰 값을 할당해오고 세션별로 쪼개서 가지고 가는 경우가 있기 때문
            // 하지만 지금은 세션을 만들때마다 버퍼를 새로 만들어주기 때문에 offset은 0으로 해도 된다.
            recvArgs.SetBuffer(new byte[1024], 0, 1024); // 버퍼, 버퍼를 시작하는 위치, 버퍼의 길이

            // Receive 시작
            RegisterRecv(recvArgs);
        }

        public void Send(byte[] sendBuff)
        {
            _socket.Send(sendBuff);
        }
        // 동시다발적으로 Disconnect를 하는 경우 
        // Disconnect를 같은 사람이 2번 하게 된다면?
        // 오류가 발생
        // Disconnect를 한번만 하도록 작업
        public void Disconnect()
        {
            // if (_socket != null)
            // 이렇게 _socket이 null이 아닐 경우에 Close를 하는 방법은 정상적으로 동작하지 않는다.
            
            // Exchange()의 리턴 값은 ref 값이 바뀌기 전의 값이다.
            // _disconnected == 1 => Disconnect가 완료 되었다(이미 ref값이 1이였다).
            // 리턴 값이 1이라면 기존에 다른 쓰레드에서 1로 바꿔줬다는 의미
            // 그래서 return
            // 즉 Disconnect를 2번 하게 될 때
            // 이미 한번 Disconnect가 되었기 때문에 
            // 둘 중에 하나는 if문에 걸려서 동작을 하지 않게 된다.
            if (Interlocked.Exchange(ref _disconnected, 1) == 1)
                return;
            _socket.Shutdown(SocketShutdown.Both);
            _socket.Close();
        }

        #region 네트워크 통신

        // 비동기 방식으로 처리하기 위해서 2단계로 진행을 함
        // 등록하는 작업, 완료 후 작업
        private void RegisterRecv(SocketAsyncEventArgs args)
        {
            bool pending = _socket.ReceiveAsync(args);

            // 바로 정보를 받아온 경우
            if (pending == false)
            {
                OnRecvComplected(null, args);
            }
            // 만약 pending이 true면 위에 EventHandler를 통해서 OnRecvComplected가 자동으로 콜백으로 실행된다.
        }
        private void OnRecvComplected(object sender, SocketAsyncEventArgs args)
        {
            // args.BytesTransferred(전달 받은 byte 값) ==  0일 경우가 있다 => 상대방이 접속을 끊는 경우에
            // 따라서 반드시 0보다 큰 값인지를 체크해야한다. 
            // 소켓 에러 메세지도 성공인지 확인
            if (args.BytesTransferred > 0 && args.SocketError == SocketError.Success)
            {
                try
                {
                    // To do
                    // 나중에는 패킷을 분석하는 로직
                    string recvData = Encoding.UTF8.GetString(args.Buffer, args.Offset, args.BytesTransferred);
                    Console.WriteLine($"[From Client : {recvData}");

                    RegisterRecv(args);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"OnRecvComplected Failed {e}");
                }

            }
            // 실패한 경우 
            else
            {
                // To do Disconnet
            }
        }
        #endregion
        // 중요한 흐름
        // 한번은 받는 동작이 끝나야 다음 등록이 진행이 된다.
        // 클라에서 보낸 패킷을 모두 받기 전까지는 더이상 받지 않겠다는 로직
        // 하지만 Recv는 간단한데 Send는 간단하지 않다.
        // 일단 Recv 예약을 건 다음에 실제로 클라이언트가 보낸 메시지를 처리하면 되기 때문에 간단
        // 하지만 Send는 예약을 하는 게 아니라(Recv를 대기 하는 게 아니라) 원하는 타이밍에 보내야 하기 때문에 
        // 많이 까다롭다.
    }
}