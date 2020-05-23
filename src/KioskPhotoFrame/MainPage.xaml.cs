using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using SelectiveOneDriveSync;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace KioskPhotoFrame
{
  /// <summary>
  /// An empty page that can be used on its own or navigated to within a Frame.
  /// </summary>
  public sealed partial class MainPage : Page, INotifyPropertyChanged
  {
    private const int _IMAGEFILE_QUEUE_SIZE = 6;

    public event PropertyChangedEventHandler PropertyChanged = delegate { };

    private BitmapSource _slideShowSource;
    private DispatcherTimer _pictureTimer;

    private StorageFile[] _nextImageFiles = new StorageFile[_IMAGEFILE_QUEUE_SIZE];
    private int _nextImageIndex = 0;

    private DateTime _lastFileRefresh;
    private IReadOnlyList<StorageFile> _pictureFileList;

    private SelectiveOneDriveClient _oneDriveClient;

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

      _oneDriveClient = new SelectiveOneDriveClient();
      _oneDriveClient.StartSync();

      _pictureTimer = new DispatcherTimer();
      _pictureTimer.Tick += _pictureTimer_Tick;
      _pictureTimer.Interval = new TimeSpan(0, 0, KioskConfig.SlideDurationSeconds);
      _pictureTimer.Start();

      RefreshImageQueueAsync();

      ApplicationView.GetForCurrentView().TryEnterFullScreenMode();

#if !DEBUG
      Window.Current.CoreWindow.PointerCursor = null;
#endif
    }

    private async void RefreshImageQueueAsync()
    {
      var cache = await _oneDriveClient.GetCacheFolder();
      var random = new Random();
      var lastRandom = -1;
      var nextRandom = 0;

      BasicProperties fileProperties = null;


      // we refresh the image list after the image so that this effectively runs in the background
      // instead of delaying the transition of the next image

      var start = DateTime.Now;

      // No point in thrashing the disk refreshing thousands of files every time we switch 
      // though only do this if there is a decent number of photos
      if (_pictureFileList?.Count < KioskConfig.MinRefreshCount || DateTime.Now.Subtract(_lastFileRefresh).TotalMinutes > KioskConfig.FileListRefreshMinutes)
      {
        _pictureFileList = await cache.GetFilesAsync();

        _lastFileRefresh = DateTime.Now;
      }

      // repopulate the image queue

      for (int i = 0; i < _nextImageFiles.Length; i++)
      {

        do
        {
          nextRandom = random.Next(_pictureFileList.Count);

          // ensure the file exists and that it is not 0 size (bad download, OneDrive Syncer will clean it up
          fileProperties = await _pictureFileList[nextRandom].GetBasicPropertiesAsync();

        } while (nextRandom == lastRandom || fileProperties?.Size == 0);

        _nextImageFiles[i] = _pictureFileList[nextRandom];
      }

      _lastFileRefresh = DateTime.Now;
    }

    private async void _pictureTimer_Tick(object sender, object e)
    {
      var newImage = _nextImageFiles[_nextImageIndex];

      if (newImage != null)
      {
        // increment the index (and rotate back to 0 when needed)
        _nextImageIndex = (_nextImageIndex + 1) % _nextImageFiles.Length;

        Debug.WriteLine($"{DateTime.Now}: Next slideshow image: {newImage.Name}");

        using (var stream = (FileRandomAccessStream)await newImage.OpenAsync(Windows.Storage.FileAccessMode.Read))
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

        if (_nextImageIndex == _nextImageFiles.Length / 2)
          RefreshImageQueueAsync();
      }
    }

    public void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
      this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
  }
}
