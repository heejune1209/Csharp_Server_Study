using System;
using System.Collections.Generic;
using System.Text;
using ServerCore;

namespace Server
{
    interface ITask
    {
        void Execute();
    }

    // ITask 인터페이스가 정의되어 있으며, 작업은 Execute() 메서드를 통해 실행됩니다.
    // BroadcastTask와 같은 클래스가 ITask를 구현하여,
    // 구체적인 작업(예: 방(Room) 내에서의 브로드캐스트 작업)을 캡슐화합니다.
    class BroadcastTask : ITask
    {
        GameRoom _room;
        ClientSession _session;
        string _chat;


        BroadcastTask(GameRoom room, ClientSession session, string chat)
        {
            _room = room;
            _session = session;
            _chat = chat;
        }

        public void Execute()
        {
            _room.Broadcast(_session, _chat);
        }
    }
    // Command 패턴으로 JobQueue를 구현하는 2번 째 방법
    class TaskQueue
    {
        Queue<ITask> _queue = new Queue<ITask>();
    }
}