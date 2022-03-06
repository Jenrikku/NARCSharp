using EveryFileExplorer;
using NARCSharp;
using System;
using System.IO;
using System.Collections.Generic;

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
            Console.WriteLine("6 - Read-write with YAZ0");
            Console.WriteLine("7 - BCMDL Swap (With YAZ0)");

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
                        foreach(KeyValuePair<string, byte[]> cFile in narc.Files)
                            File.WriteAllBytes(Path.Join(Path.GetDirectoryName(content), cFile.Key), cFile.Value);
                    }                    
                    break;
                case '3':
                        new NARC(new FileStream(content, FileMode.Open))
                            .Write(new FileStream(content + ".new.narc", FileMode.Create));
                    break;
                case '4':
                    File.WriteAllBytes(content + ".szs", YAZ0.Compress(content));
                    break;
                case '5':
                    File.WriteAllBytes(content + ".repacked.szs", YAZ0.Compress(YAZ0.Decompress(content), 3));
                    break;
                case '6':
                    File.WriteAllBytes(content + ".repack.szs",
                        YAZ0.Compress(
                            new NARC(
                                new MemoryStream(
                                    YAZ0.Decompress(content)))
                            .Write(), 3));
                    break;
                case '7':
                    Console.WriteLine("Please input a bcmdl:");
                    string bcmdl = Console.ReadLine().Replace("\"", string.Empty);
                    Console.WriteLine("Please input the name of the bcmdl inside the szs:");
                    string name = Console.ReadLine();

                    NARC swappedNarc = new(
                        new MemoryStream(
                            YAZ0.Decompress(content)));

                    swappedNarc[name] = File.ReadAllBytes(bcmdl);

                    File.WriteAllBytes(content + ".swapped.szs",
                        YAZ0.Compress(swappedNarc.Write(), 3));
                    break;
            }
        }
    }
}
