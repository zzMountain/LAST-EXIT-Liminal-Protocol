using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastExit.Editor.ArtLab
{
    public static class ArtLabSetDressing
    {
        public static void Build(Transform parent, Dictionary<string, Material> materials)
        {
            Transform dressing = CreateGroup(parent, "Station Architectural Details");
            Mesh cube = GetPrimitiveMesh(PrimitiveType.Cube);
            Mesh cylinder = GetPrimitiveMesh(PrimitiveType.Cylinder);
            BuildFloor(dressing, materials, cube);
            BuildWetPatches(dressing, materials["WetFloor"]);
            BuildBackWall(dressing, materials, cube, cylinder);
            BuildQueueSigns(dressing, materials, cube);
            BuildQuarantineFrame(dressing, materials, cube);
            BuildServiceEquipment(dressing, materials, cube, cylinder);
            TiltBoards(dressing);
        }

        private static void BuildFloor(Transform parent, Dictionary<string, Material> materials, Mesh cube)
        {
            Transform floor = CreateGroup(parent, "Concrete Slabs and Expansion Joints");
            float rowLength = (ArtLabLayout.FloorBackZ - ArtLabLayout.FloorFrontZ) / 8;

            for (int column = 0; column < 6; column++)
            {
                for (int row = 0; row < 8; row++)
                {
                    CreatePart(floor, cube, "Concrete Slab " + column + "-" + row,
                        new Vector3(-7.9f + column * 3.16f, -.006f,
                            ArtLabLayout.FloorFrontZ + (row + .5f) * rowLength),
                        new Vector3(3.13f, .026f, rowLength - .03f), materials["Floor"]);
                }
            }

            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? -8.95f : 8.95f;
                float[] edgeCenters = { 8.4f, -8.3f };
                float[] edgeLengths = { 4.8f, 2 };

                for (int section = 0; section < edgeCenters.Length; section++)
                {
                    float z = edgeCenters[section];
                    float length = edgeLengths[section];
                    CreatePart(floor, cube, "Concrete Service Kerb " + side + "-" + section,
                        new Vector3(x, .12f, z), new Vector3(.42f, .24f, length), materials["Wall"]);
                    CreatePart(floor, cube, "Steel Kerb Cap " + side + "-" + section,
                        new Vector3(x, .25f, z), new Vector3(.45f, .035f, length), materials["Steel"]);
                    CreatePart(floor, cube, "Drain Cover " + side + "-" + section,
                        new Vector3(x + (side == 0 ? .4f : -.4f), .012f, z),
                        new Vector3(.28f, .026f, 1.1f), materials["Dark"]);
                }
            }

            float queueStartZ = ArtLabLayout.QueueHeadZ - .5f;
            float queueLength = ArtLabLayout.QueueSpacing * 3 + .2f;

            for (int boundary = 0; boundary < 5; boundary++)
            {
                float x = -4 + boundary * 2;
                CreatePart(floor, cube, "Queue Footing " + boundary,
                    new Vector3(x, .035f, queueStartZ + queueLength * .5f),
                    new Vector3(.2f, .05f, queueLength), materials["Dark"]);

                for (int marking = 0; marking < 5; marking++)
                {
                    CreatePart(floor, cube, "Queue Safety Dash " + boundary + "-" + marking,
                        new Vector3(x, .065f, ArtLabLayout.QueueHeadZ - .3f + marking * .83f),
                        new Vector3(.21f, .008f, .37f),
                        materials["Hazard"]);
                }
            }
        }

        private static void BuildBackWall(Transform parent, Dictionary<string, Material> materials,
            Mesh cube, Mesh cylinder)
        {
            Transform structure = CreateGroup(parent, "Concrete Structure and Conduits");

            for (int index = 0; index < 5; index++)
            {
                float x = -8 + index * 4;
                CreatePart(structure, cube, "Structural Column " + index, new Vector3(x, 2.2f, 11.13f),
                    new Vector3(.64f, 4.4f, .7f), materials["Wall"]);
                CreatePart(structure, cube, "Column Steel Shoe " + index, new Vector3(x, .39f, 10.98f),
                    new Vector3(.71f, .78f, .83f), materials["Dark"]);
                CreatePart(structure, cube, "Column Collar " + index, new Vector3(x, 3.53f, 10.96f),
                    new Vector3(.72f, .12f, .83f), materials["Steel"]);
            }

            CreatePart(structure, cube, "Upper Concrete Beam", new Vector3(0, 4.24f, 11.2f),
                new Vector3(19.3f, .36f, .8f), materials["Wall"]);
            CreatePart(structure, cube, "Wall Base Concrete Ledge", new Vector3(0, .23f, 11.02f),
                new Vector3(19.2f, .46f, .58f), materials["Wall"]);
            CreatePart(structure, cube, "Wall Base Steel Edge", new Vector3(0, .49f, 10.83f),
                new Vector3(19.2f, .055f, .25f), materials["Steel"]);

            for (int seam = 0; seam < 6; seam++)
            {
                CreatePart(structure, cube, "Wall Panel Vertical Joint " + seam,
                    new Vector3(-9 + seam * 3.6f, 2.1f, 11.48f), new Vector3(.028f, 3.5f, .02f),
                    materials["Dark"]);
            }

            for (int seam = 0; seam < 3; seam++)
            {
                CreatePart(structure, cube, "Wall Panel Horizontal Joint " + seam,
                    new Vector3(0, 1.05f + seam * 1.1f, 11.48f), new Vector3(19.2f, .025f, .02f),
                    materials["Dark"]);
            }

            for (int pipe = 0; pipe < 3; pipe++)
            {
                GameObject conduit = CreatePart(structure, cylinder, "Overhead Conduit " + pipe,
                    new Vector3(0, 4.57f + pipe * .19f, 10.85f - pipe * .23f),
                    new Vector3(.105f, 9.45f, .105f), materials["Steel"]);
                conduit.transform.localRotation = Quaternion.Euler(0, 0, 90);
            }

            for (int bracket = 0; bracket < 5; bracket++)
            {
                CreatePart(structure, cube, "Conduit Bracket " + bracket,
                    new Vector3(-8 + bracket * 4, 4.45f, 10.6f), new Vector3(.09f, .14f, .98f),
                    materials["Dark"]);
            }

            CreatePart(structure, cube, "Station ID Board", new Vector3(-.6f, 3.63f, 10.7f),
                new Vector3(3.15f, 1.05f, .16f), materials["Dark"]);
            CreateLabelAnchor(structure, "LABEL_L-07", new Vector3(-.6f, 3.63f, 10.59f));
        }

        private static void BuildQueueSigns(Transform parent, Dictionary<string, Material> materials, Mesh cube)
        {
            Transform signs = CreateGroup(parent, "Queue Signs A-D");

            for (int lane = 0; lane < 4; lane++)
            {
                float x = -3 + lane * 2;
                string letter = ((char)('A' + lane)).ToString();
                CreatePart(signs, cube, "Queue " + letter + " Sign Post",
                    new Vector3(x + .64f, 1.45f, 10.03f), new Vector3(.1f, 2.9f, .13f), materials["Steel"]);
                CreatePart(signs, cube, "Queue " + letter + " Sign Rim", new Vector3(x, 2.79f, 10.03f),
                    new Vector3(1.47f, .72f, .16f), materials["Steel"]);
                CreatePart(signs, cube, "Queue " + letter + " Sign Face", new Vector3(x, 2.79f, 9.93f),
                    new Vector3(1.36f, .61f, .045f), materials["Dark"]);
                CreatePart(signs, cube, "Queue " + letter + " Light Strip", new Vector3(x, 3.05f, 9.895f),
                    new Vector3(1.16f, .025f, .015f), materials["Lamp"]);
                CreateLabelAnchor(signs, "LABEL_QUEUE_" + letter, new Vector3(x, 2.77f, 9.895f));
            }
        }

        private static void BuildQuarantineFrame(Transform parent, Dictionary<string, Material> materials, Mesh cube)
        {
            Transform frame = CreateGroup(parent, "Q3 Isolation Architecture");
            float doorX = ArtLabLayout.Q3Position.x;
            float doorZ = ArtLabLayout.Q3Position.z;

            for (int side = 0; side < 2; side++)
            {
                float x = doorX + (side == 0 ? -1.22f : 1.22f);
                CreatePart(frame, cube, "Q3 Outer Concrete Jamb " + side, new Vector3(x, 1.5f, doorZ + .25f),
                    new Vector3(.46f, 3, .6f), materials["Wall"]);
                CreatePart(frame, cube, "Q3 Warning Light Rail " + side, new Vector3(x, 1.35f, doorZ - .09f),
                    new Vector3(.055f, 2.48f, .025f), materials["Alert"]);
            }

            CreatePart(frame, cube, "Q3 Heavy Lintel", new Vector3(doorX, 3.07f, doorZ + .25f),
                new Vector3(2.9f, .45f, .68f), materials["Wall"]);
            CreatePart(frame, cube, "Q3 Sign Rim", new Vector3(doorX, 3.64f, doorZ + .04f),
                new Vector3(2.95f, .86f, .16f), materials["Steel"]);
            CreatePart(frame, cube, "Q3 Sign Face", new Vector3(doorX, 3.64f, doorZ - .06f),
                new Vector3(2.81f, .73f, .06f), materials["Dark"]);
            CreatePart(frame, cube, "Q3 Header Light", new Vector3(doorX, 4.16f, doorZ + .04f),
                new Vector3(.39f, .17f, .18f), materials["Alert"]);
            CreatePart(frame, cube, "Q3 Floor Threshold", new Vector3(doorX, .028f, doorZ - .3f),
                new Vector3(2.35f, .05f, .58f), materials["Dark"]);
            CreateLabelAnchor(frame, "LABEL_QUARANTINE_Q-3", new Vector3(doorX, 3.64f, doorZ - .105f));
        }

        private static void BuildServiceEquipment(Transform parent, Dictionary<string, Material> materials,
            Mesh cube, Mesh cylinder)
        {
            Transform services = CreateGroup(parent, "Station Service Equipment");

            for (int cabinet = 0; cabinet < 2; cabinet++)
            {
                float x = 4.85f + cabinet * 1.04f;
                CreatePart(services, cube, "Electrical Cabinet " + cabinet, new Vector3(x, .8f, 10.8f),
                    new Vector3(.85f, 1.55f, .48f), materials["Steel"]);
                CreatePart(services, cube, "Cabinet Recess " + cabinet, new Vector3(x, .86f, 10.55f),
                    new Vector3(.72f, 1.33f, .035f), materials["Dark"]);
                CreatePart(services, cube, "Cabinet Door " + cabinet, new Vector3(x, .86f, 10.52f),
                    new Vector3(.67f, 1.27f, .035f), materials["Body"]);
                CreatePart(services, cube, "Cabinet Handle " + cabinet,
                    new Vector3(x + .23f, .84f, 10.475f), new Vector3(.04f, .24f, .045f), materials["Dark"]);

                for (int vent = 0; vent < 3; vent++)
                {
                    CreatePart(services, cube, "Cabinet Vent " + cabinet + "-" + vent,
                        new Vector3(x, .43f + vent * .1f, 10.49f), new Vector3(.46f, .025f, .015f),
                        materials["Dark"]);
                }
            }

            CreatePart(services, cube, "Service Cable Tray", new Vector3(5.9f, 2.15f, 11.02f),
                new Vector3(3.1f, .15f, .25f), materials["Dark"]);
            Vector3 cctvPosition = ArtLabLayout.CctvPosition;
            CreatePart(services, cube, "CCTV Station Header", cctvPosition + new Vector3(0, 2.6f, .8f),
                new Vector3(2.7f, .65f, .17f), materials["Dark"]);
            CreatePart(services, cube, "CCTV Header Edge", cctvPosition + new Vector3(0, 2.93f, .8f),
                new Vector3(2.75f, .045f, .21f), materials["Steel"]);
            CreateLabelAnchor(services, "LABEL_CCTV", cctvPosition + new Vector3(0, 2.6f, .69f));

            for (int support = 0; support < 2; support++)
            {
                CreatePart(services, cube, "CCTV Header Support " + support,
                    cctvPosition + new Vector3(-1.2f + support * 2.4f, 1.33f, .84f),
                    new Vector3(.1f, 2.66f, .13f),
                    materials["Steel"]);
            }

            GameObject dropPipe = CreatePart(services, cylinder, "Wall Service Drop Pipe",
                new Vector3(8.12f, 2.27f, 10.82f), new Vector3(.13f, 2.15f, .13f), materials["Steel"]);
            dropPipe.transform.localRotation = Quaternion.identity;

            Transform desk = CreateGroup(services, "Operator Console Visual Only");
            desk.localPosition = cctvPosition + new Vector3(0, 0, -.8f);
            CreatePart(desk, cube, "Desk Pedestal", new Vector3(0, .45f, 0),
                new Vector3(1.7f, .9f, .8f), materials["Dark"]);
            CreatePart(desk, cube, "Console Worktop", new Vector3(0, .93f, -.02f),
                new Vector3(1.95f, .12f, 1.02f), materials["Steel"]);
            CreatePart(desk, cube, "Monitor Enclosure", new Vector3(0, 1.43f, .23f),
                new Vector3(1.85f, .88f, .18f), materials["Dark"]);

            for (int monitor = 0; monitor < 2; monitor++)
            {
                float x = -.45f + monitor * .9f;
                CreatePart(desk, cube, "Unconnected Monitor " + monitor, new Vector3(x, 1.46f, .125f),
                    new Vector3(.79f, .64f, .025f), materials["Glass"]);
                CreatePart(desk, cube, "Monitor Status Light " + monitor, new Vector3(x, 1.08f, .12f),
                    new Vector3(.12f, .025f, .02f), materials["Screen"]);
            }

            for (int key = 0; key < 4; key++)
            {
                CreatePart(desk, cube, "Console Key " + key, new Vector3(-.54f + key * .25f, 1.01f, -.35f),
                    new Vector3(.16f, .04f, .12f), materials["Rubber"]);
            }
        }

        private static void BuildWetPatches(Transform parent, Material material)
        {
            const int Sides = 48;
            Mesh patch = new Mesh();
            patch.name = "Shallow Wet Concrete Patch";
            Vector3[] vertices = new Vector3[Sides + 1];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[Sides * 3];

            for (int index = 0; index < Sides; index++)
            {
                float angle = index * Mathf.PI * 2 / Sides;
                float radius = 1 + .07f * Mathf.Sin(angle * 5) + .04f * Mathf.Cos(angle * 9);
                vertices[index + 1] = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                uv[index + 1] = new Vector2(vertices[index + 1].x, vertices[index + 1].z) * .5f + Vector2.one * .5f;
                triangles[index * 3] = 0;
                triangles[index * 3 + 1] = (index + 1) % Sides + 1;
                triangles[index * 3 + 2] = index + 1;
            }

            patch.vertices = vertices;
            patch.uv = uv;
            patch.triangles = triangles;
            patch.RecalculateNormals();
            patch.RecalculateBounds();
            string path = ArtLabImporter.Root + "/WetConcretePatch.asset";
            Mesh saved = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(path);

            if (saved == null)
            {
                UnityEditor.AssetDatabase.CreateAsset(patch, path);
                saved = patch;
            }
            else
            {
                UnityEditor.EditorUtility.CopySerialized(patch, saved);
                Object.DestroyImmediate(patch);
            }

            Vector3[] positions =
            {
                new Vector3(-7.5f, .009f, 9.8f),
                new Vector3(7.3f, .009f, 8.2f),
                new Vector3(-6.5f, .009f, -8.3f)
            };

            for (int index = 0; index < positions.Length; index++)
            {
                GameObject instance = CreatePart(parent, saved, "Wet Patch " + index,
                    positions[index], new Vector3(.95f, 1, .45f), material);
                instance.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        private static void TiltBoards(Transform parent)
        {
            for (int lane = 0; lane < ArtLabLayout.LaneX.Count; lane++)
            {
                string letter = ((char)('A' + lane)).ToString();
                TiltBoard(parent, "Queue " + letter + " Angled Board",
                    new Vector3(ArtLabLayout.LaneX[lane], 2.79f, 10.03f),
                    new[] { "Queue " + letter + " Sign Rim", "Queue " + letter + " Sign Face",
                        "Queue " + letter + " Light Strip", "LABEL_QUEUE_" + letter });
            }

            TiltBoard(parent, "Q3 Angled Board", ArtLabLayout.Q3Position + new Vector3(0, 3.64f, .04f),
                new[] { "Q3 Sign Rim", "Q3 Sign Face", "LABEL_QUARANTINE_Q-3" });
            TiltBoard(parent, "CCTV Angled Board", ArtLabLayout.CctvPosition + new Vector3(0, 2.6f, .8f),
                new[] { "CCTV Station Header", "CCTV Header Edge", "LABEL_CCTV" });
            TiltBoard(parent, "Station Angled Board", new Vector3(-.6f, 3.63f, 10.7f),
                new[] { "Station ID Board", "LABEL_L-07" });
        }

        private static void TiltBoard(Transform parent, string name, Vector3 pivot, string[] partNames)
        {
            Transform[] parts = parent.GetComponentsInChildren<Transform>(true).Where(part =>
                partNames.Contains(part.name)).ToArray();
            Transform board = CreateGroup(parent, name);
            board.localPosition = pivot;

            foreach (Transform part in parts)
            {
                part.SetParent(board, true);
            }

            board.localRotation = Quaternion.Euler(45, 0, 0);
        }

        private static Transform CreateGroup(Transform parent, string name)
        {
            GameObject instance = new GameObject(name);
            instance.transform.SetParent(parent, false);
            return instance.transform;
        }

        private static void CreateLabelAnchor(Transform parent, string name, Vector3 position)
        {
            Transform label = CreateGroup(parent, name);
            label.localPosition = position;
        }

        private static Mesh GetPrimitiveMesh(PrimitiveType type)
        {
            GameObject temporary = GameObject.CreatePrimitive(type);
            Mesh mesh = temporary.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temporary);
            return mesh;
        }

        private static GameObject CreatePart(Transform parent, Mesh mesh, string name, Vector3 position,
            Vector3 scale, Material material)
        {
            GameObject instance = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = position;
            instance.transform.localScale = scale;
            instance.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = instance.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return instance;
        }
    }
}
