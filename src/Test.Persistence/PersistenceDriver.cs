namespace Test.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Caching;

    public class PersistenceDriver : IPersistenceDriver<string, string>
    {
        private readonly string _BaseDirectory = null;

        public PersistenceDriver(string baseDir)
        {
            if (String.IsNullOrEmpty(baseDir)) throw new ArgumentNullException(nameof(baseDir));
            baseDir = baseDir.Replace("\\", "/");
            if (!baseDir.EndsWith("/")) baseDir += "/";
            if (!Directory.Exists(baseDir)) Directory.CreateDirectory(baseDir);
            _BaseDirectory = baseDir;
        }

        public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            key = GenerateKey(key);
            await Task.Run(() => File.Delete(key), cancellationToken).ConfigureAwait(false);
        }

        public async Task ClearAsync(CancellationToken cancellationToken = default)
        {
            string[] files = Directory.GetFiles(_BaseDirectory, "*.*", SearchOption.TopDirectoryOnly);
            if (files != null && files.Length > 0)
            {
                foreach (string file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Run(() => File.Delete(file), cancellationToken).ConfigureAwait(false);
                }
            }
        }

        public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            key = GenerateKey(key);
            return await Task.Run(() => File.Exists(key), cancellationToken).ConfigureAwait(false);
        }

        public async Task<string> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            key = GenerateKey(key);
            return await File.ReadAllTextAsync(key, cancellationToken).ConfigureAwait(false);
        }

        public async Task WriteAsync(string key, string data, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            data ??= string.Empty;
            key = GenerateKey(key);
            await File.WriteAllTextAsync(key, data, cancellationToken).ConfigureAwait(false);
        }

        public async Task<List<string>> EnumerateAsync(CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                IEnumerable<string> files = Directory.EnumerateFiles(_BaseDirectory, "*.*", SearchOption.TopDirectoryOnly);

                if (files != null)
                {
                    List<string> updated = new();

                    foreach (string file in new List<string>(files))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        updated.Add(file.Replace(_BaseDirectory, ""));
                    }

                    files = updated;
                }

                return new List<string>(files);
            }, cancellationToken).ConfigureAwait(false);
        }

        private string GenerateKey(string key)
        {
            return _BaseDirectory + key;
        }
    }
}
