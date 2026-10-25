using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace wwwmmm.Web;

public static class DemoPageHandler
{
    public static async Task HandleAsync(HttpListenerContext context)
    {
        var paths = new[] {
            Path.Combine(Directory.GetCurrentDirectory(), "Web", "wwwroot", "demo.html"),
            Path.Combine(Directory.GetCurrentDirectory(), "demo.html")
        };

        string htmlPath = null;
        foreach (var p in paths) {
            if (File.Exists(p)) { htmlPath = p; break; }
        }

        if (htmlPath == null) {
            var err = Encoding.UTF8.GetBytes("demo.html nuk u gjet.");
            context.Response.StatusCode = 404;
            context.Response.ContentType = "text/plain";
            context.Response.ContentLength64 = err.Length;
            await context.Response.OutputStream.WriteAsync(err, 0, err.Length);
            context.Response.Close();
            return;
        }

        var html = await File.ReadAllTextAsync(htmlPath);
        var buffer = Encoding.UTF8.GetBytes(html);
        
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = buffer.Length;
        await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        context.Response.Close();
    }
}
