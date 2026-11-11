using UnityEngine;
using UnityEngine.UI;

namespace LastExit.ArtLab.Preview
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class RouteEmblemGraphic : MaskableGraphic
    {
        private const string RedRoute = "R";
        private const string TurquoiseRoute = "T";
        private const string PurpleRoute = "B";
        private const int CircleSides = 24;
        private const int TriangleSides = 3;
        private const int QuadrilateralSides = 4;
        private const float SquareRadiusMultiplier = 1.2f;

        [SerializeField] private string _route = RedRoute;

        public void SetRoute(string route)
        {
            _route = route;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            Rect rect = GetPixelAdjustedRect();
            Vector2 center = rect.center;
            float radius = Mathf.Min(rect.width, rect.height) * .5f;
            int sides = GetSideCount();
            float angleOffset = _route == RedRoute ? Mathf.PI * .25f : Mathf.PI * .5f;
            float shapeRadius = _route == RedRoute ? radius * SquareRadiusMultiplier : radius;
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = center;
            helper.AddVert(vertex);

            for (int index = 0; index < sides; index++)
            {
                float angle = angleOffset + index * Mathf.PI * 2 / sides;
                vertex.position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * shapeRadius;
                helper.AddVert(vertex);
            }

            for (int index = 0; index < sides; index++)
            {
                helper.AddTriangle(0, (index + 1) % sides + 1, index + 1);
            }
        }

        private int GetSideCount()
        {
            switch (_route)
            {
                case TurquoiseRoute:
                    return CircleSides;
                case PurpleRoute:
                    return TriangleSides;
                default:
                    return QuadrilateralSides;
            }
        }
    }
}
