/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See LICENSE in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Swashbuckle.AspNetCore.Swagger;
using AuthenticationFailedContext = Microsoft.AspNetCore.Authentication.JwtBearer.AuthenticationFailedContext;

namespace Web.Api
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public const string ApiName = "web_api";
        public const string SwaggerClientID = "swaggerui";
        public string ApiFriendlyName;

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // The Client ID is used by the application to uniquely identify itself to Azure AD.
            string audience = Configuration["AzureAd:Audience"];

            // Tenant is the tenant ID (e.g. contoso.onmicrosoft.com, or 'common' for multi-tenant)
            string tenant = (bool.Parse(Configuration["AzureAd:IsMultiTenant"])) ? "common" : Configuration["AzureAd:Tenant"];

            // Authority is the URL for authority, composed by Azure Active Directory v2 endpoint and the tenant name (e.g. https://login.microsoftonline.com/contoso.onmicrosoft.com/v2.0)
            string authority = String.Format(System.Globalization.CultureInfo.InvariantCulture,
                Configuration["AzureAd:Instance"], tenant);

            // Add framework services.

            services.AddMvc();
            var xmlPath = GetXmlCommentsPath();

            //Add Options Configuration
            services.AddOptions();

            // Add Authentication scheme properties.
            services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            });

            //OpenID Connect (OIDC) Authentication
            services.AddAuthentication(options =>
            {
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
                .AddJwtBearer(option =>
                {
                    option.Authority = authority;
                    option.Audience = audience;
                    option.Events = new JwtBearerEvents
                    {
                        OnAuthenticationFailed = OnAuthenticationFailed,
                        OnTokenValidated = OnTokenValidated
                    };
                    option.TokenValidationParameters = new TokenValidationParameters
                    {
                        // Instead of using the default validation (validating against
                        // a single issuer value, as we do in line of business apps), 
                        // we inject our own multitenant validation logic
                        ValidateIssuer = false,
                        ValidIssuers = new List<string>
                        {
                            $"{authority}/v2.0"
                        },

                        // In this app, both API and client apps
                        // are represented using the same Application Id - we use
                        // the Application Id to represent the audience, or the
                        // intended recipient of tokens.
                        ValidAudience = audience,
                        NameClaimType = "name"
                    };
                });

            //Add CORS to support client-side API calls
            services.AddCors();

            services.AddLogging();


            ApiFriendlyName = String.Format(System.Globalization.CultureInfo.InvariantCulture,
                Configuration["Swagger:Title"]);


            // Inject an implementation of ISwaggerProvider with defaulted settings applied
            services.AddSwaggerGen(c =>
            {
                //Add the detail information for the API.
                c.SwaggerDoc(Configuration["Swagger:Version"], new Info
                {
                    Version = Configuration["Swagger:Version"],
                    Title = ApiFriendlyName,
                    Description = Configuration["Swagger:Description"],
                    Contact = new Contact { Name = Configuration["Swagger:Copyright"], Email = "", Url = "" },
                    Extensions = { }
                });
                c.IncludeXmlComments(xmlPath);
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddDebug(LogLevel.Trace);

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // Configure error handling middleware.
            app.UseExceptionHandler("/Home/Error");


            // Add static files to the request pipeline.
            app.UseStaticFiles();

            //Enable authentication
            app.UseAuthentication();

            //Enable CORS
            app.UseCors(builder => builder
                .AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());

            //Configure MVC
            var templateUri = String.Format("api/{0}/{{controller}}/{{id?}}", Configuration["Swagger:Version"]);
            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: templateUri);
            });

            // Enable middleware to serve generated Swagger as a JSON endpoint
            app.UseSwagger(c =>
            {
                c.RouteTemplate = "api/swagger/{documentName}/swagger.json";
            });

            // Enable middleware to serve swagger-ui assets (HTML, JS, CSS etc.)
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint($"/api/swagger/{Configuration["Swagger:Version"]}/swagger.json", $"{ApiFriendlyName} {Configuration["Swagger:Version"]}");
                c.RoutePrefix = "api/swagger";
                c.OAuthClientId(Startup.SwaggerClientID);
                c.OAuthClientSecret("no_password"); //Leaving it blank doesn't work
                c.OAuthAppName("Swagger UI");
            });
        }

        private Task OnAuthenticationFailed(AuthenticationFailedContext notification)
        {
            notification.Response.Redirect("/Home/Error");
            return Task.FromResult(0);
        }

        private Task OnTokenValidated(Microsoft.AspNetCore.Authentication.JwtBearer.TokenValidatedContext context)
        {
            // Example of how to validate for Microsoft Account + specific Azure AD tenant       
            // Replace this with your logic to validate the issuer/tenant
            // Retriever caller data from the incoming principal
            string issuer = context.SecurityToken.Issuer;
            string tenantID = context.Principal.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid").Value;

            // Build a dictionary of approved tenants
            IEnumerable<string> approvedTenantIds = new List<string>
            {
                Configuration["AzureAd:Tenant"], //{tenantid}
            };

            if (!approvedTenantIds.Contains(tenantID))
            {
                throw new SecurityTokenValidationException();
            }

            return Task.FromResult(0);
        }

        // Handle sign-in errors differently than generic errors.
        private Task OnRemoteFailure(RemoteFailureContext remoteFailureContext)
        {
            remoteFailureContext.HandleResponse();
            remoteFailureContext.Response.Redirect("/Home/Error?message=" + remoteFailureContext.Failure.Message);
            return Task.FromResult(0);
        }

        private string GetXmlCommentsPath()
        {
            return System.IO.Path.Combine("wwwroot", @"api.xml");
        }

    }
}
