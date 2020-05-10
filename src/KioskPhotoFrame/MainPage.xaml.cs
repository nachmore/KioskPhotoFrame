using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
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

    private BitmapSource _slideShowSource;
    private DispatcherTimer _pictureTimer;
    private StorageFile _nextImageFile;

    public BitmapSource SlideShowSource
    {
      get { return _slideShowSource; }
      set
      {
        _slideShowSource = value;
        OnPropertyChanged();
      }
    }

    public Stretch HorizontalImageStretch
    {
      get { return KioskConfig.HorizontalImageStretch; }
      set
      {
        OnPropertyChanged();
      }
    }

    public Stretch VerticalImageStretch
    {
      get { return KioskConfig.VerticalImageStretch; }
      set
      {
        OnPropertyChanged();
      }
    }

    public MainPage()
    {
      this.InitializeComponent();

      DataContext = this;

      this.Loaded += MainPage_Loaded;
    }

    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
      HorizontalImageStretch = KioskConfig.HorizontalImageStretch;
      VerticalImageStretch = KioskConfig.VerticalImageStretch;

      var s = new SelectiveOneDriveSync.SelectiveOneDriveClient();
      s.StartSync();

      _pictureTimer = new DispatcherTimer();
      _pictureTimer.Tick += _pictureTimer_Tick;
      _pictureTimer.Interval = new TimeSpan(0, 0, KioskConfig.SlideDurationSeconds);
      _pictureTimer.Start();

      Task.Run(async () =>
      {
        var cache = await s.GetCacheFolder();
        var random = new Random();
        var lastRandom = -1;
        var nextRandom = 0;

        var lastFileRefresh = DateTime.MinValue;
        IReadOnlyList<StorageFile> files = null;
        BasicProperties fileProperties = null;

        while (true)
        {

          // we refresh the image list after the image so that this effectively runs in the background
          // instead of delaying the transition of the next image

          var start = DateTime.Now;

          // No point in thrashing the disk refreshing thousands of files every time we switch 
          // though only do this if there is a decent number of photos
          if (files?.Count < KioskConfig.MinRefreshCount || DateTime.Now.Subtract(lastFileRefresh).TotalMinutes > KioskConfig.FileListRefreshMinutes)
          {
            files = await cache.GetFilesAsync();

            lastFileRefresh = DateTime.Now;
          }

          // select the next image to load
          do
          {
            nextRandom = random.Next(files.Count);

            // ensure the file exists and that it is not 0 size (bad download, OneDrive Syncer will clean it up
            fileProperties = await files[nextRandom].GetBasicPropertiesAsync();

          } while (nextRandom == lastRandom || fileProperties?.Size == 0);

          _nextImageFile = files[nextRandom];

          // lastRandom will be -1 on startup
          if (lastRandom >= 0)
          {
            var elapsed = (int)(DateTime.Now.Subtract(start).TotalMilliseconds);
            var remainingSleep = KioskConfig.SlideDurationSeconds * 1000 - elapsed;

            // it is indeed possible for all the above to take more than the requested duration
            if (remainingSleep > 0)
              await Task.Delay(remainingSleep).ConfigureAwait(false);
          }

          lastRandom = nextRandom;
        }
      });

      ApplicationView.GetForCurrentView().TryEnterFullScreenMode();

#if !DEBUG
      Window.Current.CoreWindow.PointerCursor = null;
#endif
    }

    private async void _pictureTimer_Tick(object sender, object e)
    {

      if (_nextImageFile != null)
      {

        Debug.WriteLine($"{DateTime.Now}: Next slideshow image: {_nextImageFile.Name}");

        using (var stream = (FileRandomAccessStream)await _nextImageFile.OpenAsync(Windows.Storage.FileAccessMode.Read))
        {
          var image = new BitmapImage();

          try
          {
            await image.SetSourceAsync(stream);
            SlideShowSource = image;
          }
          catch (Exception exception)
          {
            // TODO: at some point we need better handling and tracking of exceptions...
            Debug.WriteLine("EXCEPTION LOADING IMAGE: " + exception);
          }

        }
      }
    }

    public void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
      this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
  }
}
