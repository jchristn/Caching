namespace Test.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.IO;
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

        public void Delete(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            key = GenerateKey(key);
            File.Delete(key);
        }

        public void Clear()
        {
            string[] files = Directory.GetFiles(_BaseDirectory, "*.*", SearchOption.TopDirectoryOnly);
            if (files != null && files.Length > 0)
            {
                for (int i = 0; i < files.Length; i++)
                {
                    File.Delete(files[i]);
                }
            }
        }

        public bool Exists(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            key = GenerateKey(key);
            return File.Exists(key);
        }

        public string Get(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            key = GenerateKey(key);
            return File.ReadAllText(key);
        }

        public void Write(string key, string data)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            data ??= string.Empty;
            key = GenerateKey(key);
            File.WriteAllText(key, data);
        }

        public List<string> Enumerate()
        {
            IEnumerable<string> files = Directory.EnumerateFiles(_BaseDirectory, "*.*", SearchOption.TopDirectoryOnly);

            if (files != null)
            {
                List<string> updated = new();

                foreach (string file in new List<string>(files))
                {
                    updated.Add(file.Replace(_BaseDirectory, ""));
                }

                files = updated;
            }

            return new List<string>(files);
        }

        private string GenerateKey(string key)
        {
            return _BaseDirectory + key;
        }
    }
}
