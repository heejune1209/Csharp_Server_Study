using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ServerCore
{
    // abstract이기 때문에 Session의 abstract 메서드를 구현 할 필요가 없다.
    public abstract class PacketSession : Session
    {
        // readonly 키워드는 해당 필드가 한 번 초기화된 후(생성자나 선언 시)
        // 변경될 수 없음을 보장하는 기능을 합니다. const와 유사한 기능.
        // 하지만 const는 컴파일 타임에 결정되는 상수고, readonly는 런타임에 결정되는 상수다.
        public static readonly int HeaderSize = 2;

        // sealed 키워드를 붙여주면 봉인 효과
        // 다른 클래스가 PacketSession을 상속 받은 다음에
        // OnRecv를 override 하려고 하면 error가 나온다.
        // PacketSession에서 OnRecv를 할 때는 여기서 parsing을 해줌

        // 정리
        // OnRecv는 OnRecvComplected에서 호출이 된다.
        // 즉 일단 클라쪽에서 패킷을 보내고 조금의 패킷이라도 일단 도착을 하면 호출이 되는 메서드다.
        // 따라서 도착한 패킷을 Parsing 해주는 프로세스로 이해하면 좋다.
        public sealed override int OnRecv(ArraySegment<byte> buffer)
        {
            int processLength = 0;

            // 상대방이 보낸 패킷의 내용물 예시
            // [size(2)][packetId(2)][....][size(2)][packetId(2)]... 반복
            // 앞에 size(2)가 왔는지 먼저 확인
            // 가장 앞서 도착한 패킷의 사이즈를 통해 1개의 패킷을 처리하고 이것을 계속 반복
            while (true)
            {
                // 최소한 헤더를 파싱할 수 있는지 확인 => 헤더 부분이 올 때까지 대기
                if (buffer.Count < HeaderSize)
                    break;

                // 패킷이 완전체로 도착했는지 확인
                // ToUInt16 : byte array에서 원하는 시작 부분에서부터 2바이트(16비트) 데이터를 ushort로 전환
                ushort dataSize = BitConverter.ToUInt16(buffer.Array, buffer.Offset);

                // 패킷이 완전체가 아니라 부분적으로 왔다는 의미
                // buffer.Count가 패킷의 사이즈보다 커야 완전히 패킷이 도착했다는 의미
                if (buffer.Count < dataSize)
                    break;

                // 여기서 패킷을 조합
                // 1개의 패킷의 유효 범위를 전달해서 처리
                OnRecvPacket(new ArraySegment<byte>(buffer.Array, buffer.Offset, dataSize));
                // buffer.Slice()를 통해 컷팅을 하는 것도 가능하다 
                // => new ArraySegment가 가독성이 좀 더 좋고 struct라서 힙에 할당 되지도 않음 

                // 패킷 처리를 진행해야 할 전체 패킷의 크기를 저장
                processLength += dataSize;

                // 전체 패킷에서 하나의 패킷을 제거
                // buffer.Offset + dataSize : 1개의 패킷을 제거 한 다음부터 시작
                // buffer.Count - dataSize : 1개의 패킷을 제거한 다음 전체 크기
                buffer = new ArraySegment<byte>(buffer.Array, buffer.Offset + dataSize, buffer.Count - dataSize);
            }

            // break가 될 때까지 처리한 바이트의 수
            return processLength;
        }

        // PacketSession에서는 OnRecvPacket을 통해 받도록 설계
        // 실제 OnRecvPacket를 구현할 때 OnRecv를 사용해서 처리할 예정
        // ArraySegment<byte> buffer : 패킷에 해당하는 영역을 다시 집어주기 위함
        // 1개의 완전한 패킷이 도착했을 때 어떤 처리를 할지를 구현
        public abstract void OnRecvPacket(ArraySegment<byte> buffer);

        // 이제 PacketSession을 받아서 사용을 하면 Parsing 하는 부분은 내부에서 알아서 작업을 해줄 거고
        // 실제로 OnRecvPacket()의 매개변수를 통해 유효범위만 집어서 넘어오게 될텐데
        // 그것을 컨텐츠 단에서 packet id를 추출해서 switch case 문으로 작업을 할 예정
    }


    public abstract class Session
    {
        Socket _socket;

        //_recvBuffer가 호출되는 시점?
        // Register를 할 때!
        RecvBuffer _recvBuffer = new RecvBuffer(1024);

        Queue<ArraySegment<byte>> _sendQueue = new Queue<ArraySegment<byte>>();
        List<ArraySegment<byte>> _pendingList = new List<ArraySegment<byte>>();
        SocketAsyncEventArgs _sendArgs = new SocketAsyncEventArgs();
        SocketAsyncEventArgs _recvArgs = new SocketAsyncEventArgs();
        int _disconnected = 0;
        object _lock = new object();

        // 연결이 되었을 때 호출
        public abstract void OnConnected(EndPoint endPoint);
        // 클라에서 보낸 패킷을 받았을 때 호출
        public abstract int OnRecv(ArraySegment<byte> buffer);
        // 클라쪽에 패킷을 보냈을 때 호출
        public abstract void OnSend(int numOfBytes);
        // 연결이 끊겼을 때 호출
        public abstract void OnDisconnected(EndPoint endPoint);

        // 호출 및 수신 준비 등록
        public void Start(Socket socket)
        {
            _socket = socket;
            _recvArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnRecvComplected);
            _sendArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnSendComplected);

            RegisterRecv();
        }

        // 외부에서 전송할 데이터가 전달되면,
        // 이 데이터는 ArraySegment<byte> 형태로 _sendQueue에 enqueue 된다.
        // 만약 현재 _pendingList에 보낼 데이터가 없다면 (즉, 현재 진행 중인 Send 작업이 없으면),
        // RegisterSend()를 호출해 전송 작업을 시작한다.
        // 동시에 여러 전송 요청이 들어올 수 있으므로, _lock으로 동기화한다.
        public void Send(ArraySegment<byte> sendBuff)
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

        // 전송할 데이터 준비
        // 이 메서드는 _sendQueue에 들어있는 각 전송 요청들을 모두 꺼내서,_pendingList에 추가한다.
        // _pendingList는 보내야 할 데이터를 하나의 목록으로 묶어서 보내기 위한 배열 리스트다.

        // 이후, _sendArgs.BufferList에 이 _pendingList를 지정한 후,
        // _socket.SendAsync(_sendArgs) 를 호출하여 비동기 전송 작업을 시작한다.
        // 만약 SendAsync()가 즉시 완료된다면(반환값이 false), 직접 OnSendComplected()를 호출한다
        void RegisterSend()
        {
            while (_sendQueue.Count > 0)
            {
                ArraySegment<byte> buff = _sendQueue.Dequeue();
                _pendingList.Add(buff);
            }
            _sendArgs.BufferList = _pendingList;

            // 비동기 전송 작업을 시작
            bool pending = _socket.SendAsync(_sendArgs);

            if (pending == false)
            {
                OnSendComplected(null, _sendArgs);
            }
        }

        // 전송 완료 후 처리
        // 전송 작업이 완료되면 이 콜백이 호출된다.
        // 전송이 성공적으로 완료되었으면(전송 바이트 수가 0보다 크고 에러 없음),
        // _pendingList를 초기화하고, 전송 완료를 알리는 OnSend() 메서드를 호출한다.
        // 만약 전송 대기 큐 _sendQueue에 추가된 데이터가 남아 있으면,
        // 다시 RegisterSend()를 호출해서 연속 전송을 처리한다.
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

        // 비동기 수신 등록
        private void RegisterRecv()
        {
            // 버퍼의 읽기/쓰기 위치를 초기화
            _recvBuffer.Clean();
            // 현재 유효한 범위를 집어줘야 한다.
            // WriteSegment에서 현재 쓸 수 있는 유효 범위를 알아온다
            ArraySegment<byte> segment = _recvBuffer.WriteSegment;

            // 데이터를 받을 버퍼 설정
            _recvArgs.SetBuffer(segment.Array, segment.Offset, segment.Count);

            // 비동기 수신 작업을 OS에 요청
            bool pending = _socket.ReceiveAsync(_recvArgs);

            if (pending == false)
            {
                OnRecvComplected(null, _recvArgs);
            }
        }

        // 수신이 완료되면, 이 콜백 함수가 호출된다.
        private void OnRecvComplected(object sender, SocketAsyncEventArgs args)
        {
            // 수신된 데이터가 성공적으로 전송되었는지 체크
            if (args.BytesTransferred > 0 && args.SocketError == SocketError.Success)
            {
                try
                {
                    // Write 커서 이동
                    // BytesTransferred가 현재 수신 받은 byte의 크기
                    // 즉 수신 받은 데이터 크기 만큼 write 커서를 이동
                    if (_recvBuffer.OnWrite(args.BytesTransferred) == false)
                    {
                        Disconnect();
                        return;
                    }

                    // recvBuffer의 읽기 영역(ReadSegment)을 얻어, 추상 메서드 OnRecv()를 호출해
                    // 실제로 컨텐츠 단(예, 패킷 파싱 및 처리)에서 데이터를 처리하게 한다.
                    // OnRecv()는 처리한 바이트 수를 반환
                    // 이 값만큼 _recvBuffer의 읽기 커서를 이동시키는 OnRead()를 호출한다.

                    // 컨텐츠 쪽으로 데이터를 넘겨주고 얼마나 처리 했는지 받는다.
                    // 컨텐츠 쪽으로 데이터를 넘겨주는 작업 => OnRecv
                    // 현재 유효 범위 즉 처리할 수 있는 데이터 범위 만큼을 컨텐츠 쪽에 넘겨줘야 한다.
                    // 컨텐츠 쪽에서 패킷을 다 처리하지 못할 경우
                    // 얼마만큼의 데이터를 처리했는지를 받아옴
                    int processLength = OnRecv(_recvBuffer.ReadSegment);

                    // processLength < 0 => 컨텐츠 단에서 코드를 잘못 입력해서 이상한 값을 넣어줬는지 체크
                    // recvBuffer.DataSize보다 더 많이 처리 했다 => 처리할 데이터 양보다 많다는 말도 안되는 상황
                    if (processLength < 0 || _recvBuffer.DataSize < processLength)
                    {
                        Disconnect();
                        return;
                    }
                    // 처리한 데이터만큼 _recvBuffer의 읽기 커서를 이동시키는 Read 커서 이동
                    // 근데 반환한 값이 false라면 연결 끊음
                    if (_recvBuffer.OnRead(processLength) == false)
                    {
                        Disconnect();
                        return;
                    }

                    // 위의 프로세스 정리
                    // 1. 수신 받은 데이터 크기 많큼 Write 커서 이동 
                    // 2. 컨텐츠 단에서 패킷을 처리 
                    // 3. 처리한 데이터 만큼 Read 커서 이동

                    // 버퍼를 정리하고 또 패킷을 받을 준비
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