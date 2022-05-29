using EveryFileExplorer;
using NARCSharp;
using System;
using System.IO;
using TrueTree;

namespace TestingUtilities {
    class Program {
        static void Main(string[] args) {
            Console.WriteLine("Input a file if needed:");
            string content = Console.ReadLine().Replace("\"", string.Empty);

            Console.WriteLine("\n0 - YAZ0 Decompression.");
            Console.WriteLine("1 - Reading-only test.");
            Console.WriteLine("2 - Extract all.");
            Console.WriteLine("3 - Simple read-write.");
            Console.WriteLine("4 - YAZ0 Compression.");
            Console.WriteLine("5 - YAZ0 Decompress-Compress.");
            Console.WriteLine("6 - Read-write with YAZ0.");

            switch(Console.ReadKey(true).KeyChar) {
                case '0':
                    File.WriteAllBytes(content + ".narc", YAZ0.Decompress(content));
                    break;
                case '1':
                    _ = new NARC(new FileStream(content, FileMode.Open));
                    break;
                case '2':
                    {
                        NARC narc = new(new FileStream(content, FileMode.Open));
                        IterateAndExtract(narc.FilesRoot, content + " unpacked");
                    }                    
                    break;
                case '3':
                        new NARC(new FileStream(content, FileMode.Open))
                            .Write(new FileStream(content + ".new.narc", FileMode.Create));
                    break;
                case '4':
                    File.WriteAllBytes(content + ".szs", YAZ0.Compress(content, 9));
                    break;
                case '5':
                    File.WriteAllBytes(content + ".repacked.szs", YAZ0.Compress(YAZ0.Decompress(content), 9));
                    break;
                case '6':
                    File.WriteAllBytes(content + ".repacked.szs",
                        YAZ0.Compress(
                            new NARC(
                                YAZ0.Decompress(content))
                            .Write(), 9));
                    break;
                default:
                    Console.WriteLine("Invalid input.");
                    break;
            }

            void IterateAndExtract(Node folder, string path) {
                string absolutePath = path;
                Directory.CreateDirectory(absolutePath);

                foreach(Node node in folder)
                    if(node.Contents[0] == true) // If it is a "folder"
                        IterateAndExtract(
                            node,
                            Path.Join(absolutePath, node.Name));
                    else                         // If it is a file.
                        File.WriteAllBytes(
                            Path.Join(absolutePath, node.Name),
                            node.Contents[1]);
            }
        }
    }
}
