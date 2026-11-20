namespace LuckyDraw.Web.Services;

public enum UploadKind
{
    Image,
    Audio
}

/// <param name="Error">Câu báo lỗi (là khoá đa ngôn ngữ), có thể chứa {0} = ErrorArg.</param>
public record UploadResult(bool Ok, string? Path, string? Error, object? ErrorArg = null);

/// <summary>Lưu file upload. Hiện dùng LocalFileStorage; sau này có thể thêm SupabaseFileStorage.</summary>
public interface IFileStorage
{
    /// <param name="folder">bg | prize | logo | music | photo</param>
    Task<UploadResult> SaveAsync(IFormFile file, int eventId, string folder, UploadKind kind, long maxBytes);
    void Delete(string? relativePath);
}

/// <summary>
/// Lưu vào wwwroot/uploads/{eventId}/{folder}/{guid}.{ext}. DB chỉ lưu đường dẫn tương đối (/uploads/...).
/// Chỉ nhận .jpg .jpeg .png .webp (ảnh) và .mp3 (âm thanh); kiểm tra cả chữ ký đầu file, không tin đuôi file.
/// </summary>
public class LocalFileStorage : IFileStorage
{
    private static readonly string[] Folders = { "bg", "prize", "logo", "music", "photo" };
    private static readonly Dictionary<string, UploadKind> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = UploadKind.Image,
        [".jpeg"] = UploadKind.Image,
        [".png"] = UploadKind.Image,
        [".webp"] = UploadKind.Image,
        [".mp3"] = UploadKind.Audio
    };

    private readonly string _root;

    public LocalFileStorage(IWebHostEnvironment env)
    {
        _root = Path.Combine(env.WebRootPath, "uploads");
    }

    public async Task<UploadResult> SaveAsync(IFormFile file, int eventId, string folder, UploadKind kind, long maxBytes)
    {
        if (!Folders.Contains(folder)) throw new ArgumentException("Invalid folder", nameof(folder));
        if (file.Length == 0) return new(false, null, "File rỗng.");
        if (file.Length > maxBytes) return new(false, null, "File vượt quá {0} MB.", maxBytes / 1024 / 1024);

        var ext = Path.GetExtension(file.FileName);
        if (!Allowed.TryGetValue(ext, out var extKind) || extKind != kind)
        {
            return new(false, null, kind == UploadKind.Image
                ? "Chỉ nhận ảnh .jpg, .jpeg, .png, .webp."
                : "Chỉ nhận file âm thanh .mp3.");
        }

        var head = new byte[12];
        await using (var s = file.OpenReadStream())
        {
            _ = await s.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false);
        }
        if (!SignatureMatches(ext, head)) return new(false, null, "Nội dung file không đúng định dạng.");

        var dir = Path.Combine(_root, eventId.ToString(), folder);
        Directory.CreateDirectory(dir);
        var name = Guid.NewGuid().ToString("N") + ext.ToLowerInvariant();
        await using (var target = File.Create(Path.Combine(dir, name)))
        {
            await file.CopyToAsync(target);
        }
        return new(true, $"/uploads/{eventId}/{folder}/{name}", null);
    }

    public void Delete(string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath) || !relativePath.StartsWith("/uploads/")) return;
        var full = Path.GetFullPath(Path.Combine(_root, relativePath["/uploads/".Length..].Replace('/', Path.DirectorySeparatorChar)));
        // Chống "../" thoát ra ngoài thư mục uploads
        if (!full.StartsWith(Path.GetFullPath(_root) + Path.DirectorySeparatorChar)) return;
        try { if (File.Exists(full)) File.Delete(full); } catch (IOException) { }
    }

    private static bool SignatureMatches(string ext, byte[] h)
    {
        switch (ext.ToLowerInvariant())
        {
            case ".jpg":
            case ".jpeg":
                return h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF;
            case ".png":
                return h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47;
            case ".webp":
                return h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F' && h[8] == 'W' && h[9] == 'E' && h[10] == 'B' && h[11] == 'P';
            case ".mp3":
                // Thẻ ID3 hoặc khung MPEG (11 bit đồng bộ)
                return (h[0] == 'I' && h[1] == 'D' && h[2] == '3') || (h[0] == 0xFF && (h[1] & 0xE0) == 0xE0);
            default:
                return false;
        }
    }
}
