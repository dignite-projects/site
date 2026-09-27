using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Dignite.Site.Public.Localization;

/// <summary>
/// Files held in memory, standing in for a host's template folder. <see cref="Set"/> signals the same
/// change a physical file provider would on a save, so reloading can be tested without touching disk.
/// </summary>
public class InMemoryFileProvider : IFileProvider
{
    private readonly ConcurrentDictionary<string, string> _files = new(StringComparer.Ordinal);
    private CancellationTokenSource _changeTokenSource = new();

    public void Set(string path, string content)
    {
        _files[path] = content;
        Interlocked.Exchange(ref _changeTokenSource, new CancellationTokenSource()).Cancel();
    }

    public IFileInfo GetFileInfo(string subpath)
    {
        return _files.TryGetValue(subpath, out var content)
            ? new InMemoryFileInfo(subpath, content)
            : new NotFoundFileInfo(subpath);
    }

    public IDirectoryContents GetDirectoryContents(string subpath)
    {
        var prefix = subpath.TrimEnd('/') + "/";
        var files = _files
            .Where(f => f.Key.StartsWith(prefix, StringComparison.Ordinal) && f.Key.IndexOf('/', prefix.Length) < 0)
            .Select(f => (IFileInfo)new InMemoryFileInfo(f.Key, f.Value))
            .ToList();

        return files.Count == 0 ? NotFoundDirectoryContents.Singleton : new InMemoryDirectoryContents(files);
    }

    public IChangeToken Watch(string filter)
    {
        return new CancellationChangeToken(_changeTokenSource.Token);
    }

    private class InMemoryDirectoryContents : IDirectoryContents
    {
        private readonly List<IFileInfo> _files;

        public InMemoryDirectoryContents(List<IFileInfo> files) => _files = files;

        public bool Exists => true;

        public IEnumerator<IFileInfo> GetEnumerator() => _files.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private class InMemoryFileInfo : IFileInfo
    {
        private readonly byte[] _content;

        public InMemoryFileInfo(string path, string content)
        {
            Name = path[(path.LastIndexOf('/') + 1)..];
            _content = Encoding.UTF8.GetBytes(content);
        }

        public bool Exists => true;

        public long Length => _content.Length;

        public string? PhysicalPath => null;

        public string Name { get; }

        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;

        public bool IsDirectory => false;

        public Stream CreateReadStream() => new MemoryStream(_content);
    }
}
