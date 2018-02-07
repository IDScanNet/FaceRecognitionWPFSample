using AForge.Video.DirectShow;

namespace FaceTrackingWpfSample
{
    public class VideoSourceComboboxItem
    {
        private string _visibleName = "None";
        public FilterInfo FilterInfo { get; set; }
        public string VisibleName => FilterInfo?.Name ?? _visibleName;

        public override string ToString()
        {
            return VisibleName;
        }
    }
}