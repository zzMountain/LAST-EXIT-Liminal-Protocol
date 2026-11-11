using System;

namespace LastExit.ArtLab.Preview
{
    public interface IPreviewSelection
    {
        public event Action<int> SelectionChanged;

        public int SelectedIndex { get; }
    }
}
