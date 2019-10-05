using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MoonSharp.Interpreter;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

using theori.Audio;
using theori.Graphics;
using theori.Graphics.OpenGL;

namespace theori.Resources
{
    [MoonSharpUserData]
    public sealed class ClientResourceManager : Disposable
    {
        abstract class AsyncResourceLoader
        {
            protected readonly ClientResourceManager m_resourceManager;
            protected readonly string m_resourcePath;

            public string ResourceName => m_resourcePath;

            protected AsyncResourceLoader(ClientResourceManager resourceManager, string resourcePath)
            {
                m_resourceManager = resourceManager;
                m_resourcePath = resourcePath;
            }

            /// <summary>
            /// Started in a separate thread to load the necessary data
            /// </summary>
            public abstract bool Load();
            /// <summary>
            /// Started on the main thread to finalize the loaded data
            /// </summary>
            /// <returns></returns>
            public abstract bool Finalize();
        }

        class BackgroundResourceLoader
        {
            private readonly AsyncResourceLoader m_loader;

            public readonly Task<bool> BackgroundTask;
            public readonly Action SuccessCallback;

            public BackgroundResourceLoader(AsyncResourceLoader loader, Action successCallback)
            {
                m_loader = loader;
                BackgroundTask = Task.Run(() => loader.Load());
                SuccessCallback = successCallback;
            }

            public bool Finalize() => m_loader.Finalize();
        }

        sealed class AsyncClientTextureLoader : AsyncResourceLoader
        {
            private readonly Texture m_resultTexture;
            private Image<Rgba32> m_image;

            public AsyncClientTextureLoader(ClientResourceManager resourceManager, string resourcePath, Texture resultTexture)
                : base(resourceManager, resourcePath)
            {
                m_resultTexture = resultTexture;
            }

            public override bool Load()
            {
                var textureStream = m_resourceManager.m_locator.OpenTextureStream(m_resourcePath, out string fileExtension);
                if (textureStream == null)
                    return false;

                using (textureStream)
                    m_image = Image.Load(textureStream);
                return true;
            }

            public override bool Finalize()
            {
                m_resultTexture.GenerateHandle();
                m_resultTexture.Create2DFromImage(m_image);

                //m_resourceManager.m_resources[m_resourcePath] = m_resultTexture;

                m_image.Dispose();
                return true;
            }
        }

        sealed class AsyncSystemTextureLoader : AsyncResourceLoader
        {
            private readonly Texture m_resultTexture;
            private readonly Stream m_textureStream;
            private readonly string m_fileExtension;

            private Image<Rgba32>? m_image;

            public AsyncSystemTextureLoader(ClientResourceManager resourceManager, Stream inputStream, string fileExtension, Texture resultTexture)
                : base(resourceManager, "<external-resource>")
            {
                m_resultTexture = resultTexture;
                m_textureStream = inputStream;
                m_fileExtension = fileExtension;
            }

            public override bool Load()
            {
                using (m_textureStream)
                    m_image = Image.Load(m_textureStream);
                return true;
            }

            public override bool Finalize()
            {
                m_resultTexture.GenerateHandle();
                m_resultTexture.Create2DFromImage(m_image!);

                m_image!.Dispose();
                return true;
            }
        }

        sealed class AsyncMaterialLoader : AsyncResourceLoader
        {
            private readonly Material m_resultMaterial;
            private readonly string[] m_sources = new string[3];

            public AsyncMaterialLoader(ClientResourceManager resourceManager, string resourcePath, Material resultMaterial)
                : base(resourceManager, resourcePath)
            {
                m_resultMaterial = resultMaterial;
            }

            public override bool Load()
            {
                // materials do NOT own their stream
                using (var vertexStream = m_resourceManager.m_locator.OpenShaderStream(m_resourcePath, ".vs", out bool missingVertex))
                using (var fragmentStream = m_resourceManager.m_locator.OpenShaderStream(m_resourcePath, ".fs", out bool missingFragment))
                using (var geometryStream = m_resourceManager.m_locator.OpenShaderStream(m_resourcePath, ".gs", out bool missingGeometry))
                {
                    if ((missingVertex || vertexStream == null) && (missingFragment || fragmentStream == null))
                    {
                        if (!missingVertex) missingVertex = vertexStream == null;
                        if (!missingFragment) missingFragment = fragmentStream == null;
                        if (missingVertex != missingFragment)
                        {
                            string kind = missingVertex ? "vertex" : "fragment";
                            Logger.Log($"Missing { kind } shader for { ResourceName }");
                        }
                        else Logger.Log($"Missing vertex and fragment shader for { ResourceName }");
                        return false;
                    }

                    m_sources[0] = new StreamReader(vertexStream).ReadToEnd();
                    m_sources[1] = new StreamReader(fragmentStream).ReadToEnd();
                    if (geometryStream != null)
                        m_sources[2] = new StreamReader(geometryStream).ReadToEnd();
                }

                return true;
            }

            public override bool Finalize()
            {
                m_resultMaterial.CreatePipeline();

                bool CreateShader(ShaderType type, int sourceIndex)
                {
                    var program = new ShaderProgram(type, m_sources[sourceIndex]);
                    if (!program || !program.Linked)
                        return false;
                    m_resultMaterial.AssignShader(program);
                    return true;
                }

                if (!CreateShader(ShaderType.Vertex, 0)) return false;
                if (!CreateShader(ShaderType.Fragment, 1)) return false;

                if (m_sources[2] != null)
                {
                    if (!CreateShader(ShaderType.Geometry, 2))
                        return false;
                }

                return true;
            }
        }

        sealed class AsyncAudioLoader : AsyncResourceLoader
        {
            private readonly AudioTrack m_resultAudio;

            public AsyncAudioLoader(ClientResourceManager resourceManager, string resourcePath, AudioTrack resultAudio)
                : base(resourceManager, resourcePath)
            {
                m_resultAudio = resultAudio;
            }

            public override bool Load()
            {
                var stream = m_resourceManager.m_locator.OpenAudioStream(m_resourcePath, out string fileExtension);
                if (stream == null) return false;

                m_resultAudio.SetSourceFromStream(stream, fileExtension);
                return true;
            }

            public override bool Finalize()
            {
                // all the work is already done, no main-thread specific stuff so we do it all up front
                return true;
            }
        }

        private readonly ClientResourceLocator m_locator;

        private readonly List<AsyncResourceLoader> m_loaders = new List<AsyncResourceLoader>();
        private readonly List<BackgroundResourceLoader> m_continuousLoaders = new List<BackgroundResourceLoader>();

        private readonly Dictionary<string, Disposable> m_resources = new Dictionary<string, Disposable>();
        private readonly List<Disposable> m_managed = new List<Disposable>();

        [MoonSharpHidden]
        public ClientResourceManager(ClientResourceLocator locator)
        {
            m_locator = locator;
        }

        protected override void DisposeManaged()
        {
            foreach (var resource in m_resources.Values)
                resource.Dispose();
            m_resources.Clear();

            foreach (var resource in m_managed)
                resource.Dispose();
            m_managed.Clear();
        }

        [MoonSharpHidden]
        public void Manage(Disposable resource)
        {
            if (m_managed.Contains(resource)) return;
            m_managed.Add(resource);
        }

        [MoonSharpHidden]
        public T Manage<T>(T resource)
            where T : Disposable
        {
            Manage((Disposable)resource);
            return resource;
        }

        public Texture QueueTextureLoad(string resourcePath)
        {
            if (m_resources.TryGetValue(resourcePath, out var resource))
                return resource as Texture;

            var resultTexture = Texture.CreateUninitialized2D();
            m_resources[resourcePath] = resultTexture;

            m_loaders.Add(new AsyncClientTextureLoader(this, resourcePath, resultTexture));
            return resultTexture;
        }

        public Material QueueMaterialLoad(string resourcePath)
        {
            if (m_resources.TryGetValue(resourcePath, out var resource))
                return resource as Material;

            var resultMaterial = Material.CreateUninitialized();
            m_resources[resourcePath] = resultMaterial;

            m_loaders.Add(new AsyncMaterialLoader(this, resourcePath, resultMaterial));
            return resultMaterial;
        }

        public AudioTrack QueueAudioLoad(string resourcePath)
        {
            if (m_resources.TryGetValue(resourcePath, out var resource))
                return resource as AudioTrack;

            var resultAudio = AudioTrack.CreateUninitialized();
            m_resources[resourcePath] = resultAudio;

            m_loaders.Add(new AsyncAudioLoader(this, resourcePath, resultAudio));
            return resultAudio;
        }

        public Texture LoadTexture(string resourcePath, Action successCallback)
        {
            if (m_resources.TryGetValue(resourcePath, out var resource))
                return resource as Texture;

            var resultTexture = Texture.CreateUninitialized2D();
            m_resources[resourcePath] = resultTexture;

            m_continuousLoaders.Add(new BackgroundResourceLoader(new AsyncClientTextureLoader(this, resourcePath, resultTexture), successCallback));
            return resultTexture;
        }

        public Texture LoadTexture(Stream inputStream, string fileExtension, Action successCallback)
        {
            var resultTexture = Texture.CreateUninitialized2D();
            m_continuousLoaders.Add(new BackgroundResourceLoader(new AsyncSystemTextureLoader(this, inputStream, fileExtension, resultTexture), successCallback));
            return resultTexture;
        }

        public Material LoadMaterial(string resourcePath, Action successCallback)
        {
            if (m_resources.TryGetValue(resourcePath, out var resource))
                return resource as Material;

            var resultMaterial = Material.CreateUninitialized();
            m_resources[resourcePath] = resultMaterial;

            m_continuousLoaders.Add(new BackgroundResourceLoader(new AsyncMaterialLoader(this, resourcePath, resultMaterial), successCallback));
            return resultMaterial;
        }

        public AudioTrack LoadAudio(string resourcePath, Action successCallback)
        {
            if (m_resources.TryGetValue(resourcePath, out var resource))
                return resource as AudioTrack;

            var resultAudio = AudioTrack.CreateUninitialized();
            m_resources[resourcePath] = resultAudio;

            m_continuousLoaders.Add(new BackgroundResourceLoader(new AsyncAudioLoader(this, resourcePath, resultAudio), successCallback));
            return resultAudio;
        }

        [MoonSharpHidden]
        public bool LoadAll()
        {
            bool success = true;
            foreach (var loader in m_loaders)
            {
                if (!loader.Load())
                {
                    Logger.Log($"Failed to load resource { loader.ResourceName }");
                    success = false;
                }
            }
            return success;
        }

        [MoonSharpHidden]
        public bool FinalizeLoad()
        {
            bool success = true;
            foreach (var loader in m_loaders)
            {
                if (!loader.Finalize())
                {
                    Logger.Log($"Failed to finalize resource { loader.ResourceName }");
                    success = false;
                }
            }
            m_loaders.Clear();
            return success;
        }

        [MoonSharpHidden]
        public void Update()
        {
            for (int i = 0; i < m_continuousLoaders.Count; i++)
            {
                var loader = m_continuousLoaders[i];
                if (loader.BackgroundTask.IsCompleted)
                {
                    m_continuousLoaders.RemoveAt(i);
                    i--;

                    if (loader.BackgroundTask.IsCompletedSuccessfully && loader.BackgroundTask.Result)
                    {
                        bool finalizeSuccessful = loader.Finalize();
                        if (finalizeSuccessful)
                            loader.SuccessCallback();
                    }
                }
            }
        }

        public Texture GetTexture(string resourcePath)
        {
            if (!m_resources.TryGetValue(resourcePath, out var resource) || resource.IsDisposed)
                return null;
            return resource as Texture;
        }

        public Material GetMaterial(string resourcePath)
        {
            if (!m_resources.TryGetValue(resourcePath, out var resource) || resource.IsDisposed)
                return null;
            return resource as Material;
        }

        public AudioTrack GetAudio(string resourcePath)
        {
            if (!m_resources.TryGetValue(resourcePath, out var resource) || resource.IsDisposed)
                return null;
            return resource as AudioTrack;
        }

        private T Aquire<T>(string resourcePath, Func<string, T> loader)
            where T : Disposable
        {
            // read: if it doesn't yet exist or has already been disposed then recreate it
            //if (!m_resources.TryGetValue(resourcePath, out var handle) || !handle.TryGetTarget(out var resource) || resource.IsDisposed)
            if (!m_resources.TryGetValue(resourcePath, out var resource) || resource.IsDisposed)
            {
                resource = loader(resourcePath);
                //m_resources[resourcePath] = new WeakReference<Disposable>(resource);
                m_resources[resourcePath] = resource;
            }

            return resource as T;
        }

        [MoonSharpHidden]
        public Texture AquireTexture(string resourcePath) => Aquire(resourcePath, LoadRawTexture);
        [MoonSharpHidden]
        public Material AquireMaterial(string resourcePath) => Aquire(resourcePath, LoadRawMaterial);

        [MoonSharpHidden]
        public Texture LoadRawTexture(string resourcePath)
        {
            var textureStream = m_locator.OpenTextureStream(resourcePath, out string fileExtension);
            if (textureStream == null)
                throw new ArgumentException($"Could not find the specified texture resource \"{ resourcePath }\".", nameof(resourcePath));

            // textures do NOT own their stream
            using (textureStream)
                return Texture.FromStream2D(textureStream);
        }

        [MoonSharpHidden]
        public Material LoadRawMaterial(string resourcePath)
        {
            // materials do NOT own their stream
            using (var vertexStream = m_locator.OpenShaderStream(resourcePath, ".vs", out bool missingVertex))
            using (var fragmentStream = m_locator.OpenShaderStream(resourcePath, ".fs", out bool missingFragment))
            using (var geometryStream = m_locator.OpenShaderStream(resourcePath, ".gs", out bool missingGeometry))
            {
                if ((missingVertex || vertexStream == null) && (missingFragment || fragmentStream == null))
                    throw new ArgumentException($"Could not find the specified material resource \"{ resourcePath }\".", nameof(resourcePath));

                return new Material(vertexStream, fragmentStream, geometryStream);
            }
        }
    }
}
