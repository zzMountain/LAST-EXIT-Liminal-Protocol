using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastExit.Editor.ArtLab
{
    public static class ArtLabWayfinding
    {
        public static void Build(Transform parent, Dictionary<string, Material> materials)
        {
            GameObject group = new GameObject("Board Wayfinding");
            group.transform.SetParent(parent, false);
            Transform[] buses = parent.Cast<Transform>().Where(child => child.name.StartsWith("LE_BUS_")).ToArray();
            BuildRoads(group.transform, materials);
            BuildApproaches(group.transform, buses, materials["Marking"]);

            foreach (Transform bus in buses)
            {
                BuildDoorMarker(bus, materials["Marking"], materials["Dark"]);
            }
        }

        private static void BuildRoads(Transform parent, Dictionary<string, Material> materials)
        {
            for (int row = 0; row < ArtLabLayout.RowZ.Count; row++)
            {
                float z = ArtLabLayout.RowZ[row];
                string rowName = "R" + (row + 1);

                for (int edge = 0; edge < 2; edge++)
                {
                    float lineZ = z + (edge == 0 ? -1.4f : 1.4f);
                    CreatePart(parent, "Road " + rowName + " Edge " + edge,
                        new Vector3(0, .035f, lineZ), new Vector3(19.4f, .018f, .055f), materials["Marking"]);
                }

                for (int side = 0; side < 2; side++)
                {
                    float x = side == 0 ? -8.3f : 8.3f;
                    CreateArrow(parent, new Vector3(x, .05f, z), materials["Marking"]);
                    CreatePart(parent, "Road " + rowName + " Opening " + side,
                        new Vector3(side == 0 ? -9.6f : 9.6f, .022f, z),
                        new Vector3(.85f, .03f, 2.7f), materials["Dark"]);
                }

                CreateFloorLabel(parent, "LABEL_" + rowName, new Vector3(-6.6f, .05f, z - .7f));
            }

            CreateFloorLabel(parent, "LABEL_ВЪЕЗД", new Vector3(-8.25f, .06f, 4.2f));
            CreateFloorLabel(parent, "LABEL_ВЫЕЗД", new Vector3(8.25f, .06f, 4.2f));
        }

        private static void BuildApproaches(Transform parent, Transform[] buses, Material material)
        {
            for (int lane = 0; lane < ArtLabLayout.LaneX.Count; lane++)
            {
                float x = ArtLabLayout.LaneX[lane];
                float endZ = ArtLabLayout.FloorFrontZ;

                foreach (Transform bus in buses)
                {
                    Bounds bounds = ArtLabImporter.GetBounds(bus.gameObject);

                    if (x >= bounds.min.x - .01f && x <= bounds.max.x + .01f)
                    {
                        endZ = Mathf.Max(endZ, bounds.max.z + .18f);
                    }
                }

                string laneName = ((char)('A' + lane)).ToString();
                GameObject approach = new GameObject("Approach " + laneName);
                approach.transform.SetParent(parent, false);

                for (float z = ArtLabLayout.QueueHeadZ - .7f; z > endZ; z -= .55f)
                {
                    CreatePart(approach.transform, "Guide Dash", new Vector3(x, .045f, z),
                        new Vector3(.085f, .018f, Mathf.Min(.28f, z - endZ)), material);
                }

                CreatePart(approach.transform, "First Body Stop " + laneName, new Vector3(x, .048f, endZ),
                    new Vector3(.62f, .02f, .075f), material);
                CreatePart(approach.transform, "Head Position " + laneName,
                    new Vector3(x, .04f, ArtLabLayout.QueueHeadZ - .38f), new Vector3(.8f, .02f, .07f), material);
                CreateFloorLabel(parent, "LABEL_" + laneName,
                    new Vector3(x, .06f, ArtLabLayout.QueueHeadZ - 1.3f));
            }
        }

        private static void BuildDoorMarker(Transform bus, Material frame, Material background)
        {
            Transform anchor = bus.GetComponentsInChildren<Transform>(true).Single(child =>
                child.name.EndsWith("DOOR_ANCHOR"));
            GameObject marker = new GameObject("Door Presentation");
            marker.transform.SetParent(bus, false);
            marker.transform.position = new Vector3(anchor.position.x, 2.65f, anchor.position.z + .25f);
            marker.transform.rotation = Quaternion.identity;
            CreatePart(marker.transform, "Door Badge Frame", marker.transform.position,
                new Vector3(.66f, .04f, .5f), frame);
            CreatePart(marker.transform, "Door Badge Background", marker.transform.position + Vector3.up * .025f,
                new Vector3(.54f, .025f, .38f), background);

            for (int wing = 0; wing < 2; wing++)
            {
                float sign = wing == 0 ? -1 : 1;
                GameObject arrow = CreatePart(marker.transform, "Boarding Arrow",
                    marker.transform.position + new Vector3(sign * .09f, .045f, 0),
                    new Vector3(.065f, .015f, .28f), frame);
                arrow.transform.rotation = Quaternion.Euler(0, sign * 40, 0);
            }

            CreatePart(bus, "Boarding Threshold", new Vector3(anchor.position.x, .06f, anchor.position.z + .3f),
                new Vector3(.66f, .04f, .42f), frame);
        }

        private static void CreateArrow(Transform parent, Vector3 position, Material material)
        {
            CreatePart(parent, "Road Direction Shaft", position, new Vector3(.8f, .02f, .07f), material);

            for (int wing = 0; wing < 2; wing++)
            {
                float sign = wing == 0 ? -1 : 1;
                GameObject part = CreatePart(parent, "Road Direction Tip", position + new Vector3(.25f, 0, sign * .13f),
                    new Vector3(.42f, .02f, .07f), material);
                part.transform.rotation = Quaternion.Euler(0, sign * 40, 0);
            }
        }

        private static void CreateFloorLabel(Transform parent, string name, Vector3 position)
        {
            GameObject label = new GameObject(name);
            label.transform.SetParent(parent, false);
            label.transform.position = position;
            label.transform.rotation = Quaternion.Euler(90, 0, 0);
        }

        private static GameObject CreatePart(Transform parent, string name, Vector3 position,
            Vector3 size, Material material)
        {
            GameObject instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            instance.name = name;
            instance.transform.SetParent(parent, true);
            instance.transform.position = position;
            instance.transform.rotation = Quaternion.identity;
            instance.transform.localScale = size;
            Renderer renderer = instance.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            Object.DestroyImmediate(instance.GetComponent<Collider>());
            return instance;
        }
    }
}
