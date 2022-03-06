using Syroot.BinaryData;

namespace NARCSharp {
    internal static class Internal {
        public static void WriteBytes(this BinaryDataWriter bdw, int amount, byte Byte) {
            for(int i = 0; i < amount; i++)
                bdw.Write(Byte);
        }
    }
}
