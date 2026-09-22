using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Inkdrop_lite.Data;
using Inkdrop_lite.Features.Attachments.Contracts;
using Inkdrop_lite.Features.Common;
using InkdropLite.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Inkdrop_lite.Features.Attachments;

public sealed partial class AttachmentService(
    AppDbContext context,
    AttachmentStorageOptions options,
    ILogger<AttachmentService> logger) : IAttachmentService
{
    private const int MaxNameLength = 128;
    private const string DefaultContentType = "application/octet-stream";

    public async Task<IReadOnlyList<AttachmentResponse>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        var attachments = await context.Attachments
            .AsNoTracking()
            .OrderByDescending(attachment => attachment.CreatedAt)
            .ToListAsync(cancellationToken);

        return attachments.Select(ToResponse).ToList();
    }

    public async Task<AttachmentResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var attachment = await context.Attachments
            .AsNoTracking()
            .FirstOrDefaultAsync(attachment => attachment.Id == id, cancellationToken);

        return attachment is null ? null : ToResponse(attachment);
    }

    public async Task<AttachmentContent?> OpenContentAsync(Guid id, CancellationToken cancellationToken)
    {
        var attachment = await context.Attachments
            .AsNoTracking()
            .FirstOrDefaultAsync(attachment => attachment.Id == id, cancellationToken);

        if (attachment is null)
        {
            return null;
        }

        var path = ResolvePath(attachment.StoragePath);

        if (!File.Exists(path))
        {
            logger.LogError("Attachment {AttachmentId} is missing its file", id);
            return null;
        }

        var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        return new AttachmentContent(ToResponse(attachment), stream);
    }

    public async Task<AttachmentResponse> UploadAsync(
        Stream content,
        string? fileName,
        string? contentType,
        CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        var name = SanitizeName(fileName);
        var relativePath = $"attachments/{id:N}{SafeExtension(name)}";
        var fullPath = ResolvePath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        try
        {
            var (length, hash) = await CopyLimitedAsync(content, fullPath, cancellationToken);

            if (length == 0)
            {
                throw new RuleViolationException("The file is empty.");
            }

            var attachment = new Attachment
            {
                Id = id,
                Name = name,
                ContentType = NormalizeContentType(contentType),
                ContentLength = length,
                StoragePath = relativePath,
                Hash = hash
            };

            context.Attachments.Add(attachment);
            await context.SaveChangesAsync(cancellationToken);

            return ToResponse(attachment);
        }
        catch
        {
            DeleteFile(fullPath);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var attachment = await context.Attachments
            .FirstOrDefaultAsync(attachment => attachment.Id == id, cancellationToken);

        if (attachment is null)
        {
            return false;
        }

        // The database clears notebook icons (SET NULL) and note links (CASCADE).
        context.Attachments.Remove(attachment);
        await context.SaveChangesAsync(cancellationToken);
        DeleteFile(ResolvePath(attachment.StoragePath));

        return true;
    }

    public async Task<IReadOnlyList<AttachmentResponse>?> GetForNoteAsync(
        Guid noteId,
        CancellationToken cancellationToken)
    {
        var note = await context.Notes
            .AsNoTracking()
            .Include(note => note.Attachments)
            .FirstOrDefaultAsync(note => note.Id == noteId, cancellationToken);

        return note?.Attachments
            .OrderBy(attachment => attachment.CreatedAt)
            .Select(ToResponse)
            .ToList();
    }

    public async Task<bool> AttachToNoteAsync(
        Guid noteId,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        var note = await context.Notes
            .Include(note => note.Attachments)
            .FirstOrDefaultAsync(note => note.Id == noteId, cancellationToken);

        if (note is null)
        {
            return false;
        }

        var attachment = await context.Attachments
            .FirstOrDefaultAsync(attachment => attachment.Id == attachmentId, cancellationToken)
            ?? throw new RuleViolationException(
                "The attachment does not exist or belongs to another user.");

        if (note.Attachments.All(existing => existing.Id != attachment.Id))
        {
            note.Attachments.Add(attachment);
            await context.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task<bool> DetachFromNoteAsync(
        Guid noteId,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        var note = await context.Notes
            .Include(note => note.Attachments)
            .FirstOrDefaultAsync(note => note.Id == noteId, cancellationToken);

        if (note is null)
        {
            return false;
        }

        var linked = note.Attachments.FirstOrDefault(existing => existing.Id == attachmentId);

        if (linked is not null)
        {
            note.Attachments.Remove(linked);
            await context.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    private async Task<(long Length, string Hash)> CopyLimitedAsync(
        Stream source,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long total = 0;

        await using var destination = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            buffer.Length,
            useAsync: true);

        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;

            if (total > options.MaxBytes)
            {
                throw new RuleViolationException(
                    $"The file is larger than the {options.MaxBytes} byte limit.",
                    StatusCodes.Status413PayloadTooLarge);
            }

            hasher.AppendData(buffer, 0, read);
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return (total, Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant());
    }

    /// <summary>Joins the relative path onto the storage root and refuses anything that escapes it.</summary>
    private string ResolvePath(string relativePath)
    {
        var root = Path.GetFullPath(options.Root);
        var full = Path.GetFullPath(Path.Combine(root, relativePath));

        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Attachment path escapes the storage root.");
        }

        return full;
    }

    private void DeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Could not delete attachment file {Path}", path);
        }
    }

    /// <summary>Keeps only the last path segment of the client's name; the name is display-only.</summary>
    private static string SanitizeName(string? fileName)
    {
        var name = Path.GetFileName((fileName ?? string.Empty).Replace('\\', '/'));
        name = new string(name.Where(character => !char.IsControl(character)).ToArray()).Trim();

        if (name.Length == 0)
        {
            return "file";
        }

        if (name.Length > MaxNameLength)
        {
            var extension = Path.GetExtension(name);
            name = extension.Length is > 0 and < 16
                ? name[..(MaxNameLength - extension.Length)] + extension
                : name[..MaxNameLength];
        }

        return name;
    }

    /// <summary>The on-disk name never uses client text except a short alphanumeric extension.</summary>
    private static string SafeExtension(string name)
    {
        var extension = Path.GetExtension(name).ToLowerInvariant();
        return ExtensionPattern().IsMatch(extension) ? extension : string.Empty;
    }

    private static string NormalizeContentType(string? contentType)
    {
        var mediaType = (contentType ?? string.Empty).Split(';')[0].Trim().ToLowerInvariant();
        return MediaTypePattern().IsMatch(mediaType) ? mediaType : DefaultContentType;
    }

    private static AttachmentResponse ToResponse(Attachment attachment) =>
        new(
            attachment.Id,
            attachment.Name,
            attachment.ContentType,
            attachment.ContentLength,
            attachment.StoragePath,
            attachment.Hash,
            attachment.CreatedAt);

    [GeneratedRegex(@"^\.[a-z0-9]{1,10}$")]
    private static partial Regex ExtensionPattern();

    [GeneratedRegex(@"^[a-z0-9][a-z0-9!#$&^_.+-]{0,126}/[a-z0-9][a-z0-9!#$&^_.+-]{0,126}$")]
    private static partial Regex MediaTypePattern();
}
