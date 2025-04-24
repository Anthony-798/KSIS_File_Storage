using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using System.Text.Json;
using System.IO;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// ����� ��� �������� ������
var storagePath = Path.Combine(Directory.GetCurrentDirectory(), "Storage");
Directory.CreateDirectory(storagePath);

// ���������� ��� ��������� ���� "/"
app.MapGet("/", () => "Welcome to the File Storage API! Use paths like /path/to/file.txt to interact with files.");

// �������� ����� (PUT)
app.MapPut("/{**path}", async (HttpContext context, string path) => {
    var filePath = Path.Combine(storagePath, path);
    var directory = Path.GetDirectoryName(filePath);
    if (directory != null) {
        Directory.CreateDirectory(directory);
    }

    // �������� �� ����������� (��������� X-Copy-From)
    if (context.Request.Headers.TryGetValue("X-Copy-From", out var copyFromPath)) {
        var sourcePath = Path.Combine(storagePath, copyFromPath.ToString().TrimStart('/'));
        if (!File.Exists(sourcePath)) {
            return Results.Json(new { message = "Source file not found" }, statusCode: 404);
        }
        bool fileExists = File.Exists(filePath); // ���������, ���������� �� ������� ����
        File.Copy(sourcePath, filePath, true);
        if (fileExists) {
            return Results.Json(new { message = "File overwritten successfully" }, statusCode: 200);
        }
        return Results.Json(new { message = "File copied successfully" }, statusCode: 201);
    }

    // ������� �������� �����
    bool fileExistsUpload = File.Exists(filePath); // ���������, ���������� �� ����
    using var stream = new FileStream(filePath, FileMode.Create);
    await context.Request.Body.CopyToAsync(stream);
    if (fileExistsUpload) {
        return Results.Json(new { message = "File updated successfully" }, statusCode: 200);
    }
    return Results.Json(new { message = "File created successfully" }, statusCode: 201);
});

// ��������� ����� ��� ������ ������ (GET)
app.MapGet("/{**path}", async (HttpContext context, string path) => {
    var filePath = Path.Combine(storagePath, path);

    // ���� ��� �����, ���������� ������ ������ (������ ��������)
    if (Directory.Exists(filePath)) {
        var files = Directory.GetFileSystemEntries(filePath)
            .Select(p => Path.GetFileName(p))
            .ToArray();
        return Results.Json(files);
    }

    // ���� ��� ����, ���������� ��� ����������
    if (File.Exists(filePath)) {
        var bytes = await File.ReadAllBytesAsync(filePath);
        return Results.Bytes(bytes, "text/plain");
    }

    // ���� �� �����, �� ����� ���, ���������� 404 � ����������
    return Results.Json(new { message = "File or directory not found" }, statusCode: 404);
});

// ��������� ���������� � ����� (HEAD)
app.MapMethods("/{**path}", new[] { "HEAD" }, (HttpContext context, string path) => {
    var filePath = Path.Combine(storagePath, path);
    if (!File.Exists(filePath)) {
        context.Response.StatusCode = 404;
        context.Response.ContentType = "application/json";
        return Results.Text("{\"message\": \"File not found\"}");
    }

    var fileInfo = new FileInfo(filePath);
    context.Response.Headers["Content-Length"] = fileInfo.Length.ToString();
    context.Response.Headers["Last-Modified"] = fileInfo.LastWriteTimeUtc.ToString("R");
    return Results.StatusCode(200);
});

// �������� ����� ��� ����� (DELETE)
app.MapDelete("/{**path}", (HttpContext context, string path) => {
    var filePath = Path.Combine(storagePath, path);
    if (File.Exists(filePath)) {
        File.Delete(filePath);
        return Results.Json(new { message = "File deleted successfully" }, statusCode: 200);
    }
    if (Directory.Exists(filePath)) {
        Directory.Delete(filePath, true);
        return Results.Json(new { message = "Directory deleted successfully" }, statusCode: 200);
    }
    return Results.Json(new { message = "File or directory not found" }, statusCode: 404);
});

app.Run();