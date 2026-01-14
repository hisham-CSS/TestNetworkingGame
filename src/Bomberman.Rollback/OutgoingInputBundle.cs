using System;
using Bomberman.Core.Input;
using System.Collections.Generic;
using Bomberman.Core;

namespace Bomberman.Rollback
{
    /// <summary>
    /// Represents a packet of input information to be sent over the network.
    /// Contains the inputs for a specific frame and redundant history.
    /// </summary>
    public struct OutgoingInputBundle
    {
        /// <summary>ID of the player sending this bundle.</summary>
        public int PlayerId;
        
        /// <summary>The frame number for the primary input.</summary>
        public int Frame;
        
        /// <summary>History of previous inputs for recovery.</summary>
        public InputState[] RedundantHistory; // Use array instead of List for cleaner struct
        
        /// <summary>Current position of the player (for desync check).</summary>
        public IntVector2 LocalPosition;
        
        /// <summary>Hash of the local state (for desync check).</summary>
        public int LocalStateHash;
    }
}
