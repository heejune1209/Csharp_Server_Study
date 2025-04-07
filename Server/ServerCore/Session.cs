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
        Queue<byte[]> _sendQueue = new Queue<byte[]>();
        List<ArraySegment<byte>> _pendingList = new List<ArraySegment<byte>>();
        // _pending 대신 리스트로 pendinglist를 만들어서 사용
        SocketAsyncEventArgs _sendArgs = new SocketAsyncEventArgs();
        SocketAsyncEventArgs _recvArgs = new SocketAsyncEventArgs(); // 리시브도 이렇게 send처럼 멤버 변수로 옮겨도 상관없다
        int _disconnected = 0;
        object _lock = new object();

        public void Start(Socket socket)
        {
            _socket = socket;
            _recvArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnRecvComplected);
            _sendArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnSendComplected);

            _recvArgs.SetBuffer(new byte[1024], 0, 1024);
            RegisterRecv();
        }

        public void Send(byte[] sendBuff)
        {
            // lock을 잡아서 한번에 한명씩만 진입 할 수 있도록 해준다.
            lock (_lock)
            {
                _sendQueue.Enqueue(sendBuff);

                // _pendingList.Count == 0 => 현재 대기 중 얘가 한 명도 없다는 의미
                if (_pendingList.Count == 0)
                {
                    RegisterSend();
                }
            }
        }

        void RegisterSend()
        {
            // 이전 코드에선 _pending을 이용해서 비동기 작업이 순차적으로 처리하고,
            // 중복된 비동기 작업으로 인한 데이터 손실이나 경합 조건을 방지했었고,
            // 이런식으로 Dequeue를 한번씩 해가지고 send 큐에 있는 정보 하나당 send async를 한번씩 해주고 있었는데
            // 이것보다 더 나은 인터페이스가 있다.
            /*
            _pending = true;
            byte[] buff = _sendQueue.Dequeue();
            _sendArgs.SetBuffer(buff, 0, buff.Length);

            // SendAsync()의 반환값 pending은 운영체제가 실제로 비동기 작업이 걸렸는지 여부를 나타냄.
            bool pending = _socket.SendAsync(_sendArgs);
            if (pending == false)
            {
                OnSendComplected(null, _sendArgs);
            }
            */

            // 보낼 정보를 리스트로 보관하고 있다가 SendAsync를 하면 한번에 발송
            // SetBuffer와 SocketAsyncEventArgs.BufferList 둘중 하나만 사용해야 한다.
            while (_sendQueue.Count > 0)
            {
                byte[] buff = _sendQueue.Dequeue();
                // ArraySegment => Array의 일부(특정 배열의 일부로 구성되어 있음)
                // 0 : 어디서부터 시작
                // buff.Length : 몇개를 넣을 건지 결정
                _pendingList.Add(new ArraySegment<byte>(buff, 0, buff.Length));
                // ArraySegment는 구조체이기 때문에 힙 영역에 할당되는 애가 아니라 스택에 할당이 되어서
                // 실제로 add를 할 때는 여기다가 값이 복사되는 그런 형태로 작동을 한다.

                // 그리고 c++같은 경우엔 배열은 포인터의 개념이 있으니까 한 지점부터 3개를 넘겨주고 싶다고 하면 포인터를 통해서 주소를 넘겨줄수 있는데
                // c#은 포인터를 사용할 수 없으니까 무조건 첫 주소만 알수 있다.
            }
            _sendArgs.BufferList = _pendingList;

            // 메시지(BufferList)를 실제로 전송하는 코드
            // 반환값으로 pending을 받는데,
            // 이 값이 true면 아직 비동기 작업 진행 중.(전송은 완료되지 않았고, 나중에 args.Completed 이벤트로 콜백됨)
            // false면 전송이 즉시 완료됨 → 바로 OnSendComplected() 호출해줘야 함
            bool pending = _socket.SendAsync(_sendArgs);

            if (pending == false)
            {
                OnSendComplected(null, _sendArgs); // 수동 콜백
            }
        }


        private void OnSendComplected(object send, SocketAsyncEventArgs args)
        {
            lock (_lock)
            {
                if (args.BytesTransferred > 0 && args.SocketError == SocketError.Success)
                {
                    try
                    {
                        // BufferList가 굳이 리스트를 가지고 있지 않아도 되기 때문에
                        // OnSendComplected는 결국 Send가 완료되고 동작되기 때문에 
                        // 이렇게 초기화를 시켜줄 필요가 있다.
                        _sendArgs.BufferList = null;
                        _pendingList.Clear();
                        // Clear()를 안 해주면, 이전에 보냈던 ArraySegment<byte> 목록이
                        // 다음 전송에도 남아서 중복 전송될 수 있기 때문이야.
                        // 즉, SendAsync가 끝났는데 _pendingList를 그대로 놔두면,
                        // 같은 데이터가 또 전송되는 상황이 발생할 수 있어! ⚠️

                        // 몇 바이트를 보냈는지를 추적
                        Console.WriteLine($"Transferred bytes : {_sendArgs.BytesTransferred}");
                        if (_sendQueue.Count > 0)
                        {
                            RegisterSend();
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"OnSendComplected Failed {e}");
                    }

                }
                else
                {
                    Disconnect();
                }
            }

        }

        #region 네트워크 통신
        private void RegisterRecv()
        {
            bool pending = _socket.ReceiveAsync(_recvArgs);

            if (pending == false)
            {
                OnRecvComplected(null, _recvArgs);
            }
        }

        private void OnRecvComplected(object sender, SocketAsyncEventArgs args)
        {
            if (args.BytesTransferred > 0 && args.SocketError == SocketError.Success)
            {
                try
                {
                    string recvData = Encoding.UTF8.GetString(args.Buffer, args.Offset, args.BytesTransferred);
                    Console.WriteLine($"[From Client : {recvData}");

                    RegisterRecv();
                }
                catch (System.Exception e)
                {
                    Console.WriteLine($"OnRecvComplected Failed {e}");
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