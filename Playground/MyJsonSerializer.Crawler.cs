using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static Playground.MyJsonSerializer;

namespace Playground
{
    public static partial class MyJsonSerializer
    {
        private static partial class InternalSerializer<T> where T : IJsonWriter
        {
            public struct Crawler
            {
                public T writer;
                public Settings settings;
                public string currentPath;
                public Dictionary<object, string> pathMap;
                public string refPath;

                public static Crawler Root(object rootObj, Settings settings, T writer)
                {

                    if (settings.referenceCheck == ReferenceCheck.NoRefCheck)
                    {
                        return new Crawler()
                        {
                            settings = settings,
                            writer = writer
                        };
                    }

                    return new Crawler()
                    {
                        settings = settings,
                        currentPath = "$",
                        pathMap = new Dictionary<object, string>() { { rootObj, "$" } },
                        writer = writer
                    };
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public Crawler NewChild(object child, string name)
                {
                    if (settings.referenceCheck == ReferenceCheck.NoRefCheck) return this;

                    string childRefPath = null;
                    string childPath = currentPath + "." + name;
                    if (child != null && child.GetType().IsClass && !(child is string))
                    {
                        childRefPath = FindObjRefPath(child);
                        pathMap[child] = childPath;
                    }

                    return new Crawler()
                    {
                        settings = settings,
                        currentPath = childPath,
                        pathMap = pathMap,
                        refPath = childRefPath,
                        writer = writer
                    };
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public Crawler NewCollectionItem(object item, string fieldName, int index)
                {
                    if (settings.referenceCheck == ReferenceCheck.NoRefCheck) return this;

                    string childRefPath = null;
                    string childPath = $"{currentPath}.{fieldName}[{index}]";
                    if (item != null && item.GetType().IsClass && !(item is string))
                    {
                        childRefPath = FindObjRefPath(item);
                        pathMap[item] = childPath;
                    }

                    return new Crawler()
                    {
                        settings = settings,
                        currentPath = childPath,
                        pathMap = pathMap,
                        refPath = childRefPath,
                        writer = writer
                    };
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                private string FindObjRefPath(object obj)
                {
                    if (settings.referenceCheck == ReferenceCheck.NoRefCheck) return null;
                    if (pathMap.TryGetValue(obj, out string path)) return path;
                    else return null;
                }
            }
        }
    }
}
