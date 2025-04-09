using System;
using System.Threading;

namespace ServerCore
{
    // SendBuffer와 SendBufferHelper 의 역할 요약
    // "보낼 데이터를 잠깐 저장해두고, 필요한 만큼 잘라서 보낸다"

    // 주요 기능
    // - 큰 메모리 덩어리(Chunk)를 만들어 놓고
    // - 여러 번 나눠서 보낼 수 있게 Open() / Close() API 제공
    // - ThreadLocal을 통해 각 스레드마다 고유한 버퍼를 사용하게 하여 멀티스레드 충돌 방지
    // - 한 번 쓰면 재사용하지 않고, 다음 부분을 계속 이어서 쓰는 구조

    public class SendBufferHelper
    {
        // 한번 만든 후 고갈 될때까지 사용할 예정이기 때문에
        // 전역으로 만들면 편할 것 같다.
        // 하지만 멀티 쓰레드 환경이기 때문에 전역으로 만들면 경합이 발생 할 수 있다.
        // ThreadLocal로 만들게 되면 전역이지만 나의 Thread에서만 고유하게 사용할 수 있는 전역 변수
        // SendBuffer에 대해서 경합이 발생되는 것을 방지
        // ChunkSize만큼 Buffer를 만들어 놓은 다음에
        // 조금씩 쪼개서 사용하겠다는 개념
        public static ThreadLocal<SendBuffer> CurrentBuffer = new ThreadLocal<SendBuffer>(() =>
        {
            // ThreadLocal를 생성할 때 무엇을 할지 결정
            return null;
        });
        // 외부에서 ChunkSize를 조정하고 싶을 때 사용
        public static int ChunkSize { get; set; } = 4096 * 100;

        // ThreadLocal에 있는 CurrentBuffer를 관리해줘야 한다.
        public static ArraySegment<byte> Open(int reserveSize)
        {
            // 아직 SendBuffer를 한번도 사용하지 않는 상태
            if (CurrentBuffer.Value == null)
            {
                CurrentBuffer.Value = new SendBuffer(ChunkSize);
            }

            // 현재 SendBuffer에서 사용 가능한 사이즈가 예약하기를 원하는 사이즈보다 작을 때
            // SendBuffer가 고갈 되었다는 의미므로
            // 새로운 SendBuffer를 만들어줘야 한다.
            if (CurrentBuffer.Value.FreeSize < reserveSize)
            {
                CurrentBuffer.Value = new SendBuffer(ChunkSize);
            }

            // if 문을 모두 타지 않는다 => 현재 buffer에 공간이 남아 있다.
            return CurrentBuffer.Value.Open(reserveSize);
        }
        public static ArraySegment<byte> Close(int usedSize)
        {
            return CurrentBuffer.Value.Close(usedSize);
        }
    }



    // RecvBuffer의 경우 Session에 고유의 RecvBuffer를 만들면 되었다.
    // 즉 Session에서 클라로부터 받은 패킷을 처리해야하기 때문에 Session과 RecvBuffer는 1:1 대응 관계로 볼 수 있다.
    // 하지만 SendBuffer의 경우 Session 밖에서 만들어줘야 하기 때문에 
    // 다소 복잡해진다.

    // Recv와 달리 usedSize의 위치를 다시 처음으로 옮기지 않는 이유
    // => 사용하다가 다시 usedSize를 옮겨서 반환하기가 어려운 이유는
    // 애당초 Send를 할 때 1명에게 보내는 것이 아니라 여러 명한테 보낼 수 있다.
    // 그렇기 때문에 SendBuffer에서 사용을 끝냈지만 이전에 있던 부분(현재 사용 중인 부분)을 
    // 다른 세선에서 있는 Queue에다가 보관을 하고 있는 상태일 수도 있기 때문에 
    // usedSize의 위치를 다시 처음으로 돌려놓기가 어렵다.
    // 즉, 누군가가 계속 이전에 있던 부분(현재 사용 중인 부분)을 참조하고 있을 수도 있기 때문.
    // 따라서 SendBuffer는 1회용으로 사용할 예정 
    // => 예약된 크기만큼 만들어진 SendBuffer가 고갈될 때까지 사용할 예정
    public class SendBuffer
    {
        // [u][ ][ ][ ][ ][ ][ ][ ][ ][ ]
        // 내가 얼마만큼 사용했는지가 가장 왼쪽부터 표시 됨
        // 사용할 때마다 우측으로 이동
        byte[] _buffer;

        // chunkSize ? => chunk가 큰 뭉태기라는 의미
        // SendBuffer가 작은 크기가 아니라 큰 덩어리라고 알리기 위함
        public SendBuffer(int chunkSize)
        {
            _buffer = new byte[chunkSize];
        }
        // 얼마만큼 사용했는지
        // RecvBuffer에서 writePos에 해당
        int _usedSize = 0;

        // 현재 사용 가능한 공간
        // SendBuffer 크기 - 사용한 버퍼 크기
        public int FreeSize
        {
            get
            {
                return _buffer.Length - _usedSize;
            }
        }
        // 현재 상태에서 Buffer를 사용하겠다고 Open을 한 상태에서
        // 얼마만큼의 사이즈를 최대치로 사용할지를 결정 
        // => 실제 사용한 사이즈가 아니라 예약 한 사이즈 임을 주의
        // reserveSize : 예약 공간
        public ArraySegment<byte> Open(int reserveSize)
        {
            // 현재 예약공간보다 남은 공간이 작다면 Buffer가 고갈된 상태
            if (reserveSize > FreeSize)
            {
                return null;
            }
            // 시작 위치가 _usedSize ? 
            // => 지금까지 사용한 공간에서부터 Buffer를 사용하겠다고 Open을 해야하기 때문에
            return new ArraySegment<byte>(_buffer, _usedSize, reserveSize);
        }

        // 다쓴 다음에 사용한 사이즈를 넣어서 Close를 해준다
        // Close를 해줬다는 것은 얼마만큼 사용했다는 것을 확정짓는 것이기 때문에
        // 이를 통해 최종적으로 사용한 버퍼를 반환한다.
        public ArraySegment<byte> Close(int usedSize)
        {
            // 유효 범위 확인
            // _usedSize : 현재 사용한 위치부터 시작
            // usedSize : 버퍼의 크기. 즉, 사용할 크기만큼 buffer를 확보
            ArraySegment<byte> segment = new ArraySegment<byte>(_buffer, _usedSize, usedSize);

            // 사용한 크기만큼 늘려줌
            _usedSize += usedSize;

            // 실제로 사용한 범위를 전달
            return segment;
        }
    }
}