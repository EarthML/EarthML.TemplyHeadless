using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace SimpleRemotePage
{
    public class Program
    {
        public static Task Main(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .Configure(app =>
                {
                    app.Run(async (context) =>
                    {
                        if (context.Request.Path == "/SimpleRemotePage.js")
                        {
                            await context.Response.SendFileAsync("wwwroot/SimpleRemotePage.js");
                        }
                    });
              })
              .Build()
            .RunAsync();
      
    }
}
