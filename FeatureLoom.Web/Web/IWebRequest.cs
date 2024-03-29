﻿using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace FeatureLoom.Web
{
    public interface IWebRequest
    {
        Stream Stream { get; }

        Task<string> ReadAsync();

        string ContentType { get; }

        bool TryGetCookie(string key, out string content);

        bool IsGet { get; }
        bool IsPost { get; }
        bool IsPut { get; }
        bool IsDelete { get; }
        bool IsHead { get; }
        string Method { get; }
        string RelativePath { get; }
        string Path { get; }
        string BasePath { get; }
        string FullPath { get; }
        string HostAddress { get; }
        string OriginalPath { get; }

        void ChangePath(string newPath);

        bool TryGetQueryItem(string key, out string item);
        IEnumerable<string> GetAllQueryKeys();

        bool RequestsWebSocket { get; }
    }
}