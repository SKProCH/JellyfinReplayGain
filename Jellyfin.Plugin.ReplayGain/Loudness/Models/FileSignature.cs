namespace Jellyfin.Plugin.ReplayGain.Loudness.Models;

public readonly record struct FileSignature(long Length, DateTime LastWriteTimeUtc) {
    public static FileSignature FromFile(string path) {
        var info = new FileInfo(path);
        return new FileSignature(info.Length, info.LastWriteTimeUtc);
    }
}
