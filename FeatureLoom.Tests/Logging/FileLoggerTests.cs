using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.Logging
{
    public class FileLoggerTests
    {
        [Fact]
        public async Task CorruptedArchive_IsDeletedAndRecreated()
        {
            string dir = Path.Combine(Path.GetTempPath(), "FileLoggerTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string logPath = Path.Combine(dir, "log.txt");
            string archivePath = Path.Combine(dir, "archive.zip");
            File.WriteAllText(logPath, "some log content");
            File.WriteAllBytes(archivePath, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });

            var logger = new FileLogger(new FileLogger.Config()
            {
                logFilePath = logPath,
                archiveFilePath = archivePath,
                newFileOnStartup = true,
                compressedArchive = true
            });

            try
            {
                var timeout = DateTime.UtcNow.AddSeconds(10);
                while (File.Exists(logPath) && DateTime.UtcNow < timeout) await Task.Delay(50);

                Assert.False(File.Exists(logPath));
                // Corrupted content was replaced by a fresh zip archive (local file header signature "PK\x03\x04").
                var bytes = File.ReadAllBytes(archivePath);
                Assert.True(bytes.Length > 4);
                Assert.Equal(new byte[] { 0x50, 0x4B, 0x03, 0x04 }, bytes.AsSpan(0, 4).ToArray());
            }
            finally
            {
                logger.Stop();
                try { Directory.Delete(dir, true); } catch { }
            }
        }
    }
}
