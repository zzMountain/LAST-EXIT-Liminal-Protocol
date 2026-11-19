using UnityEngine;
using UnityEngine.UI;

namespace LastExit.Presentation
{
    // Shared presentation values; language and game state remain owned by their existing systems.
    public static class TerminalUiTheme
    {
        private static Font s_font;
        private static Font s_bodyFont;

        public static Color Background => new Color(.025f, .065f, .075f, .98f);
        public static Color Panel => new Color(.045f, .12f, .12f, .98f);
        public static Color Border => new Color(.24f, .43f, .37f, .85f);
        public static Color Accent => new Color(.57f, .82f, .68f);
        public static Color Text => new Color(.77f, .9f, .79f);
        public static Color Muted => new Color(.43f, .64f, .55f);

        public static float GetLeftPanelWidth(float screenWidth)
        {
            float minimumWidth = Mathf.Clamp(screenWidth * .23f, 128, 184);
            return Mathf.Clamp(screenWidth * .16f, minimumWidth, 224);
        }

        public static float GetSideMargin(float screenWidth)
        {
            return Mathf.Clamp((screenWidth - 600) / 16, 8, 16);
        }

        public static bool UsesStackedHud(float screenWidth, float screenHeight)
        {
            // Compare the remaining space after the two sidebars with the space
            // below the 90px header and above the 112px tutorial footer.
            float sideBoardWidth = screenWidth - 2 * GetLeftPanelWidth(screenWidth) -
                4 * GetSideMargin(screenWidth);
            float stackedBoardHeight = screenHeight - 90 - 112 - 16;
            return screenWidth < screenHeight || sideBoardWidth < stackedBoardHeight;
        }

        public static Font LoadFont(Font fallback)
        {
            if (s_font == null)
            {
                s_font = Resources.Load<Font>("UI/JetBrainsMono-Regular");
            }

            return s_font != null ? s_font : fallback;
        }

        public static void StyleButton(Button button, bool primary = false)
        {
            if (button == null)
            {
                return;
            }

            Image image = button.GetComponent<Image>();

            if (image != null)
            {
                image.color = primary ? new Color(.075f, .23f, .18f) : Panel;
                Outline outline = button.GetComponent<Outline>();

                if (outline == null)
                {
                    outline = button.gameObject.AddComponent<Outline>();
                }

                outline.effectColor = Border;
                outline.effectDistance = new Vector2(1, -1);
                outline.useGraphicAlpha = false;
            }

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.1f);
            colors.pressedColor = new Color(.72f, .88f, .76f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(.55f, .65f, .6f, .6f);
            button.colors = colors;

            foreach (Text text in button.GetComponentsInChildren<Text>(true))
            {
                StyleBodyText(text);
            }
        }

        public static void StyleBodyText(Text text)
        {
            if (s_bodyFont == null)
            {
                s_bodyFont = Resources.Load<Font>("UI/Inter-Regular");
            }

            text.font = s_bodyFont != null ? s_bodyFont : LoadFont(text.font);
            text.color = Text;
            text.fontStyle = FontStyle.Normal;
            text.raycastTarget = false;
        }

        public static void StyleText(Text text, bool muted = false)
        {
            text.font = LoadFont(text.font);
            text.color = muted ? Muted : Text;
            text.fontStyle = FontStyle.Normal;
            text.raycastTarget = false;
        }
    }
}
