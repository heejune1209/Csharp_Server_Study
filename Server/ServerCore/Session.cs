using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ServerCore
{
    class Session
    {
        Socket _socket;
        int _disconnected = 0;
        object _lock = new object();
        // send를 보낼 때 쌓아놓고 한번에 보내는 용도
        Queue<byte[]> _sendQueue = new Queue<byte[]>();

        // 일단 한번 Register를 했다면 Send가 완료될 때까지 대기를 할 수 있도록 해줌
        // 누군가가 Send를 하고 있다면 Register를 하는 것이 아니라 Queue에다가 sendbuff를 쌓아 놓게 됨
        bool _pending = false;
        SocketAsyncEventArgs _sendArgs = new SocketAsyncEventArgs();

        public void Start(Socket socket)
        {
            _socket = socket;
            SocketAsyncEventArgs recvArgs = new SocketAsyncEventArgs();
            recvArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnRecvComplected);
            recvArgs.SetBuffer(new byte[1024], 0, 1024);
            _sendArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnSendComplected);

            RegisterRecv(recvArgs);
        }

        public void Send(byte[] sendBuff)
        {  
            // lock을 잡아서 한번에 한명씩만 진입 할 수 있도록 해준다.
            lock (_lock)
            {
                _sendQueue.Enqueue(sendBuff);
                // 아직 아무도 send를 하지 않은 상태
                // send큐에 sendBuff를 넣어준 다음 pending이 false인 상태라면(첫번째라면) RegisterSend 호출
                if (_pending == false) // true면 누군가는 send를 하고 있는 상태
                {                                       
                    RegisterSend();
                }
            }
        }

        // recv 같은 경우는 처음에 예약을 걸어놓고 클라이언트에서 요청이 들어올 때까지 대기를 타면 됐지만
        // send는 처음에 예약을 거는 것 자체가 말이 되지가 않는다 => 무엇을 보낼지 어떻게 알고 예약을 걸 수 있을까?
        // Send에서 이미 lock 처리를 했기 때문에 굳이 여기서는 lock처리를 할 필요가 없다.
        void RegisterSend()
        {
            _pending = true; 
            byte[] buff = _sendQueue.Dequeue();
            _sendArgs.SetBuffer(buff, 0, buff.Length);

            bool pending = _socket.SendAsync(_sendArgs);
            if (pending == false)
            {
                OnSendComplected(null, _sendArgs);
            }
        }
        
        // 하지만 여기서는 RegisterSend에서 호출될 수도 있지만 이벤트 콜백으로 호출이 될 수도 있기 때문에
        // lock을 걸어줘야 한다.
        private void OnSendComplected(object send, SocketAsyncEventArgs args)
        {
            lock(_lock)
            {
                if (args.BytesTransferred > 0 && args.SocketError == SocketError.Success)
                {
                    try
                    {
                        // 그 동안 쌓아놓은 Queue가 있다.
                        // => 누군가가 send를 하는 동안 sendQueue에 buff를 저장해놓고 있었다.
                        // 그것을 가지고 와서 Dequeue로 처리를 해준다는 이야기
                        if (_sendQueue.Count > 0)
                        {
                            RegisterSend();
                        }
                        // 아무도 그 사이에 sendQueue에 패킷을 추가하지 않았다는 이야기
                        else
                        {
                            // Send를 완료했기 때문에 또 다른 Send 등록을 받기 위해서 false로 변경
                            _pending = false;
                        }
                    }
                    catch (Exception e)
                    {
                        System.Console.WriteLine($"OnSendComplected Failed {e}");
                    }

                }
                else
                {
                    Disconnect();
                }   
            }
            
        }
        // 멀티쓰레드 프로그래밍은 계속 충돌을 내면서 연습을 하는 것이 가장 좋다.

        #region 네트워크 통신
        private void RegisterRecv(SocketAsyncEventArgs args)
        {
            bool pending = _socket.ReceiveAsync(args);

            if (pending == false)
            {
                OnRecvComplected(null, args);
            }
        }

        private void OnRecvComplected(object sender, SocketAsyncEventArgs args)
        {
            if (args.BytesTransferred > 0 && args.SocketError == SocketError.Success)
            {
                try
                {
                    string recvData = Encoding.UTF8.GetString(args.Buffer, args.Offset, args.BytesTransferred);
                    System.Console.WriteLine($"[From Client : {recvData}");

                    RegisterRecv(args);
                }
                catch (System.Exception e)
                {
                    System.Console.WriteLine($"OnRecvComplected Failed {e}");
                }

            }
            else
            {
                Disconnect();
            }
        }
        #endregion
        
        public void Disconnect()
        {
            if (Interlocked.Exchange(ref _disconnected, 1) == 1)
                return;
            _socket.Shutdown(SocketShutdown.Both);
            _socket.Close();
        }
    }
}