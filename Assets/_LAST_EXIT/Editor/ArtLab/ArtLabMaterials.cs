using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LastExit.Editor.ArtLab
{
    public static class ArtLabMaterials
    {
        private const string ShaderName = "Universal Render Pipeline/Lit";
        private const string TextureFolder = ArtLabImporter.Root + "/Textures/";

        private static readonly string[] s_routes = { "R", "T", "B", "G" };
        private static readonly string[] s_routeHex = { "#E45754", "#28BCCC", "#9965D5", "#69BD70" };

        public static IReadOnlyList<string> Routes { get; } = System.Array.AsReadOnly(s_routes);
        public static IReadOnlyList<string> RouteHex { get; } = System.Array.AsReadOnly(s_routeHex);

        public static Dictionary<string, Material> Create(ArtLabLook look)
        {
            string folder = ArtLabImporter.MaterialFolder + "/" + look;
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            Dictionary<string, Material> materials = new Dictionary<string, Material>();
            bool cold = look == ArtLabLook.ColdLiminal;
            bool warm = look == ArtLabLook.WarmIndustrial;
            materials["Floor"] = CreateSurface(folder, "Floor", cold ? "#56636B" : warm ? "#8D8675" : "#A8AFB0",
                .0f, cold ? .98f : .75f, "concrete_floor", Vector2.one);
            materials["Wall"] = CreateSurface(folder, "Wall", cold ? "#7A8991" : warm ? "#B0A28A" : "#D0D5D4",
                0, .6f, "concrete_wall", new Vector2(2, 1));
            materials["WetFloor"] = CreateSurface(folder, "WetFloor", "#3C4B54", .12f, .87f,
                null, Vector2.one);
            materials["WetFloor"].SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(
                TextureFolder + "concrete_floor_base_color.png"));
            materials["Steel"] = CreateSurface(folder, "Steel", "#A3ABB0", .65f, warm ? .9f : .65f,
                "aged_steel", Vector2.one);
            materials["Body"] = CreateSurface(folder, "Body", cold ? "#5B6870" : warm ? "#8A887E" : "#AEB8BA",
                .35f, .5f, null, Vector2.one);
            materials["Dark"] = CreateSurface(folder, "Dark", "#242F35", .15f, .25f, null, Vector2.one);
            materials["Glass"] = CreateSurface(folder, "Glass", cold ? "#263C45" : "#202B30", .08f, .82f,
                null, Vector2.one);
            materials["Rubber"] = CreateSurface(folder, "Rubber", "#697176", 0, .7f, "rubber_dark", Vector2.one);
            materials["Marking"] = CreateSurface(folder, "Marking", "#9CA7A5", 0, .1f, null, Vector2.one);
            materials["Hazard"] = CreateSurface(folder, "Hazard", "#CFB86C", 0, .15f, null, Vector2.one);
            materials["Screen"] = CreateSurface(folder, "Screen", "#2B8B9B", 0, .3f, null, Vector2.one);
            SetEmission(materials["Screen"], "#66D7DA", .7f);
            materials["Lamp"] = CreateSurface(folder, "Lamp", warm ? "#E7D8B8" : "#C7E0E0", 0, .2f,
                null, Vector2.one);
            SetEmission(materials["Lamp"], warm ? "#FFE1A7" : "#C7F0F2", 1.2f);
            materials["Alert"] = CreateSurface(folder, "Alert", "#BF514B", 0, .2f, null, Vector2.one);
            SetEmission(materials["Alert"], "#ED6961", .6f);

            for (int index = 0; index < Routes.Count; index++)
            {
                string route = Routes[index];
                materials["Route_" + route] = CreateSurface(folder, "Route_" + route, RouteHex[index], .12f, .45f,
                    null, Vector2.one);
                Material emblem = CreateSurface(folder, "Emblem_" + route, "#FFFFFF", 0, .2f, null, Vector2.one);
                emblem.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder +
                    "route_emblem_atlas.png"));
                emblem.SetTextureOffset("_BaseMap", new Vector2(index % 2 * .5f, index / 2 * -.5f));
                materials["Emblem_" + route] = emblem;
            }

            AssetDatabase.SaveAssets();
            return materials;
        }

        public static void Apply(GameObject instance, Dictionary<string, Material> materials, string route)
        {
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] slots = renderer.sharedMaterials;

                for (int index = 0; index < slots.Length; index++)
                {
                    string name = slots[index] == null ? string.Empty : slots[index].name;
                    string role = GetRole(name, route);
                    slots[index] = materials[role];
                }

                renderer.sharedMaterials = slots;
            }
        }

        private static string GetRole(string name, string route)
        {
            if (name.Contains("Emblem"))
            {
                return "Emblem_" + route;
            }

            if (name.Contains("Suit_ROUTE") || name.Contains("RouteAccent") || name.StartsWith("Route_"))
            {
                return "Route_" + route;
            }

            if (name.Contains("BodyNeutral") || name == "Body")
            {
                return "Body";
            }

            if (name.Contains("Glass") || name.Contains("Visor"))
            {
                return "Glass";
            }

            if (name.Contains("AgedSteel") || name == "Steel")
            {
                return "Steel";
            }

            if (name.Contains("Fasteners"))
            {
                return "Steel";
            }

            if (name.Contains("Rubber"))
            {
                return "Rubber";
            }

            if (name.Contains("Hazard"))
            {
                return "Hazard";
            }

            if (name.Contains("Screen"))
            {
                return "Screen";
            }

            if (name.Contains("Lamp"))
            {
                return "Lamp";
            }

            if (name.Contains("Alert"))
            {
                return "Alert";
            }

            return "Dark";
        }

        private static Material CreateSurface(string folder, string name, string hex, float metallic,
            float smoothness, string texture, Vector2 tiling)
        {
            string path = folder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(Shader.Find(ShaderName));
                AssetDatabase.CreateAsset(material, path);
            }

            ColorUtility.TryParseHtmlString(hex, out Color color);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Cull", 2);
            material.enableInstancing = true;

            if (texture != null)
            {
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(
                    TextureFolder + texture + "_base_color.png"));
                material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(
                    TextureFolder + texture + "_normal.png"));
                material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(
                    TextureFolder + texture + "_metallic_smoothness.png"));
                material.SetTextureScale("_BaseMap", tiling);
                material.SetFloat("_BumpScale", .3f);
                material.EnableKeyword("_NORMALMAP");
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetEmission(Material material, string hex, float strength)
        {
            ColorUtility.TryParseHtmlString(hex, out Color color);
            material.SetColor("_EmissionColor", color * strength);
            material.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(material);
        }
    }
}
