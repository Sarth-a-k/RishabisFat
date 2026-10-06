using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SunkenPrism.EditorTools
{
    /// <summary>Import only the supplied lunar-puzzle assets with small, deterministic settings.</summary>
    public sealed class LunarAssetImport : AssetPostprocessor
    {
        public const string Root = "Assets/Imported/LunarPuzzle/";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root, System.StringComparison.Ordinal)) return;
            ConfigureModel((ModelImporter)assetImporter);
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root, System.StringComparison.Ordinal)) return;
            ConfigureTexture((TextureImporter)assetImporter);
        }

        static void ConfigureModel(ModelImporter importer)
        {
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = false;
            importer.addCollider = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.None;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        }

        static void ConfigureTexture(TextureImporter importer)
        {
            bool moon = importer.assetPath.Contains("/Moon/");
            importer.textureType = moon ? TextureImporterType.Sprite : TextureImporterType.Default;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 128f;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = moon;
            importer.sRGBTexture = true;
            importer.maxTextureSize = 512;
            importer.isReadable = false;
            importer.mipmapEnabled = !moon;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.compressionQuality = 70;
            if (moon)
            {
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
            }
        }

        [MenuItem("Tools/Fourfold Citadel/Configure Lunar Puzzle Assets")]
        public static void ConfigureExisting()
        {
            foreach (string guid in AssetDatabase.FindAssets("", new[] { Root.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is ModelImporter model)
                {
                    ConfigureModel(model);
                    model.SaveAndReimport();
                }
                else if (AssetImporter.GetAtPath(path) is TextureImporter texture)
                {
                    ConfigureTexture(texture);
                    texture.SaveAndReimport();
                }
            }
            WriteReport();
        }

        public static string WriteReport()
        {
            var report = new StringBuilder();
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Root.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!prefab) continue;
                var instance = Object.Instantiate(prefab);
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                report.AppendLine(path);
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    report.AppendLine($"  {renderer.name} bounds {renderer.bounds} local {renderer.transform.localPosition} rotation {renderer.transform.localEulerAngles} scale {renderer.transform.localScale}");
                    var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    if (mesh)
                    {
                        long indices = 0;
                        for (int sub = 0; sub < mesh.subMeshCount; sub++) indices += mesh.GetIndexCount(sub);
                        report.AppendLine($"    vertices {mesh.vertexCount}; triangles {indices / 3}; submeshes {mesh.subMeshCount}");
                    }
                    foreach (var material in renderer.sharedMaterials)
                        report.AppendLine($"    material {(material ? material.name : "missing")}");
                }
                Object.DestroyImmediate(instance);
            }
            string result = report.ToString();
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../work/lunar-assets/unity-import-report.txt"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, result);
            return result;
        }
    }
}
