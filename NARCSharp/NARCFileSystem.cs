using NewGear.Trees.TrueTree;

namespace NARCSharp {
    public class NARCFileSystem {
        private NARC _narc;
        private readonly BranchNode<byte[]> _root;

        public NARCFileSystem(in NARC narc) {
            _narc = narc;
            _root = narc.RootNode;
        }

        public byte[] GetFile(string path) {
            LeafNode<byte[]>? node = _root.FindChildByPath<LeafNode<byte[]>>(path);

            if(node?.Contents is null)
                return Array.Empty<byte>();

            return node.Contents;
        }

        /// <summary>
        /// Writes a file to the system, overriding it if the entry already exists.
        /// </summary>
        public void AddFile(string path, byte[] data) {
            LeafNode<byte[]>? node = _root.FindChildByPath<LeafNode<byte[]>>(path);

            if(node is not null) {
                node.Contents = data;
                return;
            }

            string[] parts = path.Split('/');
            BranchNode<byte[]> current = _root;

            for(int i = 0; i <= parts.Length; i++) {
                BranchNode<byte[]> child = new(parts[i]);

                current.AddChild(child);
                current = child;
            }

            current.AddChild(new LeafNode<byte[]>(parts[parts.Length], data));
        }

        /// <summary>
        /// Removes a file from the system.
        /// </summary>
        /// <returns></returns>
        public bool RemoveFile(string path) {
            LeafNode<byte[]>? leaf = _root.FindChildByPath<LeafNode<byte[]>>(path);

            if(leaf is null)
                return false;

            return RemoveFile(leaf);
        }

        /// <summary>
        /// Removes a directory if empty. Setting recursive to true deletes all files and directories inside it.
        /// </summary>
        /// <param name="recursive">Setting this parameter to true removes all child files and directories.</param>
        /// <returns>Whether the directory was removed successfully.</returns>
        public bool RemoveDirectory(string path, bool recursive = false) {
            BranchNode<byte[]>? branch = _root.FindChildByPath<BranchNode<byte[]>>(path);

            if(branch is null || (!recursive && branch.HasChildren))
                return false;

            return RemoveDirectory(branch);
        }

        public Dictionary<string, INode<byte[]>>? GetDirectoryContents(string path) {
            BranchNode<byte[]>? branch = _root.FindChildByPath<BranchNode<byte[]>>(path);

            if(branch is null)
                return null;

            return GetDirectoryContents(branch);
        }

        public Dictionary<string, INode<byte[]>> GetDirectoryContents(BranchNode<byte[]> dir) {
            Dictionary<string, INode<byte[]>> dict = new();

            foreach(INode<byte[]> node in dir)
                dict.Add(node.Name, node);

            return dict;
        }

        /// <summary>
        /// Searches through all directories and subdirectories and lists them into a string array.
        /// A path from where to start the search can be specified.
        /// </summary>
        public string[] ListDirectoryTree(string path = "") {
            BranchNode<byte[]>? startPath = _root;
            List<string> pathList = new();
            string relPath = path;

            if(path.Length > 0 && !relPath.EndsWith("/"))
                relPath += "/";

            if(string.IsNullOrEmpty(path))
                startPath = _root.FindChildByPath<BranchNode<byte[]>>(path);

            if(startPath is null)
                return Array.Empty<string>();
            RecursiveSearch(startPath);

            void RecursiveSearch(BranchNode<byte[]> node) {
                pathList.Add(relPath + node.Name);

                foreach(BranchNode<byte[]> child in node.ChildBranches) {
                    relPath += child.Name + "/";

                    RecursiveSearch(child);
                }
            }

            return pathList.ToArray();
        }

        public NARC ToNARC() => _narc;


        private static bool RemoveFile(LeafNode<byte[]> leaf) =>
            leaf.Parent?.RemoveChild(leaf) ?? false;

        private static bool RemoveDirectory(BranchNode<byte[]> branch) {
            foreach(INode<byte[]> node in branch)
                if(node is LeafNode<byte[]> childLeaf)
                    RemoveFile(childLeaf);
                else if(node is BranchNode<byte[]> childBranch)
                    RemoveDirectory(childBranch);

            return branch.Parent?.RemoveChild(branch) ?? false;
        }
    }
}
