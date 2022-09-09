using System;
using System.Collections;
using System.Collections.Generic;

namespace NARCSharp {
#nullable enable
    public interface INode {
        /// <summary>
        /// The ID of the node. (It can be the same as another node)
        /// </summary>
        public dynamic ID { get; set; }
        /// <summary>
        /// Used to store data that does not belong to the node's contents.
        /// </summary>
        public dynamic? Metadata { get; set; }
        /// <summary>
        /// The contents of this node, used to store various data.
        /// </summary>
        public dynamic? Contents { get; set; }
        /// <summary>
        /// Represents a link between the data on two nodes, normally from different trees.
        /// </summary>
        public INode? LinkedNode { get; set; }
        /// <summary>
        /// Returns the parent node.
        /// </summary>
        public BranchNode? Parent { get; set; }
    }

    public class BranchNode : INode, IEnumerable<INode> {
        public BranchNode(dynamic id) => ID = id;

        /// <summary>
        /// Returns the first occurrence of a child with the same ID.
        /// </summary>
        /// <param name="id">The child's ID.</param>
        /// <returns></returns>
        public INode? this[dynamic id] {
            get {
                foreach(INode node in children)
                    if(node.ID == id)
                        return node;
                return null;
            }
        }

        /// <summary>
        /// Returns a node based on what its index is.
        /// </summary>
        /// <param name="index">The index of the node.</param>
        /// <returns></returns>
        public INode this[int index] {
            get => children[index];
        }

        /// <summary>
        /// Checks whether or not this node has children.
        /// </summary>
        public bool HasChildren { get => children.Count > 0; }

        internal List<INode> children = new();

        /// <summary>
        /// Adds a node as a child to another one and returns this child node.
        /// </summary>
        public INode AddChild(INode child) {
            child.Parent = this;
            children.Add(child);
            return child;
        }

        /// <summary>
        /// Removes a child node from the current branch.
        /// </summary>
        /// <returns>Whether the node was removed successfully.</returns>
        public bool RemoveChild(INode child) => children.Remove(child);

        /// <summary>
        /// Removes a child node at an specific index.
        /// </summary>
        /// <param name="index"></param>
        public void RemoveChild(int index) => children.RemoveAt(index);

        /// <summary>
        /// Finds a child by it's relative path.
        /// Example: "firstChild/secondChild"
        /// </summary>
        public INode? FindChildByPath(string relativePath) => FindChildByPath<INode>(relativePath);

        /// <summary>
        /// Finds a child by it's relative path.
        /// Example: "firstChild/secondChild"
        /// </summary>
        public T? FindChildByPath<T>(string relativePath) where T : INode {
            string[] entries = relativePath.Split('/');

            INode current = this;
            foreach(string entry in entries) {
                if(current is not BranchNode branch)
                    throw new ArgumentException($"{relativePath} contains children inside nodes that are not branches.");

                current = branch.children.Find((INode node) => { return node.ID == entry; })
                    ?? throw new ArgumentException("A part of the path could not be found.");
            }

            return (T) current;
        }

        /// <summary>
        /// Converts a <see cref="LeafNode"/> into a <see cref="BranchNode"/>.
        /// </summary>
        public static explicit operator BranchNode(LeafNode leaf) {
            return new(leaf.ID) {
                Contents = leaf.Contents,
                Metadata = leaf.Metadata,
                LinkedNode = leaf.LinkedNode,
                Parent = leaf.Parent
            };
        }

        // Interface implementation.
        public IEnumerator<INode> GetEnumerator() => children.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => children.GetEnumerator();

        public dynamic ID { get; set; }
        public dynamic? Contents { get; set; }
        public dynamic? Metadata { get; set; }
        public INode? LinkedNode { get; set; }
        public BranchNode? Parent { get; set; }
    }

    public class LeafNode : INode {
        public LeafNode(dynamic id) => ID = id;

        // Interface implementation.
        public dynamic ID { get; set; }
        public dynamic? Contents { get; set; }
        public dynamic? Metadata { get; set; }
        public INode? LinkedNode { get; set; }
        public BranchNode? Parent { get; set; }
    }
}
