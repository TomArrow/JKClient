using System;
using System.Collections.Generic;
using System.Text;

namespace JKClient
{
    public class EntityEventArgs : EventArgs
    {

        public EntityEvent EventType { get; private set; }
        public ClientEntity Entity { get; private set; }

        public EntityEventArgs(EntityEvent eventType, ClientEntity entity)
        {
            EventType = eventType;
            Entity = entity;
        }
    }
}
