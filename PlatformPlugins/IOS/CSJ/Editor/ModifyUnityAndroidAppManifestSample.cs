using System.IO;
using System.Text;
using System.Xml;
using UnityEditor.Android;

 public class ModifyUnityAndroidAppManifestSample : IPostGenerateGradleAndroidProject
    {
        private string projectBasePath;

        public void OnPostGenerateGradleAndroidProject(string basePath)
        {
            projectBasePath = basePath;
            var androidManifest = new AndroidManifest(ManifestFilePath);
            androidManifest.SetHardwareAccel();
            androidManifest.Save();
        }

        public int callbackOrder => 1;

        private string _manifestFilePath;

        private string ManifestFilePath
        {
            get
            {
                if (string.IsNullOrEmpty(_manifestFilePath))
                {
                    var pathBuilder = new StringBuilder(projectBasePath);
                    pathBuilder.Append(Path.DirectorySeparatorChar)
                        .Append("src")
                        .Append(Path.DirectorySeparatorChar)
                        .Append("main")
                        .Append(Path.DirectorySeparatorChar)
                        .Append("AndroidManifest.xml");
                    _manifestFilePath = pathBuilder.ToString();
                }

                return _manifestFilePath;
            }
        }
    }


    internal class AndroidXmlDocument : XmlDocument
    {
        private readonly string _path;
        protected readonly XmlNamespaceManager namespaceManager;
        protected const string AndroidXmlNamespace = "http://schemas.android.com/apk/res/android";

        protected AndroidXmlDocument(string path)
        {
            _path = path;
            namespaceManager = new XmlNamespaceManager(NameTable);
            namespaceManager.AddNamespace("android", AndroidXmlNamespace);

            using (var reader = new XmlTextReader(path))
            {
                reader.Read();
                Load(reader);
            }
        }

        public sealed override void Load(XmlReader reader)
        {
            base.Load(reader);
        }

        public string Save()
        {
            using (var writer = new XmlTextWriter(_path, new UTF8Encoding(false)) {Formatting = Formatting.Indented})
            {
                Save(writer);
                return _path;
            }
        }
    }


    internal class AndroidManifest : AndroidXmlDocument
    {
        private const string LaunchActivityPath =
            "/manifest/application/activity[intent-filter/action/@android:name='android.intent.action.MAIN' and " +
            "intent-filter/category/@android:name='android.intent.category.LAUNCHER']";

        public AndroidManifest(string path) : base(path)
        {
        }

        private XmlAttribute CreateAndroidAttribute(string key, string value)
        {
            var attr = CreateAttribute("android", key, AndroidXmlNamespace);
            attr.Value = value;
            return attr;
        }

        private XmlNode GetActivityWithLaunchIntent()
        {
            return SelectSingleNode(
                LaunchActivityPath, namespaceManager);
        }

        internal void SetHardwareAccel()
        {
            GetActivityWithLaunchIntent()?.Attributes?.Append(CreateAndroidAttribute("hardwareAccelerated", "true"));
        }
    }
