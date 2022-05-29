using Syroot.BinaryData;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using TrueTree;

namespace NARCSharp {
    public class NARC {
        /// <summary>
        /// The byte order of the <see cref="NARC"/> file, often being <see cref="ByteOrder.LittleEndian"/>.
        /// </summary>
        public ByteOrder ByteOrder { get; set; } = ByteOrder.LittleEndian;

        /// <summary>
        /// The version of the <see cref="NARC"/> file, often being '256'.
        /// </summary>
        public ushort Version { get; set; } = 1;

        /// <summary>
        /// Contains all the files inside the <see cref="NARC"/> file. It can be iterated.
        /// </summary>
        public Node FilesRoot { get; set; } = new("root");

        // For proper writing:
        private readonly byte[] bfntHeader;

        /// <summary>
        /// Creates a new empty NARC.
        /// </summary>
        public NARC() { }

        /// <summary>
        /// Reads a <see cref="NARC"/> from a byte array.
        /// </summary>
        /// <param name="bytes"></param>
        public NARC(byte[] bytes) : this(new MemoryStream(bytes)) { }

        /// <summary>
        /// Reads a <see cref="NARC"/> from a file.
        /// </summary>
        /// <param name="filename">The file's complete path.</param>
        public NARC(string filename) : this(new FileStream(filename, FileMode.Open)) { }

        /// <summary>
        /// Reads a <see cref="NARC"/> from a <see cref="Stream"/>.
        /// </summary>
        /// <param name="leaveOpen">Whether or not the stream will be kept opened. (false for disposing it)</param>
        public NARC(Stream stream, bool leaveOpen = false) {
            using BinaryDataReader reader = new(stream, Encoding.ASCII, leaveOpen);

            // Magic check:
            if(reader.ReadString(4) != "NARC")
                throw new InvalidDataException("The given file is not a NARC.");

            // Reads the byte order and changes the one used by the reader if needed:
            ByteOrder = (ByteOrder) reader.ReadUInt16();
            reader.ByteOrder = ByteOrder;

            Version = reader.ReadUInt16();

            reader.Position += 4; // Length skip (calculated when writing).

            Debug.Assert(reader.ReadUInt16() == 16); // Header length check.
            Debug.Assert(reader.ReadUInt16() == 3);  // Entry count check.

            Debug.Assert(reader.ReadString(4) == "BTAF"); // BFAT magic check.

            // The positions where the sections' reading was left last time.
            long bfatIndex = reader.Position + 8;
            long bfntIndex = reader.Position + reader.ReadUInt32() - 4; // From the BFAT length.
            long fimgIndex;

            uint fileCount = reader.ReadUInt32();

            uint currentFileOffset;
            uint currentFileEnd;

            #region BFNT preparations
            reader.Position = bfntIndex;
            Debug.Assert(reader.ReadString(4) == "BTNF"); // BFNT magic check.
            
            fimgIndex = reader.Position + reader.ReadUInt32() - 4; // Sets FIMG section begining.

            using(reader.TemporarySeek()) {
                reader.Position = fimgIndex;
                Debug.Assert(reader.ReadString(4) == "GMIF"); // FIMG magic check.
                fimgIndex += 8; // Skips magic and length.
            }

            uint bfntHeaderLength = reader.ReadUInt32() - 4;

            bfntHeader = new byte[bfntHeaderLength];
            for(int i = 0; i < bfntHeaderLength; i++)
                bfntHeader[i] = reader.ReadByte();
            #endregion

            Node currentFolder = FilesRoot;
            for(int i = 0; i < fileCount; i++) {
                byte nameLength = reader.ReadByte();

                if(nameLength == 0x00) { // End of the "folder".
                    currentFolder = currentFolder.Parent;
                    i--;
                    continue;
                }

                if(nameLength >= 0x80) { // If it is a "folder".
                    Node childFolder = new(reader.ReadString(nameLength & 0x7F));
                    childFolder.Contents.Add(true); // It is a "folder". (Contents[0] == true)

                    currentFolder = currentFolder.AddChild(childFolder);

                    reader.Position += 2;
                    i--;
                    continue;
                }

                // Read BFAT section:
                using(reader.TemporarySeek()) {
                    reader.Position = bfatIndex;

                    currentFileOffset = reader.ReadUInt32();
                    currentFileEnd = reader.ReadUInt32();

                    bfatIndex = reader.Position;
                }

                Node child = new(reader.ReadString(nameLength));
                child.Contents.Add(false); // It is a file. (Contents[0] == false)

                // Read FIMG section:
                using(reader.TemporarySeek()) {
                    reader.Position = fimgIndex + currentFileOffset;
                    child.Contents.Add(reader.ReadBytes((int) (currentFileEnd - currentFileOffset)));
                }

                currentFolder.AddChild(child);
            }
        }

        /// <summary>
        /// Writes a <see cref="NARC"/> into a file.
        /// </summary>
        /// <param name="filename">The file's complete path.</param>
        public void Write(string filename) {
            using FileStream stream = new(filename, FileMode.Create);
            Write(stream);
        }

        /// <returns>A packed <see cref="NARC"/> as a byte array.</returns>
        public byte[] Write() {
            using MemoryStream stream = new();

            Write(stream, true);
            return stream.ToArray();
        }

        /// <summary>
        /// Writes the <see cref="NARC"/> to a <see cref="Stream"/>.
        /// </summary>
        /// <param name="leaveOpen">Whether or not the stream will be kept opened. (false for disposing it)</param>
        public void Write(Stream stream, bool leaveOpen = false) {
            using BinaryDataWriter writer = new(stream, Encoding.ASCII, leaveOpen) {
                ByteOrder = ByteOrder
            };

            #region Header
            writer.Write("NARC",
                BinaryStringFormat.NoPrefixOrTermination); // Magic string.
            writer.Write((ushort) 0xFFFE);                 // Byte order.
            writer.Write(Version);                         // Version.
            writer.Position += 4;                          // Length skip. (calculated later)
            writer.Write((ushort) 0x10);                   // Header length.
            writer.Write((ushort) 0x03);                   // Section count.
            #endregion

            #region BFAT (pre)
            writer.Write("BTAF",
                BinaryStringFormat.NoPrefixOrTermination); // Magic string.

            long bfatLengthIndex = writer.Position; // For calculation the position later.

            writer.Position += 4; // Length skip. (calculated later)

            List<byte[]> fileContainer = new(); // Contains all the files without "folders".
            FolderIterate(FilesRoot);

            writer.Write(fileContainer.Count);               // Number of files. (hash pairs)
            writer.Write(new byte[fileContainer.Count * 8]); // Reserve hashes' positions.
            #endregion

            #region BFNT
            writer.Write("BTNF",
                BinaryStringFormat.NoPrefixOrTermination); // Magic string.

            long bfntLengthIndex = writer.Position; // For calculation the position later.

            writer.Position += 4;                // Length skip. (calculated later)
            writer.Write(bfntHeader.Length + 4); // Header length.
            writer.Write(bfntHeader);            // Header data.

            byte folderCount = 0;
            WriteBFNTEntry(FilesRoot); // Write all BFNT entries recursively.
            #endregion

            #region FIMG & BFAT
            writer.Write("GMIF",
                BinaryStringFormat.NoPrefixOrTermination); // Magic string.

            long fimgLengthIndex = writer.Position;

            writer.Position += 4; // Length skip (calculated later)

            long bfatIndex = 0x1C; // BFAT current position.
            foreach(byte[] entry in fileContainer) {
                uint currentOffset = 
                    (uint) writer.Position + 4 - (uint) fimgLengthIndex; // BFAT pair first entry.

                writer.Write(entry); // File contents.

                using(writer.TemporarySeek()) {
                    writer.Position = bfatIndex;
                    writer.Write(currentOffset);                // Relative offset of the file to the FIMG section.
                    writer.Write(currentOffset + entry.Length); // Relative end of the file to the FIMG section.

                    bfatIndex += 8; // Update bfatIndex.
                }
            }
            #endregion

            #region Sections' length
            writer.Position = bfatLengthIndex;
            writer.Write((uint) (bfntLengthIndex - bfatLengthIndex)); // BFAT length.

            writer.Position = bfntLengthIndex;
            writer.Write((uint) (fimgLengthIndex - bfntLengthIndex)); // BFNT length.

            writer.Position = fimgLengthIndex;
            writer.Write((uint) (writer.BaseStream.Length - fimgLengthIndex)); // FIMG length.

            writer.Position = 0x08;
            writer.Write((uint) writer.BaseStream.Length); // NARC total length.
            #endregion


            void FolderIterate(Node folderNode) {
                foreach(Node node in folderNode) {
                    if(node.Contents[0] == true) // If it is a "folder" then iterate through it.
                        FolderIterate(node);
                    else                         // If it is a file then add its content to the list.
                        fileContainer.Add(node.Contents[1]);
                }
            }

            void WriteBFNTEntry(Node entry) {
                foreach(Node node in entry) {
                    if(node.Contents[0] == true) { // If it is a "folder".
                        writer.Write((byte) (node.Name.Length + 0x80)); // Name's length.
                        writer.Write(node.Name,
                            BinaryStringFormat.NoPrefixOrTermination);  // Name.
                        writer.Write(folderCount++);                    // Folder id. (count)
                        writer.Write((byte) 0xF0);                      // Constant.

                        WriteBFNTEntry(node);
                    } else {
                        writer.Write(node.Name); // File name.
                    }
                }

                writer.Write((byte) 0x00); // End of "folder".
            }
        }
    }
}
