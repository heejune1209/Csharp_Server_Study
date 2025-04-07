using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ServerCore
{
    abstract class Session
    {
        Socket _socket;
        Queue<byte[]> _sendQueue = new Queue<byte[]>();
        List<ArraySegment<byte>> _pendingList = new List<ArraySegment<byte>>();
        SocketAsyncEventArgs _sendArgs = new SocketAsyncEventArgs();
        SocketAsyncEventArgs _recvArgs = new SocketAsyncEventArgs();
        int _disconnected = 0;
        object _lock = new object();

        // 클라가 접속했을 때 호출
        // Endpoint를 받는 이유 : 접속한 클라의 IP주소를 알기 위해서
        public abstract void OnConnected(EndPoint endPoint);
        // 클라쪽에서 패킷을 보낸 것을 받았을 때 호출
        public abstract void OnRecv(ArraySegment<byte> buffer);
        // 클라쪽에 패킷을 보냈을 때
        public abstract void OnSend(int numOfBytes);
        // 클라와 접속이 끊겼을 때
        public abstract void OnDisconnected(EndPoint endPoint);
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
            lock (_lock)
            {
                _sendQueue.Enqueue(sendBuff);

                if (_pendingList.Count == 0)
                {
                    RegisterSend();
                }
            }
        }

        void RegisterSend()
        {
            while (_sendQueue.Count > 0)
            {
                byte[] buff = _sendQueue.Dequeue();
                _pendingList.Add(new ArraySegment<byte>(buff, 0, buff.Length));
            }
            _sendArgs.BufferList = _pendingList;

            bool pending = _socket.SendAsync(_sendArgs);

            if (pending == false)
            {
                OnSendComplected(null, _sendArgs);
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
                        _sendArgs.BufferList = null;
                        _pendingList.Clear();
                        OnSend(_sendArgs.BytesTransferred);
                        if (_sendQueue.Count > 0)
                        {
                            RegisterSend();
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
                    OnRecv(new ArraySegment<byte>(args.Buffer, args.Offset, args.BytesTransferred));
                    RegisterRecv();
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
            OnDisconnected(_socket.RemoteEndPoint);
            _socket.Shutdown(SocketShutdown.Both);
            _socket.Close();
        }
    }
}