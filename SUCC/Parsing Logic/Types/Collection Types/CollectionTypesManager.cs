﻿using System;

namespace SUCC.ParsingLogic.CollectionTypes
{
    internal static class CollectionTypesManager
    {
        public static bool IsSupportedType(Type collectionType)
        {
            if (Arrays.IsArrayType(collectionType))
                return true;

            if (Lists.IsListType(collectionType))
                return true;

            if (HashSets.IsHashSetType(collectionType))
                return true;

            if (Dictionaries.IsDictionaryType(collectionType))
                return true;

            return false;
        }

        public static void SetCollectionNode(Node node, object data, Type collectionType, FileStyle style)
        {
            if (Arrays.IsArrayType(collectionType))
                Arrays.SetArrayNode(node, data, collectionType, style);

            else if (Lists.IsListType(collectionType))
                Lists.SetListNode(node, data, collectionType, style);

            else if (HashSets.IsHashSetType(collectionType))
                HashSets.SetHashSetNode(node, data, collectionType, style);

            else if (Dictionaries.IsDictionaryType(collectionType))
                Dictionaries.SetDictionaryNode(node, data, collectionType, style);

            else
                throw new Exception("Unsupported type!");
        }

        public static object RetrieveCollection(Node node, Type collectionType)
        {
            if (!node.Value.IsNullOrEmpty())
                throw new FormatException("Collection nodes cannot have a value");


            if (Arrays.IsArrayType(collectionType))
                return Arrays.RetrieveArray(node, collectionType);

            if (Lists.IsListType(collectionType))
                return Lists.RetrieveList(node, collectionType);

            if (HashSets.IsHashSetType(collectionType))
                return HashSets.RetrieveHashSet(node, collectionType);

            if (Dictionaries.IsDictionaryType(collectionType))
                return Dictionaries.RetrieveDictionary(node, collectionType);


            throw new Exception("Unsupported type!");
        }
    }
}
