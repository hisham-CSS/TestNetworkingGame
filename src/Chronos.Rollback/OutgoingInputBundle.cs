using Chronos.Core;

namespace Chronos.Rollback
{
    public struct OutgoingInputBundle<TInput> where TInput : struct, IInputState<TInput>
    {
        public int PlayerId;
        public int Frame;
        public TInput[] RedundantHistory;
        public int LocalPosX; // Generic placeholder for Sync Check
        public int LocalPosY;
        public int LocalStateHash;
    }
}
