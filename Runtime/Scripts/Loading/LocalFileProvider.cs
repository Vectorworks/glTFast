// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;

namespace GLTFast.Loading {

    /// <summary>
    /// Default <see cref="IDownloadProvider"/> implementation
    /// </summary>
    public class LocalFileProvider : IDownloadProvider {
        public async Task<IDownload> Request(Uri url)
        {
            var req = new FileLoad(url);
            while (req.MoveNext())
            {
                await Task.Yield();
            }
            req.Close();
            return req;
        }

        public async Task<ITextureDownload> RequestTexture(Uri url, bool nonReadable)
        {
#if UNITY_WEBREQUEST_TEXTURE
            var req = new AwaitableTextureDownload(url, nonReadable);
            await req.WaitAsync();
            return req;
#else
            return null;
#endif
        }
    }

    public class FileLoad : IDownload {

        protected const Int32 bufferSize = 32 * 4096;
        protected FileStream fileStream;

        protected string path;
        protected int length;
        protected int sumLoaded;
        protected byte[] bytes;
        protected string readError = null;

        public FileLoad() { }

        public FileLoad(Uri url)
        {
            if (url.Scheme != "file")
            {
                throw new ArgumentException("[FileLoad] FileLoad can only load uris starting with file:");
            }
            path = url.LocalPath;
            if (!File.Exists(path))
            {
                throw new ArgumentException("[FileLoad] File " + url.LocalPath + " does not exist!");
            }
            Init();
        }

        public void Close()
        {
            Debug.Log("[FileLoad] Closing " + fileStream);
            fileStream.Close();
        }

        protected void Init()
        {
            Debug.Log("[FileLoad] Opening " + path);
            fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
            length = (int)fileStream.Length;
            sumLoaded = 0;
            bytes = new byte[length];
        }

        public object Current { get { return fileStream; } }
        public bool MoveNext()
        {
            if (fileStream == null)
            {
                Debug.LogWarning("[FileLoad] filestream is null, this shouldn't happend!");
                return false;
            }

            if (Success)
                return false;

            try
            {
                var readSize = Math.Min(length - sumLoaded, bufferSize);
                var count = fileStream.Read(bytes, sumLoaded, readSize);
                sumLoaded += count;
                return count > 0;
            }
            catch (Exception e)
            {
                readError = e.Message;
                Debug.LogError("[FileLoad] error: " + readError);
                throw e;
            }
        }

        public void Reset()
        {
            fileStream.Dispose();
            fileStream = null;
            path = String.Empty;
            length = 0;
            sumLoaded = 0;
            bytes = null;
            readError = null;
        }

        public void Dispose()
        {
            fileStream.Dispose();
            fileStream = null;
        }

        public bool? isBinary {
            get {
                if (Success)
                {
                    return path.EndsWith(".glb");
                }
                else
                {
                    return null;
                }
            }
        }

        public bool Success => sumLoaded >= length;

        public string Error => readError;

        public byte[] Data => bytes;

        public string Text => System.Text.Encoding.UTF8.GetString(bytes);

        public bool? IsBinary => isBinary;
    }

    public class AwaitableTextureLoad : AwaitableDownload, ITextureDownload {

        public AwaitableTextureLoad() : base() { }
        public AwaitableTextureLoad(Uri url) : base(url) { }

        public AwaitableTextureLoad(Uri url, bool nonReadable)
        {
            Init(url, nonReadable);
        }

        protected static UnityWebRequest CreateRequest(Uri url, bool nonReadable)
        {
            return UnityWebRequestTexture.GetTexture(url, nonReadable);
        }

        protected void Init(Uri url, bool nonReadable)
        {
            m_Request = CreateRequest(url, nonReadable);
            m_AsyncOperation = m_Request.SendWebRequest();
        }

        public Texture2D Texture {
            get {
                return (m_Request.downloadHandler as DownloadHandlerTexture).texture;
            }
        }
    }
}
