using System;
using System.Linq;
using LastExit.ArtLab.Preview;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace LastExit.Editor.ArtLab
{
    public static class ArtLabUxOverlay
    {
        private static readonly Color s_ink = new Color(.9f, .95f, .96f);
        private static readonly Color s_muted = new Color(.58f, .69f, .73f);
        private static readonly Color s_panel = new Color(.055f, .09f, .11f, .88f);

        public static void Build(Camera camera, Transform look, Transform[] buses, string[] routes, int[] capacities)
        {
            if (buses.Length != routes.Length || buses.Length != capacities.Length)
            {
                throw new ArgumentException("Bus, route and capacity arrays must match.");
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject root = new GameObject("UX Preview HUD", typeof(RectTransform), typeof(Canvas),
                typeof(PreviewCanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(look, false);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1080);
            scaler.matchWidthOrHeight = .5f;
            RectTransform parent = (RectTransform)root.transform;
            RectTransform guideLayer = CreateRect("Physical Path Guides", parent, Vector2.zero);
            Stretch(guideLayer);
            RectTransform badgeLayer = CreateRect("Bus And Queue Badges", parent, Vector2.zero);
            Stretch(badgeLayer);

            PreviewBus[] previewBuses = new PreviewBus[buses.Length];
            PreviewBusBadge[] badges = new PreviewBusBadge[buses.Length];

            for (int index = 0; index < buses.Length; index++)
            {
                Transform bus = buses[index];
                Renderer[] physical = bus.GetComponentsInChildren<Renderer>(true)
                    .Where(renderer => renderer.name.StartsWith("LE_BUS_", StringComparison.Ordinal)).ToArray();
                Transform door = bus.GetComponentsInChildren<Transform>(true)
                    .Single(child => child.name.EndsWith("DOOR_ANCHOR", StringComparison.Ordinal));
                PreviewBus preview = bus.gameObject.AddComponent<PreviewBus>();
                preview.Initialize(physical, door, routes[index], capacities[index]);
                previewBuses[index] = preview;
                badges[index] = CreateBusBadge(badgeLayer, preview, font, index + 1);
            }

            PreviewLaneGuide[] lanes = new PreviewLaneGuide[4];
            string[] laneRoutes = { "R", "T", "B", "G" };

            for (int index = 0; index < lanes.Length; index++)
            {
                lanes[index] = CreateLane(guideLayer, badgeLayer, index, laneRoutes[index], font);
            }

            RectTransform header = CreateHeader(parent, font);
            RectTransform footer = CreateFooter(parent, font);
            PreviewPointerInput input = root.AddComponent<PreviewPointerInput>();
            input.Initialize(camera, new[] { header, footer });
            PreviewBusDrag drag = root.AddComponent<PreviewBusDrag>();
            drag.Initialize(camera, input, previewBuses);
            Button reset = CreateReset(header, font);
            UnityEventTools.AddPersistentListener(reset.onClick, drag.ResetLayout);
            PreviewHudLayout layout = root.AddComponent<PreviewHudLayout>();
            layout.Initialize(header, footer, (RectTransform)reset.transform,
                header.GetComponentsInChildren<Text>(), footer.GetComponentsInChildren<Text>());
            ArtLabHudAssets.Configure(layout);
            ArtLabPreviewHud hud = root.AddComponent<ArtLabPreviewHud>();
            PreviewComposition composition = root.AddComponent<PreviewComposition>();
            composition.Initialize(camera, input, new[] { header, footer }, drag, previewBuses, badges, lanes, hud, layout,
                camera.GetComponent<PreviewCameraFraming>());

            GameObject eventSystem = new GameObject("UX Preview EventSystem", typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(look, false);
            eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            Canvas.ForceUpdateCanvases();
            hud.RefreshLayout();
        }

        private static PreviewBusBadge CreateBusBadge(RectTransform parent, PreviewBus bus, Font font, int row)
        {
            Image background = CreatePanel("Route " + bus.Route + " Capacity " + bus.Capacity, parent,
                new Vector2(92, 34));
            RectTransform badge = background.rectTransform;
            RouteEmblemGraphic icon = CreateEmblem(badge, bus.Route, 20);
            icon.rectTransform.anchoredPosition = new Vector2(-30, 1);
            Text occupancy = CreateText("Occupancy", badge, font, 26, "0/" + bus.Capacity, new Vector2(52, 32));
            occupancy.rectTransform.anchoredPosition = new Vector2(13, 1);
            occupancy.fontStyle = FontStyle.Bold;
            CreateEdge(badge, "Route Accent", icon.color, 0, 3);
            CreateEdge(badge, "Upper Bezel", new Color(.55f, .66f, .7f, .8f), 1, 1);
            RectTransform arrows = CreateRect("Horizontal Selection", badge, new Vector2(144, 34));

            for (int side = 0; side < 2; side++)
            {
                RouteEmblemGraphic arrow = CreateEmblem(arrows, "B", 12);
                arrow.color = s_ink;
                arrow.rectTransform.anchoredPosition = new Vector2(side == 0 ? -62 : 62, 0);
                arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, side == 0 ? 90 : -90);
            }

            arrows.gameObject.SetActive(false);
            RouteEmblemGraphic door = CreateEmblem(parent, "B", 14);
            door.name = "Selected Door Hint " + bus.Route;
            door.color = new Color(.83f, .91f, .93f, .9f);
            door.rectTransform.localRotation = Quaternion.Euler(0, 0, 180);
            Text rowLabel = CreateText("Row " + row, parent, font, 24, "R" + row, new Vector2(56, 34));
            rowLabel.color = s_muted;
            PreviewBusBadge binding = badge.gameObject.AddComponent<PreviewBusBadge>();
            binding.Initialize(background, door.rectTransform, arrows.gameObject, rowLabel.rectTransform);
            return binding;
        }

        private static PreviewLaneGuide CreateLane(RectTransform guides, RectTransform badges,
            int index, string route, Font font)
        {
            Image line = CreatePanel("Guide " + route, guides, new Vector2(2, 100));
            Image stop = CreatePanel("First Physical Body " + route, guides, new Vector2(18, 3));
            Text status = CreateText("Door Availability " + route, guides, font, 24, string.Empty,
                new Vector2(150, 32));
            status.color = new Color(.65f, 1, .75f);
            Image head = CreatePanel("Queue Head " + route, badges, new Vector2(40, 30));
            head.color = new Color(.055f, .09f, .11f, .65f);
            RouteEmblemGraphic emblem = CreateEmblem(head.rectTransform, route, 18);
            emblem.rectTransform.anchoredPosition = new Vector2(10, 0);
            Text label = CreateText("Corridor", head.rectTransform, font, 18, ((char)('A' + index)).ToString(),
                new Vector2(20, 26));
            label.rectTransform.anchoredPosition = new Vector2(-11, 0);
            CreateEdge(head.rectTransform, "Queue Accent", emblem.color, 0, 1);
            PreviewLaneGuide binding = line.gameObject.AddComponent<PreviewLaneGuide>();
            binding.Initialize(-3 + index * 2, route, line, head.rectTransform, stop, status);
            return binding;
        }

        private static RectTransform CreateHeader(RectTransform parent, Font font)
        {
            Image panel = CreatePanel("Header", parent, new Vector2(0, 100));
            RectTransform header = panel.rectTransform;
            CreateEdge(header, "Station Panel Edge", new Color(.34f, .5f, .56f, .9f), 1, 2);
            header.anchorMin = new Vector2(0, 1);
            header.anchorMax = Vector2.one;
            header.pivot = new Vector2(.5f, 1);
            Text title = CreateText("Station", header, font, 27, "L-07 / LAST EXIT", new Vector2(350, 38));
            AnchorLeft(title.rectTransform, new Vector2(18, -24));
            title.fontStyle = FontStyle.Bold;
            Text subtitle = CreateText("Preview Boundary", header, font, 15,
                "UX-ПРОБА • БЕЗ СИМУЛЯЦИИ", new Vector2(390, 26));
            AnchorLeft(subtitle.rectTransform, new Vector2(19, -57));
            subtitle.color = s_muted;
            Text counts = CreateText("Scene Counts", header, font, 16,
                "В очередях 12   /   Рядов 5", new Vector2(390, 24));
            AnchorLeft(counts.rectTransform, new Vector2(19, -82));
            return header;
        }

        private static RectTransform CreateFooter(RectTransform parent, Font font)
        {
            Image panel = CreatePanel("Footer", parent, new Vector2(0, 112));
            RectTransform footer = panel.rectTransform;
            CreateEdge(footer, "Operator Panel Edge", new Color(.34f, .5f, .56f, .9f), 1, 2);
            footer.anchorMin = Vector2.zero;
            footer.anchorMax = new Vector2(1, 0);
            footer.pivot = new Vector2(.5f, 0);
            Text hint = CreateText("Drag Hint", footer, font, 20,
                "Выберите автобус и тяните влево / вправо", new Vector2(-24, 34));
            StretchHorizontal(hint.rectTransform, 1, -23);
            Text arrivals = CreateText("Arrivals Unconfigured", footer, font, 16,
                "Следующий автобус каждого ряда: не задан", new Vector2(-24, 28));
            StretchHorizontal(arrivals.rectTransform, 1, -55);
            arrivals.color = s_muted;
            Text safety = CreateText("Safety Unavailable", footer, font, 16,
                "CCTV и карантин: недоступны в этой пробе", new Vector2(-24, 28));
            StretchHorizontal(safety.rectTransform, 1, -84);
            safety.color = s_muted;
            return footer;
        }

        private static Button CreateReset(RectTransform header, Font font)
        {
            Image background = CreatePanel("Reset Layout", header, new Vector2(130, 48));
            background.color = new Color(.15f, .23f, .27f);
            CreateEdge(background.rectTransform, "Reset Bezel", new Color(.45f, .6f, .65f), 1, 1);
            background.raycastTarget = true;
            RectTransform rect = background.rectTransform;
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(1, 1);
            rect.anchoredPosition = new Vector2(-18, -16);
            Button button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            CreateText("Reset Label", rect, font, 19, "Сбросить", new Vector2(125, 46));
            return button;
        }

        private static RouteEmblemGraphic CreateEmblem(RectTransform parent, string route, float size)
        {
            RectTransform rect = CreateRect("Emblem " + route, parent, new Vector2(size, size));
            RouteEmblemGraphic graphic = rect.gameObject.AddComponent<RouteEmblemGraphic>();
            graphic.SetRoute(route);
            string hex = route == "R" ? "#E45754" : route == "T" ? "#28BCCC" :
                route == "B" ? "#9965D5" : "#69BD70";
            ColorUtility.TryParseHtmlString(hex, out Color color);
            graphic.color = color;
            graphic.raycastTarget = false;
            return graphic;
        }

        private static Image CreatePanel(string name, RectTransform parent, Vector2 size)
        {
            RectTransform rect = CreateRect(name, parent, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = s_panel;
            image.raycastTarget = false;
            return image;
        }

        private static void CreateEdge(RectTransform parent, string name, Color color, float y, float thickness)
        {
            Image line = CreatePanel(name, parent, new Vector2(0, thickness));
            line.color = color;
            line.rectTransform.anchorMin = new Vector2(0, y);
            line.rectTransform.anchorMax = new Vector2(1, y);
            line.rectTransform.pivot = new Vector2(.5f, y);
        }

        private static Text CreateText(string name, RectTransform parent, Font font, int size,
            string text, Vector2 dimensions)
        {
            RectTransform rect = CreateRect(name, parent, dimensions);
            Text label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = size;
            label.text = text;
            label.color = s_ink;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        private static RectTransform CreateRect(string name, RectTransform parent, Vector2 size)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)child.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(.5f, .5f);
            rect.anchorMax = rect.anchorMin;
            rect.sizeDelta = size;
            return rect;
        }

        private static void AnchorLeft(RectTransform rect, Vector2 position)
        {
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(0, .5f);
            rect.anchoredPosition = position;
            rect.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;
        }

        private static void StretchHorizontal(RectTransform rect, float y, float offset)
        {
            rect.anchorMin = new Vector2(0, y);
            rect.anchorMax = new Vector2(1, y);
            rect.anchoredPosition = new Vector2(0, offset);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
