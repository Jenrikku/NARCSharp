using NewGear.IO;
using NewGear.Trees.TrueTree;

namespace NARCSharp {
    public struct NARC {
        public ByteOrder ByteOrder;
        /// <summary>
        /// The root of the archive's file system. Files and directories can be accessed from it.
        /// </summary>
        public BranchNode<byte[]> RootNode;

        public ushort Version;
        public ushort Reserved0; // Header length.
        public ushort Reserved1; // Section (block) count.

        public NARC() {
            ByteOrder = ByteOrder.LittleEndian;
            RootNode = new(string.Empty);

            Version = 0x0100;
            Reserved0 = 16;
            Reserved1 = 3;
        }
    }
}
