using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace QuestAI.EditorTools
{
    /// <summary>
    /// Dois problemas de build Android corrigidos aqui, via
    /// IPostGenerateGradleAndroidProject (roda em todo build, depois que o
    /// Unity já exportou o projeto Gradle completo — não depende de editar
    /// nada em Library/PackageCache nem manter um AndroidManifest.xml
    /// customizado em Assets/Plugins/Android substituindo o principal):
    ///
    /// 1) xrmanifest.androidlib (gerado pelo pacote com.unity.xr.management
    ///    4.5.0) traz package="com.UnityTechnologies.XR.Manifest" no
    ///    manifest — forma deprecada, rejeitada pelo AGP atual.
    ///
    /// 2) O manifest PRINCIPAL (unityLibrary/src/main/AndroidManifest.xml,
    ///    gerado inteiramente pelo Unity com todas as activities corretas)
    ///    precisa de INTERNET, RECORD_AUDIO e usesCleartextTraffic=true sem
    ///    que a gente toque nas activities/estrutura que o Unity já gerou —
    ///    edição feita via XmlDocument (não substituição de texto), então
    ///    só adiciona o que falta e nunca duplica.
    /// </summary>
    public class QuestAIXrManifestNamespaceFix : IPostGenerateGradleAndroidProject
    {
        private const string XrManifestModuleName = "xrmanifest.androidlib";
        private const string XrNamespace = "com.UnityTechnologies.XR.Manifest";
        private const string AndroidNs = "http://schemas.android.com/apk/res/android";

        // Rodar depois dos callbacks do OpenXR/Meta/XR Management, que podem
        // regenerar o manifest e apagar permissões adicionadas anteriormente.
        public int callbackOrder => int.MaxValue;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string moduleName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            if (moduleName == XrManifestModuleName)
            {
                FixXrManifestPackageAttribute(Path.Combine(path, "AndroidManifest.xml"));
                EnsureNamespaceInBuildGradle(Path.Combine(path, "build.gradle"));
                return;
            }

            // Unity chama este callback com a raiz do projeto Gradle. O manifest
            // principal fica em unityLibrary/src/main (e não em src/main). Mantemos
            // o segundo candidato para builds/exportações que chamem por módulo.
            string[] mainManifestCandidates =
            {
                Path.Combine(path, "unityLibrary", "src", "main", "AndroidManifest.xml"),
                Path.Combine(path, "src", "main", "AndroidManifest.xml")
            };
            foreach (string mainManifestPath in mainManifestCandidates)
            {
                if (File.Exists(mainManifestPath))
                {
                    PatchMainManifest(mainManifestPath);
                    return;
                }
            }

            Debug.LogWarning($"[QuestAI] Manifest principal não encontrado sob o projeto Gradle: {path}");
        }

        // --- xrmanifest.androidlib: remove atributo package= deprecado ---

        private static void FixXrManifestPackageAttribute(string manifestPath)
        {
            if (!File.Exists(manifestPath))
                return;

            string content = File.ReadAllText(manifestPath);
            string updated = Regex.Replace(content, @"\s+package=""[^""]*""", string.Empty);

            if (updated != content)
            {
                File.WriteAllText(manifestPath, updated);
                Debug.Log($"[QuestAI] Removido atributo package= de {manifestPath}");
            }
        }

        private static void EnsureNamespaceInBuildGradle(string buildGradlePath)
        {
            if (!File.Exists(buildGradlePath))
                return;

            string content = File.ReadAllText(buildGradlePath);
            if (content.Contains("namespace"))
                return; // já configurado (por este hook ou pelo próprio Unity).

            string updated = Regex.Replace(
                content,
                @"android\s*\{",
                $"android {{\n    namespace '{XrNamespace}'",
                RegexOptions.Singleline
            );

            if (updated != content)
            {
                File.WriteAllText(buildGradlePath, updated);
                Debug.Log($"[QuestAI] Adicionado namespace '{XrNamespace}' em {buildGradlePath}");
            }
        }

        // --- Manifest principal: injeta permissões + usesCleartextTraffic ---
        // Edição via XmlDocument: só adiciona o que falta, nunca toca em
        // <activity> nem em qualquer outro elemento já existente.

        private static void PatchMainManifest(string manifestPath)
        {
            var doc = new XmlDocument();
            doc.Load(manifestPath);

            XmlElement manifestRoot = doc.DocumentElement;
            if (manifestRoot == null || manifestRoot.Name != "manifest")
                return;

            bool changed = false;
            changed |= EnsureUsesPermission(doc, manifestRoot, "android.permission.INTERNET");
            changed |= EnsureUsesPermission(doc, manifestRoot, "android.permission.RECORD_AUDIO");
            changed |= EnsureCleartextTraffic(doc, manifestRoot);

            if (changed)
            {
                doc.Save(manifestPath);
                Debug.Log($"[QuestAI] Manifest principal ajustado (permissões/cleartext): {manifestPath}");
            }
        }

        private static bool EnsureUsesPermission(XmlDocument doc, XmlElement manifestRoot, string permissionName)
        {
            foreach (XmlNode node in manifestRoot.ChildNodes)
            {
                if (node is XmlElement element &&
                    element.Name == "uses-permission" &&
                    element.GetAttribute("name", AndroidNs) == permissionName)
                {
                    return false; // já existe — idempotente, não duplica.
                }
            }

            XmlElement newElement = doc.CreateElement("uses-permission");
            XmlAttribute nameAttribute = doc.CreateAttribute("android", "name", AndroidNs);
            nameAttribute.Value = permissionName;
            newElement.Attributes.Append(nameAttribute);

            // Permissões ficam no topo do manifest, antes de <application>, por convenção.
            XmlNode applicationNode = FindApplicationElement(manifestRoot);
            manifestRoot.InsertBefore(newElement, applicationNode);
            return true;
        }

        private static bool EnsureCleartextTraffic(XmlDocument doc, XmlElement manifestRoot)
        {
            XmlElement applicationElement = FindApplicationElement(manifestRoot);
            if (applicationElement == null)
                return false; // Unity sempre gera <application> — defensivo apenas.

            string current = applicationElement.GetAttribute("usesCleartextTraffic", AndroidNs);
            if (current == "true")
                return false; // já garantido — idempotente.

            XmlAttribute attribute = doc.CreateAttribute("android", "usesCleartextTraffic", AndroidNs);
            attribute.Value = "true";
            applicationElement.Attributes.SetNamedItem(attribute); // substitui só este atributo se já existir com outro valor.
            return true;
        }

        private static XmlElement FindApplicationElement(XmlElement manifestRoot)
        {
            foreach (XmlNode node in manifestRoot.ChildNodes)
            {
                if (node is XmlElement element && element.Name == "application")
                    return element;
            }
            return null;
        }
    }
}
