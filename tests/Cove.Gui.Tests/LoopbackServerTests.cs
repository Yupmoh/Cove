using System.Net;
using Cove.Gui;
using Xunit;

public class LoopbackServerTests
{
    [Fact]
    public async Task Serves_Index_200_And_Missing_404()
    {
        var tmp = Directory.CreateTempSubdirectory().FullName;
        await File.WriteAllTextAsync(Path.Combine(tmp, "index.html"), "<html>hello</html>");
        await File.WriteAllTextAsync(Path.Combine(tmp, "app.js"), "console.log(1)");
        await using var server = new LoopbackServer(tmp, _ => throw new NotImplementedException(), "0.1.0", "dev", port: 0);
        server.Start();
        using var http = new HttpClient();

        var index = await http.GetAsync($"http://127.0.0.1:{server.Port}/");
        Assert.Equal(HttpStatusCode.OK, index.StatusCode);
        Assert.Contains("hello", await index.Content.ReadAsStringAsync());

        var js = await http.GetAsync($"http://127.0.0.1:{server.Port}/app.js");
        Assert.Equal("text/javascript", js.Content.Headers.ContentType!.MediaType);

        var missing = await http.GetAsync($"http://127.0.0.1:{server.Port}/nope.js");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }
}
