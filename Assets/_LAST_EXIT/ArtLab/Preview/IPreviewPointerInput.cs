using System;
using UnityEngine;

namespace LastExit.ArtLab.Preview
{
    public interface IPreviewPointerInput : IPointerActivation
    {
        public event Action<Vector2> Moved;
        public event Action<Vector2> Released;
        public event Action Canceled;
    }
}
