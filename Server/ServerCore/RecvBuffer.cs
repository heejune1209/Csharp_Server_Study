using System;
using System.Collections.Generic;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ServerCore
{
    // 일반적으로 따로 RecvBuffer를 빼지 않고
    // Session에 꾸역꾸역 몰아 넣는데
    // RecvBuffer를 따로 빼야 버그 수정에 용이하다.
    // 결국 RecvBuffer를 통해 일부분만 Recv를 할 수 있는 기능을 구현

    // RecvBuffer는 비유를 하자면, 물을 받아두는 양동이이고,
    // SendBuffer는 물을 모아서 호스로 내보내는 통이라고 생각하면 된다.

    // 설명을 하자면, RecvBuffer는 소켓에서 들어온 데이터를 일단 임시 저장하고,
    // SendBuffer는 보낼 데이터를 모아놓고 OS에게 전송 요청을 하는 역할을 한다.
    // 핵심은 “중간 저장소”라는 점
    public class RecvBuffer
    {
        // 주요 기능
        // - 바이트 배열을 내부에 가짐 → ArraySegment<byte>
        // - 데이터를 받으면 _writePos만큼 커서가 이동 (쓰기 위치)
        // - 컨텐츠에서 데이터를 처리하면 _readPos만큼 커서 이동 (읽기 위치)
        // - Clean()을 통해 읽고 쓴 위치를 다시 정리
        // - ReadSegment, WriteSegment 으로 읽을 위치/쓸 위치를 제공

        // "받은 데이터를 안전하게 쌓고, 처리하고, 정리한다"

        // lead buffer
        // 10 바이트 배열이라고 가정
        // 5 바이트를 받았을 때
        // 3 바이트 패킷을 모두 처리 했을 때
        // [ ][ ][ ][r][ ][w][ ][ ][ ][ ]
        ArraySegment<byte> _buffer;

        // 마우스 커서라고 생각
        // 읽고 있는 커서
        // 버퍼에서 현재 읽고 있는 위치
        int _readPos;
        // 해당 마우스 커서에서부터 입력이 된다고 이해
        // 버퍼에서 현재 쓰고 있는 위치
        int _writePos;
        public RecvBuffer(int bufferSize)
        {
            _buffer = new ArraySegment<byte>(new byte[bufferSize], 0, bufferSize);
        }
        // 유효범위, 즉 버퍼에 현재 쌓여 있고 읽을 수 있는 데이터 크기
        // write pos - read pos 
        // why? 현재까지 버퍼에 쌓인 값 현재까지 읽어드린 값을 빼면 남은 데이터 크기를 구할 수 있다.
        public int DataSize { get { return _writePos - _readPos; } }

        // 버퍼에 현재 남아 있는 공간
        // 총 버퍼의 크기 - Write Pos 
        // -1을 안해도 되는 이유는 write pos의 처음 시작점이 0이기 때문에
        public int FreeSize { get { return _buffer.Count - _writePos; } }

        // ArraySegment<T>(T[] array, int offset, int count)
        // array  : 원본 배열(전체 버퍼)
        // offset : 잘라내기 시작할 위치(시작 인덱스)
        // count  : 잘라낼 길이(몇 바이트까지)

        // ArraySegment<byte> : byte 배열의 일부를 의미
        // array : byte 배열
        // offset : 어디서부터 시작
        // count : 몇 개를 읽을 것인지

        // DataSegment
        // 유효 범위의 Segment
        // 현재까지 받은 데이터에서 어디서부터 어디까지 읽으면 되는지를 컨텐츠 단에 전달하는 역할
        // ArraySegment => Array의 일부(특정 배열의 일부로 구성되어 있음)

        public ArraySegment<byte> ReadSegment
        {
            get
            {
                // _buffer.Array : 현재 쌓여있는 버퍼
                // _buffer.Offset + _readPos : 쌓여있는 버퍼의 시작 위치 + 현재 읽고 있는 위치
                // => 즉 어디서부터 읽어야 할지 전달
                // DataSize : 버퍼에 현재 쌓여 있고 읽을 수 있는 데이터 크기
                return new ArraySegment<byte>(_buffer.Array, _buffer.Offset + _readPos, DataSize);
                // "버퍼 안에서 _readPos 위치부터 DataSize만큼 잘라서 반환해줘!"라는 뜻
            }
        }

        // RecvSegment
        // 다음에 Recieve를 할 때 어디서부터 어디까지 쓸 수 있는지에 대한 유효 범위
        // 기존에는 전체를 넘겨주고 있었지만 이제는 실제로 쓸 수 있는 범위를 전달하기 위함
        public ArraySegment<byte> WriteSegment
        {
            get
            {
                // _buffer.Array : 현재 쌓여있는 버퍼
                // _buffer.Offset + _writePos : 쌓여있는 버퍼의 시작 위치 + 현재 쓰고 있는 위치
                // => 즉 어디서부터 쓸 수 있는지 전달
                // FreeSize : 현재 버퍼에 남아 있는 공간, 쓸 수 있는 공간이 어디까지인지 전달하기 위함
                return new ArraySegment<byte>(_buffer.Array, _buffer.Offset + _writePos, FreeSize);
            }
        }

        // Buffer를 정리해주는 메서드
        // 정리를 안해주게 되면 buffer의 읽고 쓰는 현재 위치가 계속 뒤로 밀리기 때문에
        // 한번씩 다시 처음으로 읽고 쓰는 위치를 돌려줘야 한다.
        public void Clean()
        {
            // 현재 버퍼에 쌓여 있는 데이터 사이즈
            int dataSize = DataSize;

            // 읽고 쓰는 위치가 정확하게 겹칠 때 => 남은 데이터가 없음
            // 실질적으로 클라에서 보내준 모든 데이터를 다 처리한 상태
            if (dataSize == 0)
            {
                // 남은 데이터가 없으면 복사하지 않고 커서 위치만 리셋
                _readPos = _writePos = 0;
            }
            // 남은 데이터가 있을 때 시작 위치로 복사
            else
            {
                // _buffer.Array : Copy할 소스
                // _buffer.Offset + _readPos : 복사를 시작할 위치
                // _buffer.Array : 붙여 넣을 소스
                // _buffer.Offset : 붙여 넣을 위치 
                // dataSize : 복사할 데이터 크기(현재 남아 있는 데이터)
                Array.Copy(_buffer.Array, _buffer.Offset + _readPos, _buffer.Array, _buffer.Offset, dataSize);
                _readPos = 0;

                // _writePos의 시작 위치가 곧 현재 남아 있는 데이터의 크기와 같으므로
                _writePos = DataSize;
            }
        }

        // Read Pos를 이동시켜주는 함수
        // 컨텐츠에서 데이터를 가공해서 처리를 한 후 처리한 데이터만큼 Read Pos를 이동
        public bool OnRead(int numOfBytes)
        {
            // 읽은 데이터가 현재 읽어야 할 데이터보다 클 때
            // 버그 방어 코드
            if (numOfBytes > DataSize)
                return false;
            // 처리한 데이터 만큼 앞으로 이동
            _readPos += numOfBytes;
            return true;
        }
        public bool OnWrite(int numOfBytes)
        {
            // 쓰는 데이터가 현재 남아 있는 공간보다 클 때 
            // 버그 방어 코드
            if (numOfBytes > FreeSize)
            {
                return false;
            }
            _writePos += numOfBytes;
            return true;
        }
    }
}