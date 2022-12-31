using NewGear.Extensions;
using NewGear.IO;
using NewGear.Trees.TrueTree;

// Check out this file's format here: https://redpepper.miraheze.org/wiki/NARC
namespace NARCSharp {
    public class NARCParser {
        /// <summary>
        /// Checks if the given file is a NARC.
        /// </summary>
        public static bool Identify(byte[] data) => data.CheckMagic("NARC");

        public static NARC Read(byte[] data) {
            using BinaryStream stream = new(data) { ByteOrder = ByteOrder.BigEndian, Position = 4 };

            NARC narc = new();


            // Header ------------------------

            if(!Identify(data)) // Magic check.
                throw new InvalidDataException("The given file is not a NARC.");

            // Reads the byte order and changes the one used by the stream if required:
            narc.ByteOrder = stream.Read<ByteOrder>();
            stream.ByteOrder = narc.ByteOrder;

            narc.Version = stream.Read<ushort>();

            stream.Position += 4; // Length skip (calculated when writing).

            // Reserved values (should be constant)
            narc.Reserved0 = stream.Read<ushort>(); // 16 (Header length)
            narc.Reserved1 = stream.Read<ushort>(); // 3  (Section / block count)


            // FATB --------------------------

            // Moves the stream to the position after the magic:
            stream.Position += 4;

            if(!data.CheckMagic("BTAF", 16)) // Magic check.
                throw new InvalidDataException("The FATB section was not found. The file may be corrupted.");

            // Calculates the beginning of the next section:
            ulong fntbBeginning = stream.Position - 4 + stream.Read<uint>();

            uint fatbCount = stream.Read<uint>(); // Amount of hashes

            // startPos -> The starting position of the file.
            // endPos   -> The ending position of the file.
            // Both positions are relative to the FIMG section, right after the length.
            // Each entry in the array describes a file's boundary.
            (uint startPos, uint endPos)[] positions = new (uint, uint)[fatbCount];

            for(uint i = 0; i < fatbCount; i++) // Reads all hashes.
                positions[i] = (stream.Read<uint>(), stream.Read<uint>());

            if(fatbCount > 0) {
                narc.HasAlignment = false; // Disables alignment temporarily.

                // Goes through all file starts and ends and checks if there are any differences:
                for(int i = 0; i < positions.Length - 1;) {
                    if(positions[i].endPos != positions[++i].startPos)
                        // Disables alignment when writing if the file does not require it:
                        narc.HasAlignment = true;
                }
            }


            // FNTB --------------------------

            // Moves the stream to the position after the magic number:
            stream.Position = fntbBeginning + 4;

            if(!data.CheckMagic("BTNF", (int) fntbBeginning)) // Magic check.
                throw new InvalidDataException("The FNTB section was not found. The file may be corrupted.");

            // Calculates the beginning of the next section:
            ulong fimgBeginning = stream.Position - 4 + stream.Read<uint>();

            ulong dirEntriesStart = stream.Position; // The start of the directory entries array. Constant.
            ulong dirEntriesPos = stream.Position;   // The current position within the directory entries array.

            ReadDirectory(narc.RootNode);

            narc.Nameless = !narc.RootNode.HasChildren; // Checks if the file contains no names.

            // Called each time a directory is found.
            // The stream's position is within the directory entries array.
            void ReadDirectory(BranchNode<byte[]> dir) {
                // The start position of the directory within the name array.
                ulong startPosition = dirEntriesStart + stream.Read<uint>();

                // Advances the position within the directory entries array.
                dirEntriesPos = stream.Position + 4;

                // Sets the stream's position to the directory's start point.
                stream.Position = startPosition;

                while(stream.Peek() != 0) { // 0 means that the end of the directory has been reached.
                    byte length = stream.Read<byte>(); // Names are length-prefixed.

                    if(length >= 0b10000000) { // Directory.
                        length = (byte) (length & 0b01111111); // Removes the first bit from the length.

                        // Creates a new branch node with the directory's name. 
                        BranchNode<byte[]> childDir = new(stream.ReadString(length));

                        using(stream.TemporarySeek()) {
                            stream.Position = dirEntriesPos; // Goes back to the directory entries array.
                            ReadDirectory(childDir); // Reads the child directory.
                        } // Return to the past position.

                        stream.Position += 2; // Skips directory ID and child directories count.

                        dir.AddChild(childDir); // Adds the child to its parent.
                    } else // Reads a file and adds it directly to its parent directory.
                        dir.AddChild(new LeafNode<byte[]>(stream.ReadString(length)));
                }
            }


            // FIMG --------------------------

            // Moves the stream to the position after the length:
            stream.Position = fimgBeginning + 8;

            if(!data.CheckMagic("GMIF", (int) fimgBeginning)) // Magic check.
                throw new InvalidDataException("The FIMG section was not found. The file may be corrupted.");

            uint index = 0; // The file index. Used to get the right hash from the FATB section.
            IterateDirectory(narc.RootNode);

            // Read remaining files with no names:
            while(positions.Length > index) {
                LeafNode<byte[]> file = new(index.ToString()); // Creates a new leaf node with the index as its name.

                ReadContents(file);

                narc.RootNode.AddChild(file); // Adds the file to the root node.
            }

            // Reads a directory by iterating through all child nodes in it.
            void IterateDirectory(BranchNode<byte[]> dir) {
                // Because of how NARC files are often written, files have to be read before child directories.

                foreach(LeafNode<byte[]> file in dir.ChildLeaves) // Reads files.
                    ReadContents(file);

                foreach(BranchNode<byte[]> childDir in dir.ChildBranches) // Reads child directories.
                    IterateDirectory(childDir);
            }

            // Reads the contents from the next file and puts them into the given leaf node.
            void ReadContents(LeafNode<byte[]> file) {
                (uint startPos, uint endPos) = positions[index++]; // Gets the current hash.

                using(stream.TemporarySeek()) {
                    stream.Position += startPos; // Sets the position to the file's beginning.

                    // Reads the file's contents.
                    // The size is calculated by subtracting the starting position to the end position.
                    file.Contents = stream.Read<byte>((int) (endPos - startPos));
                } // Return to the past position.
            }


            // -------------------------------

            return narc;
        }

        public static byte[] Write(NARC narc) {
            using BinaryStream stream = new(0xFF) { ByteOrder = narc.ByteOrder };


            // Header ------------------------

            stream.Write("NARC");         // Magic.
            stream.Write<ushort>(0xFFFE); // Endian.
            stream.Write(narc.Version);   // Version.

            stream.Position += 4;         // Length skip.

            stream.Write(narc.Reserved0); // Header length. (16)
            stream.Write(narc.Reserved1); // Section count. (3)


            // FATB --------------------------

            stream.Write("BTAF"); // Magic.

            uint count = 0; // Amount of files. (hash count)

            CountFiles(narc.RootNode);

            // Goes thourgh all files inside the given directory and all its children:
            void CountFiles(BranchNode<byte[]> dir) {
                foreach(LeafNode<byte[]> file in dir.ChildLeaves)
                    count++;

                foreach(BranchNode<byte[]> childDir in dir.ChildBranches)
                    CountFiles(childDir);
            }

            stream.Write(count * 8 + 12); // Section length.

            stream.Write(count); // Hash count.

            // The current position inside the hash array. Used when writing file contents.
            uint fbatHashPos = (uint) stream.Position;

            // Reserve spaces to put hashes later:
            stream.Position += count * 8;


            // FNTB --------------------------

            uint fntbStartPos = (uint) stream.Position; // FNTB start position. (before magic)

            stream.Write("BTNF"); // Magic.

            stream.Position += 4; // Length skip.

            uint dirEntriesStart = (uint) stream.Position;   // The start of the directory entries array. Constant.
            uint dirEntriesPos = (uint) stream.Position + 8; // The current position within the directory entries array.
            uint dirEntriesLength = 8;                       // The length of the directory entries array.

            if(narc.Nameless) // If no names are contained, the root directory points towards the 4th byte.
                dirEntriesLength = 4;
            else
                FindChildDirectories(narc.RootNode);

            // Finds all child directories in order to calculate the directory entries array:
            void FindChildDirectories(BranchNode<byte[]> dir) {
                foreach(BranchNode<byte[]> child in dir.ChildBranches) {
                    FindChildDirectories(child); // Searches for more directories inside the child.
                    dirEntriesLength += 8;
                }
            }

            // The length of the directory entries array is also the root's offset.
            stream.Write(dirEntriesLength);

            // No files before (always constant in the root directory):
            stream.Write<ushort>(0);

            // Amount of children directories:
            stream.Write((ushort) (narc.RootNode.ChildBranches.Count + 1));

            // Reserve space for directory entries:
            if(!narc.Nameless)
                stream.Position += dirEntriesLength - 8;

            // Write names:

            byte directoryID = 0;  // Increases by one each time a directory's name has been written.
            ushort fileAmount = 0; // Increases by one each time a file's name has been written.

            if(!narc.Nameless) // Only write names if required.
                WriteNames(narc.RootNode);

            // Writes all names and also goes back and writes directory entries when required:
            void WriteNames(BranchNode<byte[]> dir) {
                // Names:
                foreach(INode<byte[]> node in dir) {
                    if(node is BranchNode<byte[]>) { // Directory names.
                        stream.Write((byte) (node.Name.Length + 0b10000000)); // Name length. (8th bit set to 1)
                        stream.Write(node.Name);                              // Name.
                        stream.Write(++directoryID);                          // Directory ID.
                        stream.Write<byte>(0xF0);                             // Constant byte. (0xF0)
                    } else {                         // Filenames.
                        stream.Write((byte) node.Name.Length);                // Name length.
                        stream.Write(node.Name);                              // Name.

                        fileAmount++;
                    }
                }

                stream.Write<byte>(0); // End-of-directory byte.

                // Directory entries:
                foreach(BranchNode<byte[]> node in dir.ChildBranches) {
                    // The relative position of the current position to the beginning of the directory entries array.
                    uint nameSectionRelPos = (uint) stream.Position - dirEntriesStart;

                    using(stream.TemporarySeek()) {
                        // Sets the position to the beginning of the directory entries array.
                        stream.Position = dirEntriesPos;

                        stream.Write(nameSectionRelPos); // Offset to the directoriy's beginning inside the names array.
                        stream.Write(fileAmount);        // Amount of files present before this directory.

                        ushort childCount = (ushort) node.ChildBranches.Count; // Amount of child directories.

                        // Amount of children directories, counting the parent itself. (+1)
                        // 0xF000 if none.
                        stream.Write((ushort) (childCount == 0 ? 0xF000 : childCount + 1));

                        dirEntriesPos += 8; // Advances the current position within the directory entries array.
                    } // Return to the past position.

                    WriteNames(node); // Write the names of this directory's children.
                }
            }

            if(narc.HasAlignment)
                stream.Align(128); // Alignment to the FIMG section.

            using(stream.TemporarySeek()) {
                // Calculates the FNTB section's length by taking in mind its starting point:
                uint fntbLength = (uint) (stream.Position - fntbStartPos);

                stream.Position = fntbStartPos + 4; // Goes to the FNTB's length position.
                stream.Write(fntbLength);           // FNTB section's length.
            } // Return to the FIMG section's beginning.


            // FIMG --------------------------

            uint fimgStartPos = (uint) stream.Position; // FIMG start position. (before magic)

            stream.Write("GMIF"); // Magic

            stream.Position += 4; // Length skip.

            WriteFileData(narc.RootNode);

            // Writes file contents and also FATB's hashes:
            void WriteFileData(BranchNode<byte[]> dir) {
                foreach(LeafNode<byte[]> file in dir.ChildLeaves) { // Files within the directory.
                    // FATB hash values, both relatives to the position after the FIMG's length:
                    uint fileStart = (uint) stream.Position - (fimgStartPos + 8);
                    uint fileEnd;

                    stream.Write(file.Contents ?? Array.Empty<byte>()); // File contents.

                    fileEnd = (uint) stream.Position - (fimgStartPos + 8); // Calculates file's end.

                    using(stream.TemporarySeek()) {
                        stream.Position = fbatHashPos; // Goes to the current position within the hash array.

                        stream.Write(fileStart); // File's start. 
                        stream.Write(fileEnd);   // File's end.

                        fbatHashPos += 8; // Advances the position within the hash array.
                    } // Return to the past position.

                    if(narc.HasAlignment)
                        stream.Align(128); // Align to the next file.
                }

                foreach(BranchNode<byte[]> childDir in dir.ChildBranches)
                    WriteFileData(childDir); // Files within child directories.
            }

            uint fimgLength = (uint) (stream.Position - fimgStartPos); // Calculate FIMG section's length.

            stream.Position = fimgStartPos + 4; // Goes to the FIMG's length position.
            stream.Write(fimgLength);           // FIMG's section length.


            // -------------------------------

            stream.Position = 8; // Goes back to the file's length position.

            stream.Write((uint) stream.Length); // Writes the file's length.

            return stream.ToArray();
        }
    }
}