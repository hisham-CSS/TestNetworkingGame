using System;
using Bomberman.Core.Input;
using System.Collections.Generic;
using Bomberman.Core;

namespace Bomberman.Core.Rollback
{
    public struct OutgoingInputBundle
    {
        public int PlayerId;
        public int Frame;
        public InputState[] RedundantHistory; // Use array instead of List for cleaner struct
        public IntVector2 LocalPosition;
        public int LocalStateHash;
    }
}
