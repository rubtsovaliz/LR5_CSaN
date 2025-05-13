using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;


var builder = WebApplication.CreateBuilder();
var app = builder.Build();


static string GetMimeType(string filePath) =>
 Path.GetExtension(filePath).ToLower() switch
 {
     ".txt" => "text/plain",
     ".html" => "text/html",
     ".jpg" => "image/jpeg",
     ".png" => "image/png",
     ".pdf" => "application/pdf",
     ".zip" => "application/zip",
     _ => "application/octet-stream"
 };


var uploadDirectory = Path.Combine(Directory.GetCurrentDirectory(), "api", "file");
Directory.CreateDirectory(uploadDirectory);


app.Run(async httpContext =>
{
    var httpResponse = httpContext.Response;
    var httpRequest = httpContext.Request;
    string requestPath = httpRequest.Path;


    if (requestPath == "/api/file" && httpRequest.Method == "GET")
    {
        await ListFilesAsync(httpRequest, httpResponse, uploadDirectory);
        return;
    }


    if (requestPath.Split('/').Length <= 2)
    {
        var indexPath = Path.Combine(Directory.GetCurrentDirectory(), "html", "index.html");
        await httpResponse.SendFileAsync(indexPath);
        return;
    }


    string fileName = requestPath.Split('/')[3];
    string decodedFileName = Uri.UnescapeDataString(fileName);
    var filePath = Path.Combine(uploadDirectory, decodedFileName);


    switch (httpRequest.Method)
    {
        case "GET":
            if (!File.Exists(filePath))
            {
                httpResponse.StatusCode = 404;
                return;
            }


            httpResponse.StatusCode = 200;
            httpResponse.ContentType = GetMimeType(filePath);
            var encodedFileName = Uri.EscapeDataString(decodedFileName);
            httpResponse.Headers.Add("Content-Disposition", $"attachment; filename=\"{encodedFileName}\"");


            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                await fileStream.CopyToAsync(httpResponse.Body);
            }
            break;


        case "PUT":
            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                await httpRequest.Body.CopyToAsync(fileStream);
            }
            httpResponse.StatusCode = (int)HttpStatusCode.Created;
            await httpResponse.WriteAsync("Файл успешно загружен.");
            break;


        case "DELETE":
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
                httpResponse.StatusCode = (int)HttpStatusCode.NotFound;
                await httpResponse.WriteAsync("Файл не найден.");
                return;
            }


            fileInfo.Delete();
            httpResponse.StatusCode = (int)HttpStatusCode.NoContent;
            break;


        case "HEAD":
            await GetFileInfoAsync(httpResponse, filePath);
            break;


        default:
            httpResponse.StatusCode = 405;
            await httpResponse.WriteAsync("Метод не поддерживается.");
            break;
    }
});


async Task ListFilesAsync(HttpRequest httpRequest, HttpResponse httpResponse, string directoryPath)
{
    var files = Directory.EnumerateFiles(directoryPath).Select(Path.GetFileName).ToArray();
    await httpResponse.WriteAsJsonAsync(files);
    Console.WriteLine(string.Join(", ", files));
}


async Task GetFileInfoAsync(HttpResponse httpResponse, string filePath)
{
    if (!File.Exists(filePath))
    {
        httpResponse.StatusCode = 404;
        return;
    }


    var fileInfo = new FileInfo(filePath);
    httpResponse.Headers.Append("Content-Length", fileInfo.Length.ToString());
    httpResponse.Headers.Append("Last-Modified", fileInfo.LastWriteTime.ToString("R"));
    httpResponse.StatusCode = 200;
}

app.Run();