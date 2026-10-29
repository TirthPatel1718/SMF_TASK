using System.IO;

namespace StreamCopy.Services;

public class FileCopyService
{
    private CancellationTokenSource? _cts;
    private readonly ManualResetEventSlim _pauseEvent = new(true);
    private string? _currentDestFilePath;

    public bool IsBusy { get; private set; }
    public bool IsPaused { get; private set; }
    public int ProgressPercentage { get; private set; }

    public void PrepareOperation()
    {
        IsBusy = true;
        IsPaused = false;
        ProgressPercentage = 0;
        _pauseEvent.Set();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
    }

    public async Task CopyStreamAsync(Stream sourceStream, string destinationFolder, string originalFileName, long totalLength)
    {
        _currentDestFilePath = null;

        try
        {
            Directory.CreateDirectory(destinationFolder);
            _currentDestFilePath = GetUniqueDestinationPath(destinationFolder, originalFileName);

            byte[] buffer = new byte[64 * 1024]; 
            long totalBytesRead = 0;
            int bytesRead;

            using (FileStream destStream = new(_currentDestFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, _cts!.Token)) > 0)
                {
                   
                    _pauseEvent.Wait(_cts.Token);

                   
                    _cts.Token.ThrowIfCancellationRequested();

                   
                    await destStream.WriteAsync(buffer, 0, bytesRead, _cts.Token);

                    totalBytesRead += bytesRead;
                    ProgressPercentage = totalLength > 0 ? (int)((totalBytesRead * 100) / totalLength) : 100;

                    
                    await Task.Delay(40, _cts.Token);
                }
            }

            ProgressPercentage = 100;
        }
        catch (OperationCanceledException)
        {
            ProgressPercentage = 0;
            DeleteIncompleteFile();
        }
        catch (Exception)
        {
            ProgressPercentage = 0;
            DeleteIncompleteFile();
            throw;
        }
        finally
        {
            IsBusy = false;
            IsPaused = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void DeleteIncompleteFile()
    {
        try
        {
            if (!string.IsNullOrEmpty(_currentDestFilePath) && File.Exists(_currentDestFilePath))
            {
                File.Delete(_currentDestFilePath);
            }
        }
        catch
        {
            // Ignore temporary locks during abort cleanup
        }
    }

    public void Pause()
    {
        if (IsBusy && !IsPaused)
        {
            _pauseEvent.Reset();
            IsPaused = true;
        }
    }

    public void Resume()
    {
        if (IsBusy && IsPaused)
        {
            _pauseEvent.Set();
            IsPaused = false;
        }
    }

    public void Cancel()
    {
        if (IsBusy)
        {
            _pauseEvent.Set(); // Unblock loop if currently paused so token aborts
            _cts?.Cancel();
        }
    }

    private string GetUniqueDestinationPath(string folder, string fileName)
    {
        string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);
        string targetPath = Path.Combine(folder, fileName);

        int counter = 1;
        while (File.Exists(targetPath))
        {
            targetPath = Path.Combine(folder, $"{nameWithoutExt} ({counter}){extension}");
            counter++;
        }
        return targetPath;
    }
}