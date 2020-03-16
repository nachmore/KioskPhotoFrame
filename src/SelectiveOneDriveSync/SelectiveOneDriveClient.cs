using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel.Store;
using Windows.Storage;

namespace SelectiveOneDriveSync
{
  public class SelectiveOneDriveClient
  {
    private const int DEFAULT_SYNC_INTERVAL = 60;
    private const string DEFAULT_SYNC_FILENAME = "selective_sync.list.txt";
    private const string DEFAULT_SYNC_FOLDERNAME = "photo_kiosk_cache";

    /// <summary>
    /// Minimum sync interval in minutes
    /// </summary>
    public int SyncInterval { get; set; } = DEFAULT_SYNC_INTERVAL;
    public string FileList { get; set; } = DEFAULT_SYNC_FILENAME;

    public string SyncToFolder { get; set; } = DEFAULT_SYNC_FOLDERNAME;

    private GraphServiceClient _graphClient;
    private bool _stopSyncRequested = false;

    public SelectiveOneDriveClient()
    {
      // TODO: Creating the class should not cause this cascasing side effect of 
      //       potential UI to request a token (should probably happen as part of an
      //       actual activity)
      _graphClient = new GraphServiceClient(
        "https://graph.microsoft.com/v1.0",
        new DelegateAuthenticationProvider(
                            async (requestMessage) =>
                            {
                              //await MSALHelper.SignOut();
                              var token = await MSALHelper.AcquireToken();
                              requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", token);
                            }));
    }

    public void StartSync()
    {

      _ = MSALHelper.AcquireToken();
            
      Task.Run(OneDriveSyncTask);
    }

    private async void OneDriveSyncTask()
    {
      while (!_stopSyncRequested)
      {

        var start = DateTime.Now;

        // retrieve the file list
        var filesToSync = await ReadAllText(FileList);

        var splitLinesRe = new Regex(@"\r\n|\n|\r");

        var files = splitLinesRe.Split(filesToSync);

        // first line serves as the drive ID and driveItem ID
        var re = new Regex(@"drive:\s*?(\S+) id:\s*?([\S!]+)");
        var matches = re.Matches(files[0]);
        var driveId = matches[0].Groups[1].Value;
        var driveItemId = matches[0].Groups[2].Value;

        foreach (var file in files.Skip(1))
        {

          if (string.IsNullOrWhiteSpace(file))
            continue;

          // will stomp on files with the same name in different directories. For now, "oh well"
          Debug.WriteLine($"Syncing {file}");
          await SyncFile(driveId, driveItemId, file);
        }
        
        var timeToSleep = (int)(SyncInterval - DateTime.Now.Subtract(start).TotalMinutes);

        if (timeToSleep > 0)
          await Task.Delay(timeToSleep * 1000);
      }
    }

    private async Task SyncFile(string driveId, string driveItemId, string itemPath)
    {
      
      var fileName = Path.GetFileName(itemPath);
      var cacheFolder = await GetCacheFolder();

      if (System.IO.File.Exists(Path.Combine(cacheFolder.Path, fileName))) 
      {
        Debug.WriteLine($"file already exists, skipping: {fileName}");
        return;
      }

      var file = await cacheFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

      using (var responseStream = await _graphClient.Me.Drives[driveId].Items[driveItemId].ItemWithPath(itemPath).Content.Request().GetAsync())
      {
        using (var fileStream = await file.OpenStreamForWriteAsync())
        {
          var byteContents = (responseStream as MemoryStream).ToArray();
          fileStream.Write(byteContents, 0, byteContents.Length);

        }
      }
    }

    public async Task<StorageFolder> GetCacheFolder()
    {
      var localCacheFolder = ApplicationData.Current.LocalCacheFolder;

      return await localCacheFolder.CreateFolderAsync(SyncToFolder, CreationCollisionOption.OpenIfExists);
    }

    private async Task<string> ReadAllText(string onedriveFileName)
    {

      using (var responseStream = await _graphClient.Me.Drive.Root.ItemWithPath(onedriveFileName).Content.Request().GetAsync())
      {
        var reader = new StreamReader(responseStream);
        var rv = reader.ReadToEnd();

        return rv;
      }
    }

  }
}
