using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pathing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebApplication1
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.Use((context, next) =>
            {
                var requestFeature = context.Features.Get<IHttpRequestFeature>();
                var originalPath = requestFeature.Path;
                requestFeature.Path = PathDecoder.GetPathFromRawTarget(requestFeature.RawTarget);
                logger.LogInformation("RawTarget: {rawTarget}, Path: {originalPath}, Reparsed Path: {path}, PathString: {pathString}", requestFeature.RawTarget, originalPath, requestFeature.Path, new PathString(requestFeature.Path).ToUriComponent());
                return next();
            });

            app.Run(context =>
            {
                return context.Response.WriteAsync("Hello World");
            });
        }
    }
}
