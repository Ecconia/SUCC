﻿using System;

namespace SUCC.ParsingLogic
{
    /// <summary>
    /// Gets and sets the data encoded by Nodes.
    /// </summary>
    internal static class NodeManager
    {
        internal static void SetNodeData<T>(Node node, T data, FileStyle style) => SetNodeData(node, data, typeof(T), style);
        internal static void SetNodeData(Node node, object data, Type type, FileStyle style)
        {
            if (data == null)
            {
                node.ClearChildren();
                node.Value = Utilities.NullIndicator;
                return;
            }
            else if (node.Value == Utilities.NullIndicator)
            {
                node.Value = String.Empty;
            }

            // Ensure the type is initialized. This is especially important if it's added as
            // a base type in the type's static constructor.
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(type.TypeHandle);

            // If we try to save a single-line string and find it is currently saved as a multi-line string, we do NOT remove the mutli-line formatting.
            // The reason for this is that there might be comments on the """s, and we want to preserve those comments.
            // Also, this happens in only two cases:
            //     1. A string that is usually single-line is manually switched to multi-line formatting by a user
            //     2. A string is saved as multi-line, then later saved as single-line
            // In case 1, we don't want to piss off the user; keep it how they like it.
            // In case 2, the string is probably going to be saved again later with multiple lines. It doesn't seem necessary to disrupt the structure
            // of the file for something temporary.
            string dataAsString = data as string;
            if (type == typeof(string) && (dataAsString.ContainsNewLine() || node.ChildNodes.Count > 0))
                BaseTypesManager.SetStringSpecialCase(node, dataAsString, style);

            else if (BaseTypesManager.IsBaseType(type))
                SetBaseTypeNode(node, data, type, style);

            else if (CollectionTypes.TrySetCollection(node, data, type, style))
                return;

            else
                ComplexTypes.SetComplexNode(node, data, type, style);
        }

        internal static T GetNodeData<T>(Node node) => (T)GetNodeData(node, typeof(T));
        internal static object GetNodeData(Node node, Type type)
        {
            if (node.Value == Utilities.NullIndicator)
                return null;


            // Ensures that the type's static constructor has been run before we try to load it.
            // A convenient place to add base type rules is in the type's static constructor, so
            // this ensures the base type rules are registered before they are needed.
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(type.TypeHandle);

            try
            {
                if (type == typeof(string) && node.Value == MultiLineStringNode.Terminator && node.ChildLines.Count > 0)
                    return BaseTypesManager.ParseSpecialStringCase(node);

                if (BaseTypesManager.IsBaseType(type))
                    return RetrieveBaseTypeNode(node, type);

                var collection = CollectionTypes.TryGetCollection(node, type);
                if (collection != null) 
                    return collection;

                if (!node.Value.IsNullOrEmpty())
                    return ComplexTypeShortcuts.GetFromShortcut(node.Value, type);

                return ComplexTypes.RetrieveComplexType(node, type);
            }
            catch (Exception e)
            {
                throw new Exception($"Error getting data of type {type} from node: {e.InnerException}");
            }
        }


        private static void SetBaseTypeNode(Node node, object data, Type type, FileStyle style)
        {
            node.ClearChildren(NodeChildrenType.none);
            node.Value = BaseTypesManager.SerializeBaseType(data, type, style);
        }

        private static object RetrieveBaseTypeNode(Node node, Type type)
        {
            // Base types are unique in that they CAN be serialized as a single line, and indeed that is how SUCC will always save them.
            // However, you CAN manually write a file that uses complex type rules for a base type, and thanks to the logic in this method,
            // it will still work.
            // See https://github.com/JimmyCushnie/SUCC/issues/26

            if (node.ChildNodes.Count > 0)
                return ComplexTypes.RetrieveComplexType(node, type);

            if (BaseTypesManager.TryParseBaseType(node.Value, type, out var result))
                return result;

            return ComplexTypeShortcuts.GetFromShortcut(node.Value, type);
        }
    }
}