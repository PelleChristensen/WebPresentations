#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DenmarkMap.EditorTools
{
    /// <summary>
    /// Tools > Denmark Map > Build Map Prefab
    /// Configures the tile FBX importers, creates materials and assembles the DenmarkMap prefab
    /// (80 tiles with LODGroups + water/base board) with a MapLODDistance component.
    /// Expected layout: Assets/DenmarkMap/Models/*.fbx, Assets/DenmarkMap/Shaders/DenmarkMapVertexColorLit.shader
    /// </summary>
    public static class DenmarkMapBuilder
    {
        const string Root = "Assets/DenmarkMap";
        const string Models = Root + "/Models";
        const string Mats = Root + "/Materials";
        const string Prefabs = Root + "/Prefabs";

        [MenuItem("Tools/Denmark Map/Build Map Prefab")]
        public static void Build()
        {
            Directory.CreateDirectory(Mats);
            Directory.CreateDirectory(Prefabs);

            var fbxGuids = AssetDatabase.FindAssets("t:Model", new[] { Models });
            var paths = fbxGuids.Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith(".fbx")).OrderBy(p => p).ToArray();
            if (paths.Length == 0) { Debug.LogError("[DenmarkMap] No FBX files in " + Models); return; }

            // 1. importer settings
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var p in paths)
                {
                    var mi = (ModelImporter)AssetImporter.GetAtPath(p);
                    bool dirty = mi.materialImportMode != ModelImporterMaterialImportMode.None || mi.importNormals != ModelImporterNormals.Import
                                 || mi.importAnimation || mi.importCameras || mi.importLights || mi.isReadable;
                    mi.materialImportMode = ModelImporterMaterialImportMode.None;
                    mi.importNormals = ModelImporterNormals.Import;
                    mi.importTangents = ModelImporterTangents.None;
                    mi.importAnimation = false;
                    mi.animationType = ModelImporterAnimationType.None;
                    mi.importCameras = false;
                    mi.importLights = false;
                    mi.isReadable = false;
                    mi.addCollider = false;
                    mi.meshCompression = ModelImporterMeshCompression.Off;
                    mi.indexFormat = ModelImporterIndexFormat.Auto;
                    if (dirty) mi.SaveAndReimport();
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }

            // 2. materials
            var vc = GetOrCreateMaterial("Map_VertexColor", Shader.Find("DenmarkMap/VertexColorLit"));
            var water = GetOrCreateMaterial("Map_Water", Shader.Find("Universal Render Pipeline/Lit"), m =>
            {
                m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
                m.SetColor("_BaseColor", new Color(0.10f, 0.42f, 0.55f, 0.42f));
                m.SetFloat("_Smoothness", 0.92f);
            });
            var wood = GetOrCreateMaterial("Map_Base", Shader.Find("Universal Render Pipeline/Lit"), m =>
            {
                m.SetColor("_BaseColor", new Color(0.30f, 0.20f, 0.13f));
                m.SetFloat("_Smoothness", 0.35f);
            });

            // 3. assemble
            var root = new GameObject("DenmarkMap");
            foreach (var p in paths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, root.transform);
                go.transform.localPosition = Vector3.zero;
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    string n = r.gameObject.name;
                    var m = n.StartsWith("Water") ? water : (n.StartsWith("Base") || n.StartsWith("InsetFrame")) ? wood : vc;
                    r.sharedMaterials = Enumerable.Repeat(m, r.sharedMaterials.Length).ToArray();
                    r.shadowCastingMode = n.StartsWith("Water") ? ShadowCastingMode.Off : ShadowCastingMode.On;
                }
                if (go.name.StartsWith("Tile_")) EnsureLodGroup(go);
            }

            // 4. orientation check: north row (Tile_09_*) should be +Z, east column (Tile_*_07) +X
            Vector3 South = Centre(root, "Tile_00_"), North = Centre(root, "Tile_09_");
            if (North.z < South.z) root.transform.rotation = Quaternion.Euler(0, 180, 0) * root.transform.rotation;
            Vector3 West = Centre(root, "_00", true), East = Centre(root, "_07", true);
            if (East.x < West.x) Debug.LogWarning("[DenmarkMap] Map appears mirrored on X - check FBX axis settings.");

            var lod = root.AddComponent<DenmarkMap.MapLODDistance>();
            lod.lod0Distance = 0.8f; lod.lod1Distance = 1.4f;

            string prefabPath = Prefabs + "/DenmarkMap.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.UserAction);
            Selection.activeGameObject = root;
            Debug.Log($"[DenmarkMap] Built {prefabPath} from {paths.Length} models. North={North} South={South}");
        }

        static Vector3 Centre(GameObject root, string key, bool suffix = false)
        {
            var rs = root.GetComponentsInChildren<Renderer>().Where(r =>
            {
                var t = r.transform.parent != null ? r.transform.parent.name : r.name;
                return suffix ? t.StartsWith("Tile_") && t.EndsWith(key) : t.StartsWith(key);
            }).ToArray();
            if (rs.Length == 0) return Vector3.zero;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            return b.center;
        }

        static void EnsureLodGroup(GameObject tile)
        {
            var g = tile.GetComponent<LODGroup>();
            if (g != null && g.lodCount == 3) return;
            if (g == null) g = tile.AddComponent<LODGroup>();
            var lods = new LOD[3];
            float[] h = { 0.15f, 0.08f, 0.0001f };
            for (int i = 0; i < 3; i++)
            {
                var child = tile.transform.Cast<Transform>().FirstOrDefault(t => t.name.EndsWith("_LOD" + i));
                lods[i] = new LOD(h[i], child != null ? child.GetComponentsInChildren<Renderer>() : new Renderer[0]);
            }
            g.SetLODs(lods);
            g.RecalculateBounds();
        }

        static Material GetOrCreateMaterial(string name, Shader shader, System.Action<Material> setup = null)
        {
            string path = $"{Mats}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            if (shader == null) { Debug.LogError("[DenmarkMap] Shader missing for " + name); shader = Shader.Find("Universal Render Pipeline/Lit"); }
            m = new Material(shader) { name = name };
            setup?.Invoke(m);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
#endif
