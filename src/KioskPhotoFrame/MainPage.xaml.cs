using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace KioskPhotoFrame
{
  /// <summary>
  /// An empty page that can be used on its own or navigated to within a Frame.
  /// </summary>
  public sealed partial class MainPage : Page, INotifyPropertyChanged
  {
    public event PropertyChangedEventHandler PropertyChanged = delegate { };

    private BitmapImage _slideShowSource;
    public BitmapImage SlideShowSource
    {
      get { return _slideShowSource; }
      set
      {
        _slideShowSource = value;
        OnPropertyChanged();
      }
    }

    public MainPage()
    {
      this.InitializeComponent();

      this.Loaded += MainPage_Loaded;
    }

    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {

      var s = new SelectiveOneDriveSync.SelectiveOneDriveClient();
      s.StartSync();
     
      Task.Run(async () =>
      {
        var cache = await s.GetCacheFolder();
        var random = new Random();
        var lastRandom = 0;
        var nextRandom = 0;

        var lastFileRefresh = DateTime.MinValue;
        IReadOnlyList<StorageFile> files = null;

        while (true)
        {

          // No point in thrashing the disk refreshing thousands of files every time we switch 
          // though only do this if there is a decent number of photos
          if (files?.Count < KioskConfig.MinRefreshCount || DateTime.Now.Subtract(lastFileRefresh).TotalMinutes > KioskConfig.FileListRefreshMinutes)
          {
            files = await cache.GetFilesAsync();

            lastFileRefresh = DateTime.Now;
          }

          if (files.Count > 0)
          {
            do
            {
              nextRandom = random.Next(files.Count);
            } while (nextRandom == lastRandom);

            lastRandom = nextRandom;

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
              using (var stream = (FileRandomAccessStream)await files[nextRandom].OpenAsync(Windows.Storage.FileAccessMode.Read))
              {
                var bitmapImage = new BitmapImage();
                bitmapImage.SetSource(stream);
                SlideShowSource = bitmapImage;
              }
            });
          }

          await Task.Delay(KioskConfig.SlideDurationSeconds * 1000).ConfigureAwait(false);
        }
      });

      DataContext = this;
      
      ApplicationView.GetForCurrentView().TryEnterFullScreenMode();

      Window.Current.CoreWindow.PointerCursor = null;
    }

    public void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
      this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
  }
}
