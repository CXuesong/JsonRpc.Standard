using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using JsonRpc.AspNetCore;
using JsonRpc.Standard.Contracts;
using Microsoft.AspNetCore.Http;

namespace WebTestApplication
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add framework services.
            services.AddMvc();
            services.AddMemoryCache();
            services.AddSession();
            services.AddJsonRpc(options => options.UseCamelCaseContractResolver()).RegisterFromAssembly<Startup>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseSession();
            // If you want to implement your JSON-RPC endpoint in Controller, you need to ensure calling
            // UseWebSockets first, then UseMvc.
            // You may also create your JSON-RPC Websocket endpoint with app.Use(...) rather than Controller.
            // See https://docs.microsoft.com/en-us/aspnet/core/fundamentals/websockets?view=aspnetcore-2.2#how-to-use-websockets
            // for more information.
            app.UseWebSockets();
            app.UseMvc();
            app.UseJsonRpc("/api/jsonrpc");
            app.UseStaticFiles();
        }
    }
}
