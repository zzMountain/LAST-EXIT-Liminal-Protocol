using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LastExit.Editor.ArtLab
{
    // Shared neutral dressing for the UX study. Original modular FBX and collision footprint remain authoritative.
    public static class ArtLabVehicleDetail
    {
        public static void Build(Transform[] buses, Dictionary<string, Material> source)
        {
            Material roof = CreateMaterial("PolishRoof", source["Body"], new Color(.42f, .51f, .54f), .65f);
            Material edge = CreateMaterial("PolishEdge", source["Body"], new Color(.61f, .68f, .68f), .7f);
            Material entry = CreateMaterial("PolishEntry", source["Lamp"], new Color(.93f, .86f, .62f), .3f, true);

            foreach (Transform bus in buses)
            {
                Renderer[] renderers = bus.GetComponentsInChildren<Renderer>()
                    .Where(renderer => renderer.name.StartsWith("LE_BUS_")).ToArray();
                Bounds bounds = renderers[0].bounds;

                foreach (Renderer renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                }

                Transform door = bus.GetComponentsInChildren<Transform>()
                    .Single(child => child.name.EndsWith("DOOR_ANCHOR"));
                Transform group = new GameObject("Neutral Vehicle Detail").transform;
                group.SetParent(bus, false);
                float top = bounds.max.y;
                float z = bounds.center.z;
                float length = bounds.size.x;
                Renderer front = renderers.Single(renderer => renderer.name.EndsWith("_front"));
                bool frontAtMaximumX = front.bounds.center.x > bounds.center.x;
                float frontDirection = frontAtMaximumX ? 1 : -1;
                float frontX = frontAtMaximumX ? bounds.max.x : bounds.min.x;
                float rearX = frontAtMaximumX ? bounds.min.x + 1.0f : bounds.max.x - 1.0f;
                // Low raised cassettes break the broad roof into readable planes at phone resolution.
                CreateBox(group, "Raised Roof Cassette", new Vector3(bounds.center.x, top + .055f, z),
                    new Vector3(length - .7f, .11f, 1.25f), roof);
                CreateBox(group, "Near Roof Edge", new Vector3(bounds.center.x, top + .04f, z - .69f),
                    new Vector3(length - .6f, .06f, .055f), edge);
                CreateBox(group, "Rear Vent Housing", new Vector3(rearX, top + .17f, z - .15f),
                    new Vector3(.95f, .23f, .85f), source["Dark"]);
                CreateBox(group, "Vent Cover", new Vector3(rearX, top + .3f, z - .15f),
                    new Vector3(.85f, .035f, .76f), roof);

                for (int rib = 0; rib < 4; rib++)
                {
                    CreateBox(group, "Vent Slat " + rib, new Vector3(rearX - .3f + rib * .2f, top + .325f, z - .15f),
                        new Vector3(.055f, .018f, .65f), source["Dark"]);
                }

                // A dark roof brow identifies the cabin end without adding a second door.
                CreateBox(group, "Cabin Brow", new Vector3(frontX - frontDirection * .42f, top + .12f, z),
                    new Vector3(.38f, .13f, 1.24f), source["Glass"]);
                CreateBox(group, "Cabin Leading Rim", new Vector3(frontX - frontDirection * .19f, top + .05f, z),
                    new Vector3(.065f, .08f, 1.3f), edge);

                // Entry is attached to the actual left-side door anchor, on the queue-facing side only.
                float entryZ = bounds.max.z + .12f;
                CreateBox(group, "Entry Step", new Vector3(door.position.x, bounds.min.y + .12f, entryZ),
                    new Vector3(.92f, .16f, .42f), source["Dark"]);
                CreateBox(group, "Entry Threshold Light", new Vector3(door.position.x, bounds.min.y + .21f, entryZ + .12f),
                    new Vector3(.85f, .035f, .11f), entry);
                CreateBox(group, "Entry Canopy", new Vector3(door.position.x, top + .13f, entryZ - .1f),
                    new Vector3(1.02f, .12f, .38f), source["Dark"]);
                CreateBox(group, "Entry Canopy Light", new Vector3(door.position.x, top + .2f, entryZ - .08f),
                    new Vector3(.82f, .035f, .16f), entry);

                for (int side = -1; side <= 1; side += 2)
                {
                    CreateBox(group, "Entry Jamb " + side,
                        new Vector3(door.position.x + side * .47f, bounds.center.y, bounds.max.z + .02f),
                        new Vector3(.055f, bounds.size.y - .25f, .055f), edge);
                }
            }
        }

        private static Material CreateMaterial(string name, Material source, Color color, float smoothness,
            bool emissive = false)
        {
            string path = "Assets/_LAST_EXIT/ArtLab/Rendering/" + name + ".mat";
            Material result = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (result != null)
            {
                return result;
            }

            result = new Material(source);
            result.SetColor("_BaseColor", color);
            result.SetFloat("_Smoothness", smoothness);

            if (emissive)
            {
                result.EnableKeyword("_EMISSION");
                result.SetColor("_EmissionColor", new Color(.93f, .84f, .56f) * .65f);
            }

            AssetDatabase.CreateAsset(result, path);
            return result;
        }

        private static void CreateBox(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, true);
            part.transform.position = position;
            part.transform.localScale = size;
            part.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(part.GetComponent<Collider>());
        }
    }
}
