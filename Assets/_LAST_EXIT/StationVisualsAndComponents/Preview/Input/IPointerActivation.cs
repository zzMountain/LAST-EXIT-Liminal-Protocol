using System;
using UnityEngine;

namespace LastExit.ArtLab.Preview
{
    public interface IPointerActivation
    {
        public event Action<Vector2> Pressed;
    }
}
