using System;
using System.IO;
using System.Threading;
using vrcosc_magicchatbox.Classes.DataAndSecurity;

namespace vrcosc_magicchatbox.Core.Configuration;

/// <summary>
/// Reusable atomic file write helper.
///
/// Writes are durable: payload is first written to a sibling <c>*.tmp</c> file,
/// then moved over the target with <see cref="File.Move(string, string, bool)"/>.
/// If the move fails because of a transient sharing violation (e.g. AV/indexer
/// holding the file), the operation is retried a small, bounded number of
/// times with a short backoff.
///
/// On terminal failure the temp file is cleaned up and the exception is
/// logged via <see cref="Logging"/>. The original target file is not modified
/// unless the move succeeds, so callers either see the new content or the
/// previous content — never a partially written file.
/// </summary>
internal static class AtomicFileWriter
{
    private const int MaxAttempts = 3;
    private const int InitialBackoffMs = 25;

    /// <summary>
    /// Atomically writes <paramref name="contents"/> to <paramref name="path"/>.
    /// Returns true on success, false on failure (already logged).
    /// </summary>
    public static bool WriteAllText(string path, string contents)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        contents ??= string.Empty;

        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"AtomicFileWriter: failed to create directory for '{path}': {ex.Message}");
            return false;
        }

        string tempPath = path + ".tmp";

        try
        {
            File.WriteAllText(tempPath, contents);
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"AtomicFileWriter: failed to write temp file for '{path}': {ex.Message}");
            TryDelete(tempPath);
            return false;
        }

        int backoff = InitialBackoffMs;
        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                File.Move(tempPath, path, overwrite: true);
                return true;
            }
            catch (IOException ex) when (attempt < MaxAttempts)
            {
                Logging.WriteInfo($"AtomicFileWriter: transient IO error moving temp file to '{path}' (attempt {attempt}/{MaxAttempts}): {ex.Message}");
                Thread.Sleep(backoff);
                backoff *= 2;
            }
            catch (UnauthorizedAccessException ex) when (attempt < MaxAttempts)
            {
                Logging.WriteInfo($"AtomicFileWriter: transient access error moving temp file to '{path}' (attempt {attempt}/{MaxAttempts}): {ex.Message}");
                Thread.Sleep(backoff);
                backoff *= 2;
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"AtomicFileWriter: failed to replace '{path}': {ex.Message}");
                TryDelete(tempPath);
                return false;
            }
        }

        TryDelete(tempPath);
        return false;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup; ignore.
        }
    }
}
