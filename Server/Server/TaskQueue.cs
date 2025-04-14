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