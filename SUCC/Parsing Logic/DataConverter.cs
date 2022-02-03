﻿using SUCC.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SUCC.ParsingLogic
{
    internal static class DataConverter
    {
        internal static string GetLineTextIncludingChildLines(Line line)
            => SuccFromDataStructure(new Line[] { line });

        /// <summary>
        /// Turns a data structure into raw SUCC
        /// </summary>
        internal static string SuccFromDataStructure(IEnumerable<Line> lines)
        {
            var succBuilder = new StringBuilder();
            RecursivelyBuildLines(lines, succBuilder);
            return FinishSuccBuilder(succBuilder);


            void RecursivelyBuildLines(IEnumerable<Line> _lines, StringBuilder builder)
            {
                foreach (var line in _lines)
                {
                    builder.Append(line.RawText);
                    builder.Append(Utilities.NewLine);

                    if (line is Node node)
                        RecursivelyBuildLines(node.ChildLines, builder);
                }
            }

            string FinishSuccBuilder(StringBuilder builder)
                => builder.ToString().TrimEnd('\n', '\r', ' ');
        }

        /// <summary>
        /// Parses a string of SUCC into a data structure
        /// </summary>
        internal static (List<Line> topLevelLines, Dictionary<string, KeyNode> topLevelNodes) DataStructureFromSucc(string input, ReadableDataFile fileRef)
            => DataStructureFromSucc(input.SplitIntoLines(), fileRef);

        /// <summary>
        /// Parses lines of SUCC into a data structure
        /// </summary>
        internal static (List<Line>, Dictionary<string, KeyNode>) DataStructureFromSucc(string[] lines, ReadableDataFile dataFile) // I am so, so sorry. If you need to understand this function for whatever reason... may god give you guidance.
        {
            // If the file is empty
            // Do this because otherwise new files are created with a newline at the top
            if (lines.Length == 1 && lines[0] == "")
                return (new List<Line>(), new Dictionary<string, KeyNode>());


            var topLevelLines = new List<Line>();
            var topLevelNodes = new Dictionary<string, KeyNode>();

            var nestingNodeStack = new Stack<Node>(); // The top of the stack is the node that new nodes should be children of

            bool doingMultiLineString = false;
            int multiLineStringIndentationLevel = -1;

            // Parse the input line by line
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                var line = lines[lineIndex];
                if (line.Contains('\t'))
                    throw new FormatException("a SUCC file cannot contain tabs. Please use spaces instead.");

                if (doingMultiLineString)
                {
                    var parentNode = nestingNodeStack.Peek();

                    if (parentNode.ChildNodeType != NodeChildrenType.multiLineString)
                        throw new Exception("oh no, we were supposed to be doing a multi-line string but the top of the node stack isn't a multi-line string node!");

                    var newboi = new MultiLineStringNode(rawText: line, dataFile);

                    if (parentNode.ChildNodes.Count == 0)
                    {
                        // If this is the first line of the multi-line string, it determines the indentation level.
                        // However, that indentation level must be greater than the parent's.
                        multiLineStringIndentationLevel = newboi.IndentationLevel;

                        if (multiLineStringIndentationLevel <= parentNode.IndentationLevel)
                            throw new InvalidFileStructureException(dataFile, lineIndex, "multi-line string lines must have an indentation level greater than their parent");
                    }
                    else
                    {
                        if (newboi.IndentationLevel != multiLineStringIndentationLevel)
                            throw new InvalidFileStructureException(dataFile, lineIndex, "multi-line string lines must all have the same indentation level");
                    }

                    parentNode.AddChild(newboi);

                    if (newboi.IsTerminator)
                    {
                        doingMultiLineString = false;
                        multiLineStringIndentationLevel = -1;

                        nestingNodeStack.Pop();
                    }

                    continue;
                }

                if (LineHasData(line))
                {
                    Node node = GetNodeFromLine(line, dataFile, lineIndex);

                    addNodeInAppriatePlaceInStack:

                    if (nestingNodeStack.Count == 0) // If this is a top-level node
                    {
                        if (!(node is KeyNode))
                            throw new InvalidFileStructureException(dataFile, lineIndex, "top level lines must be key nodes");

                        topLevelLines.Add(node);
                        KeyNode heck = node as KeyNode;

                        try
                        {
                            topLevelNodes.Add(heck.Key, heck);
                        }
                        catch (ArgumentException)
                        {
                            throw new InvalidFileStructureException(dataFile, lineIndex, $"multiple top level keys called '{heck.Key}'");
                        }
                    }
                    else // If this is NOT a top-level node
                    {
                        int stackTopIndentation = nestingNodeStack.Peek().IndentationLevel;
                        int lineIndentation = line.GetIndentationLevel();

                        if (lineIndentation > stackTopIndentation) // If this should be a child of the stack top
                        {
                            Node newParent = nestingNodeStack.Peek();
                            if (newParent.ChildNodes.Count == 0) // If this is the first child of the parent, assign the parent's child type
                            {
                                if (node is KeyNode)
                                    newParent.ChildNodeType = NodeChildrenType.key;
                                else if (node is ListNode)
                                    newParent.ChildNodeType = NodeChildrenType.list;
                                else
                                    throw new Exception("what the heck?");
                            }
                            else // If the parent already has children, check for errors with this line
                            {
                                CheckNewSiblingForErrors(child: node, newParent: newParent, dataFile, lineIndex);
                            }

                            try
                            {
                                newParent.AddChild(node);
                            }
                            catch (ArgumentException)
                            {
                                throw new InvalidFileStructureException(dataFile, lineIndex, $"multiple sibling keys called '{(node as KeyNode).Key}' (indentation level {lineIndentation})");
                            }
                        }
                        else // If this should NOT be a child of the stack top
                        {
                            nestingNodeStack.Pop();
                            goto addNodeInAppriatePlaceInStack;
                        }
                    }

                    if (node.Value == "") // If this is a node with children
                        nestingNodeStack.Push(node);

                    if (node.Value == MultiLineStringNode.Terminator) // if this is the start of a multi line string
                    {
                        nestingNodeStack.Push(node);
                        node.ChildNodeType = NodeChildrenType.multiLineString;

                        doingMultiLineString = true;
                    }
                }
                else // Line has no data
                {
                    Line NoDataLine = new Line(rawText: line);

                    if (nestingNodeStack.Count == 0)
                        topLevelLines.Add(NoDataLine);
                    else
                        nestingNodeStack.Peek().AddChild(NoDataLine);
                }
            }

            return (topLevelLines, topLevelNodes);
        }



        private static bool LineHasData(string line)
        {
            line = line.Trim();
            return line.Length != 0 && line[0] != '#';
        }

        private static Node GetNodeFromLine(string line, ReadableDataFile file, int lineNumber)
        {
            var dataType = GetDataLineType(line);
            switch (dataType)
            {
                case DataLineType.key:
                    return new KeyNode(rawText: line, file);
                    
                case DataLineType.list:
                    return new ListNode(rawText: line, file);

                default:
                    throw new InvalidFileStructureException(file, lineNumber, "format error");
            }
        }

        private static void CheckNewSiblingForErrors(Node child, Node newParent, ReadableDataFile dataFile, int lineNumber)
        {
            Node sibling = newParent.ChildNodes[0];
            if (child.IndentationLevel != sibling.IndentationLevel) // if there is a mismatch between the new node's indentation and its sibling's
                throw new InvalidFileStructureException(dataFile, lineNumber, "Line did not have the same indentation as its assumed sibling");

            if (  // if there is a mismatch between the new node's type and its sibling's
                   newParent.ChildNodeType == NodeChildrenType.key && !(child is KeyNode)
                || newParent.ChildNodeType == NodeChildrenType.list && !(child is ListNode)
                || newParent.ChildNodeType == NodeChildrenType.multiLineString
                || newParent.ChildNodeType == NodeChildrenType.none)
                throw new InvalidFileStructureException(dataFile, lineNumber, $"Line did not match the child type of its parent");
        }

        private enum DataLineType { none, key, list }
        private static DataLineType GetDataLineType(string line)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0) return DataLineType.none;
            if (trimmed[0] == '#') return DataLineType.none;
            if (trimmed[0] == '-') return DataLineType.list;
            if (trimmed.Contains(':')) return DataLineType.key;

            return DataLineType.none;
        }
    }
}