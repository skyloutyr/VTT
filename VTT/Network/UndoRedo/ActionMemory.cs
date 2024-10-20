namespace VTT.Network.UndoRedo
{
    using System;
    using System.Collections.Generic;
    using VTT.Control;
    using VTT.Network.Packet;

    public class ActionMemory
    {
        private readonly List<ServerAction> _actions = new List<ServerAction>();
        private readonly object _lock = new object();
        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0052:Remove unread private members", Justification = "TBD Later")]
        private readonly ServerClient _owner;

        public ActionMemory(ServerClient serverClient) => this._owner = serverClient;

        public Guid Owner { get; set; }
        public int ActionAmount => _actions.Count;
        public int CurrentIndex { get; set; } = -1;
        public int ActionBufferSize { get; set; } = 32;

        public void NewAction(ServerAction sa)
        {
            if (sa is SmallChangeAction sca && _actions.Count > 0)
            {
                lock (_lock)
                {
                    ServerAction csa = _actions[CurrentIndex];
                    if (csa is SmallChangeAction sccsa && sccsa.ActionType == sa.ActionType)
                    {
                        if (sccsa.AcceptSmallChange(sca))
                        {
                            return;
                        }
                    }
                }
            }

            lock (_lock)
            {
                if (CurrentIndex < _actions.Count - 1)
                {
                    _actions.RemoveRange(CurrentIndex + 1, _actions.Count - CurrentIndex - 1);
                }

                if (this._actions.Count + 1 > this.ActionBufferSize)
                {
                    this._actions.RemoveAt(0);
                    --this.CurrentIndex;
                }

                _actions.Add(sa);
                ++CurrentIndex;
            }
        }

        public bool UndoAction()
        {
            lock (_lock)
            {
                if (_actions.Count > 0 && CurrentIndex >= 0)
                {
                    ServerAction sa = _actions[CurrentIndex];
                    sa.Undo();
                    --CurrentIndex;
                    return true;
                }

                return false;
            }
        }

        public bool RedoAction()
        {
            lock (_lock)
            {
                if (_actions.Count < CurrentIndex + 1)
                {
                    ServerAction sa = _actions[CurrentIndex + 1];
                    sa.Redo();
                    ++CurrentIndex;
                    return true;
                }

                return false;
            }
        }
    }

    public abstract class ServerAction
    {
        public abstract ServerActionType ActionType { get; }

        public abstract void Undo();
        public abstract void Redo();

        public void SendToAllOnMap(Map cMap, PacketBase packet) => packet.Broadcast(x => x.ClientMapID.Equals(cMap.ID));
    }

    public abstract class SmallChangeAction : ServerAction
    {
        public DateTime LastModifyTime { get; set; }

        public abstract bool AcceptSmallChange(SmallChangeAction newAction);

        public bool CheckIfRecent(DateTime otherTime, int ms) => (otherTime - this.LastModifyTime).Milliseconds <= ms;
    }

    public enum ServerActionType
    {
        Unknown,
        AddDrawing,
        AddTurnEntry,
        AuraAddOrRemove,
        AuraChange
    }
}
