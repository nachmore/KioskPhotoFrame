using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Media;

namespace KioskPhotoFrame
{
  static class KioskConfig
  {

    /// <summary>
    /// Duration of each slide, in seconds
    /// </summary>
    public static int SlideDurationSeconds { get; set; } = 15;

    public static int FileListRefreshMinutes { get; set; } = 15;

    /// <summary>
    /// If there are less files than this in the cache then refresh the cache
    /// every time the slide show updates
    /// </summary>
    public static int MinRefreshCount { get; set; } = 20;
    public static Stretch HorizontalImageStretch { get; internal set; } = Stretch.UniformToFill;
    public static Stretch VerticalImageStretch { get; internal set; } = Stretch.Uniform;
  }
}
