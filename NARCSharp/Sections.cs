using Syroot.BinaryData;
using System;
using System.Text;

namespace NARCSharp.Sections {
    /// <summary>
    /// The <see cref="BFAT"/> section contains the positions where the 
    /// files are contained inside the <see cref="NARC"/>.
    /// </summary>
    internal class BFAT {
        public (uint offset, uint size)[] FileDataArray;

        public BFAT() { }
        public BFAT(BinaryDataReader reader) {
            uint numberOfFiles = reader.ReadUInt32();
            FileDataArray = new (uint, uint)[numberOfFiles];

            for(ulong i = 0; i < numberOfFiles; i++) {
                FileDataArray[i].offset = reader.ReadUInt32();
                FileDataArray[i].size = reader.ReadUInt32();
            }
        }
    }

    /// <summary>
    /// The <see cref="BFNT"/> section contains the filenames for the files.
    /// </summary>
    internal class BFNT {
        public string[] FileNames;
        public ulong Unknown;

        public BFNT() { }
        public BFNT(BinaryDataReader reader, uint numberOfFiles) {
            Unknown = reader.ReadUInt64();
            FileNames = new string[numberOfFiles];

            for(int i = 0; i < numberOfFiles; i++) {
                FileNames[i] = reader.ReadString(BinaryStringFormat.ByteLengthPrefix, Encoding.ASCII);
#if DEBUG
                Console.WriteLine(FileNames[i]);
#endif
            }
        }
    }

    /// <summary>
    /// The <see cref="FIMG"/> section contains the bytes (content) of each file.
    /// </summary>
    internal class FIMG {
        public byte[][] FilesData;

        public FIMG() { }
        public FIMG(BinaryDataReader reader, BFAT btaf) {
            FilesData = new byte[btaf.FileDataArray.Length][];
            long PositionBuf = reader.Position;

            for(int i = 0; i < btaf.FileDataArray.Length; i++) {
                reader.Position = PositionBuf + btaf.FileDataArray[i].offset;

                FilesData[i] = reader.ReadBytes(
                    (int) (btaf.FileDataArray[i].size - btaf.FileDataArray[i].offset));
            }
        }
    }
}
