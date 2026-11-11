using UnityEngine;

namespace LastExit.ArtLab.Preview
{
    public static class PreviewBoardGeometry
    {
        public static IPreviewBoardBus GetFirstBody(IPreviewBoardBus[] buses, float laneX)
        {
            int index = GetFirstBodyIndex(buses, laneX);
            return index < 0 ? null : buses[index];
        }

        public static int GetFirstBodyIndex(IPreviewBoardBus[] buses, float laneX)
        {
            int first = -1;
            float nearestZ = float.MinValue;

            for (int index = 0; index < buses.Length; index++)
            {
                Bounds bounds = buses[index].GetPhysicalBounds();

                if (laneX >= bounds.min.x && laneX <= bounds.max.x && bounds.max.z > nearestZ)
                {
                    first = index;
                    nearestZ = bounds.max.z;
                }
            }

            return first;
        }

        public static bool IsDoorAvailable(IPreviewBoardBus bus, float laneX, string route, float tolerance = .15f)
        {
            return bus != null && Mathf.Abs(bus.DoorPosition.x - laneX) < tolerance &&
                bus.Route == route && bus.Capacity > 0;
        }
    }
}
