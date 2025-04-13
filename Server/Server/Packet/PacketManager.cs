using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Text;
using ServerCore;

namespace Server
{
    // PacketManager는 패킷의 파싱과 핸들링을 자동으로 분배하는 중앙 관리 클래스입니다.
    // 싱글톤(Singleton) 패턴을 사용하여 하나의 인스턴스로 동작하며,
    // 수신된 패킷을 적절한 핸들러로 연결하는 역할을 수행합니다.
    class PacketManager
    {
        #region  Singlton
        static PacketManager _instance;
        public static PacketManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new PacketManager();
                }
                return _instance;
            }
        }
        // PacketManager는 전역에서 하나의 인스턴스로 관리됩니다.
        #endregion

        // 프로토콜 ID(ushort)를 키로 하여
        // "원시 바이트 배열을 받아서 해당 패킷 객체를 생성(파싱)하는" 작업을 수행하는 액션을 저장합니다.
        // 예: PlayerInfoReq가 도착했을 때, MakePacket<PlayerInfoReq> 함수를 호출하도록 매핑합니다.
        // Action<PacketSession, ArraySegment<byte>> : PacketSession, ArraySegment를 인자로 받는 특정 행동
        // 패킷을 생성하는 기능을 보관
        Dictionary<ushort, Action<PacketSession, ArraySegment<byte>>> _onRecv = new Dictionary<ushort, Action<PacketSession, ArraySegment<byte>>>();

        // 프로토콜 ID(ushort)를 키로 하여,
        // "파싱된 패킷 객체(IPacket)를 가지고 실제로 처리하는" 핸들러(예: PacketHandler.PlayerInfoReqHandler)를 저장합니다.
        // Action<PacketSession, Action<PacketSession, IPacket>> : PacketSession, IPacket을 인자로 받는 특정 행동
        Dictionary<ushort, Action<PacketSession, IPacket>> _handler = new Dictionary<ushort, Action<PacketSession, IPacket>>();

        // 최대한 멀티쓰레드가 개입하기 전에 가장 처음에 호출!
        // PacketManager가 사용할 패킷 타입과 해당 처리 함수를 등록합니다.
        public void Register()
        {
            // _onRecv에는 Packet을 “만드는” 함수로, MakePacket<PlayerInfoReq>를 등록합니다.
            _onRecv.Add((ushort)PacketID.PlayerInfoReq, MakePacket<PlayerInfoReq>);
            // _handler에는 파싱된 패킷을 처리할 핸들러로, PacketHandler.PlayerInfoReqHandler를 등록합니다.
            _handler.Add((ushort)PacketID.PlayerInfoReq, PacketHandler.PlayerInfoReqHandler);
        }

        // 수신된 원시 바이트 배열(패킷 데이터)에서 첫 두 필드(패킷의 크기와 패킷 ID)를 읽어온 후,
        // 해당 패킷 ID에 대해 등록된 _onRecv의 액션(예: MakePacket<PlayerInfoReq>)을 호출합니다.
        public void OnRecvPacket(PacketSession session, ArraySegment<byte> buffer)
        {
            // 패킷 크기 및 ID 추출:
            // 버퍼의 첫 2바이트는 패킷 전체의 크기, 다음 2바이트는 패킷 ID입니다.
            ushort count = 0;
            ushort size = BitConverter.ToUInt16(buffer.Array, buffer.Offset);
            count += 2;
            ushort packetId = BitConverter.ToUInt16(buffer.Array, buffer.Offset + count);
            count += 2;

            Action<PacketSession, ArraySegment<byte>> action = null;
            
            // 해당 액션 찾기:
            // 등록된 딕셔너리 _onRecv에서 packetId에 해당하는 액션을 찾아 호출합니다.
            if (_onRecv.TryGetValue(packetId, out action))
            {
                // 찾은 액션은 PacketSession과 전체 바이트 배열을 인자로 받아,
                // 구체적인 패킷 객체를 생성하고 파싱하는 역할을 수행합니다.
                action.Invoke(session, buffer);
            }
        }

        // where T : IPacket, new() => T에 조건을 달아줌, IPacket을 상속받아야하고, new가 가능해야 한다.
        // IPacket을 상속한 Packet을 생성한 후 해당 패킷의 Protocol에 따라 해당하는 작업을 실행한다.
        // PacketHandler에 등록한 인터페이스를 호출
        // 역할
        // 제네릭 메서드로, IPacket 인터페이스를 구현한 구체적인 패킷 타입 T를 생성한 후,
        // buffer 데이터를 이용해 역직렬화(Read)하여 패킷 객체를 완성합니다.
        void MakePacket<T>(PacketSession session, ArraySegment<byte> buffer) where T : IPacket, new()
        {
            T packet = new T();
            // 패킷 데이터 파싱 (역직렬화):
            // 생성한 패킷의 Read() 메서드를 호출해 buffer에 담긴 데이터를 파싱합니다.
            packet.Read(buffer);

            Action<PacketSession, IPacket> action = null;
            // 생성된 패킷의 Protocol(즉, packetId)을 기준으로,
            // _handler 딕셔너리에서 해당하는 핸들러(예: PlayerInfoReqHandler)를 찾아서 호출합니다.
            if (_handler.TryGetValue(packet.Protocol, out action))
            {
                action?.Invoke(session, packet);
            }
        }
    }
}