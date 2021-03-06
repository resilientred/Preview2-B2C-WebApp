﻿using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.AspNetCore.Authentication.Extensions
{
    public static class AzureAdB2CServiceCollectionExtensions
    {
        public static IServiceCollection AddAzureAdB2CAuthentication(this IServiceCollection services, string apiScope)
        {
            // Move to config binding
            services.AddAuthentication(sharedOptions =>
            {
                sharedOptions.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                sharedOptions.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                sharedOptions.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            });

            services.AddSingleton<IConfigureOptions<AzureAdB2COptions>, BindAzureAdB2COptions>();
            services.AddSingleton<IPostConfigureOptions<OpenIdConnectOptions>, PostConfigureAzureOptions>();
            services.AddOpenIdConnectAuthentication(options =>
            {
                options.ResponseType = OpenIdConnectResponseType.CodeIdToken;
                options.Scope.Add(apiScope);
            });
            services.AddCookieAuthentication();
            return services;
        }

        private class BindAzureAdB2COptions : ConfigureOptions<AzureAdB2COptions>
        {
            public BindAzureAdB2COptions(IConfiguration config) : 
                base(options => config.GetSection("AzureAdB2C").Bind(options))
            { }
        }

        private class PostConfigureAzureOptions: IPostConfigureOptions<OpenIdConnectOptions>
        {
            private readonly AzureAdB2COptions _azureOptions;
            private readonly IMemoryCache _cache;

            public PostConfigureAzureOptions(IOptions<AzureAdB2COptions> azureOptions, IMemoryCache cache)
            {
                _azureOptions = azureOptions.Value;
                _cache = cache;
            }

            public void PostConfigure(string name, OpenIdConnectOptions options)
            {
                options.ClientId = _azureOptions.ClientId;
                options.Authority = _azureOptions.Authority;
                options.UseTokenLifetime = true;
                options.CallbackPath = _azureOptions.CallbackPath;

                options.TokenValidationParameters = new TokenValidationParameters() { NameClaimType = "name" };

                options.Events = new OpenIdConnectEvents()
                {
                    OnRedirectToIdentityProvider = OnRedirectToIdentityProvider,
                    OnRemoteFailure = OnRemoteFailure,
                    OnAuthorizationCodeReceived = OnAuthorizationCodeReceived
                };
            }

            private async Task OnAuthorizationCodeReceived(AuthorizationCodeReceivedContext context)
            {
                var code = context.ProtocolMessage.Code;

                var signedInUserID = context.Ticket.Principal.FindFirst(ClaimTypes.NameIdentifier).Value;
                var cca = new ConfidentialClientApplication(
                    _azureOptions.ClientId,
                    _azureOptions.Authority,
                    _azureOptions.RedirectUri,
                    new ClientCredential(_azureOptions.ClientSecret),
                    userTokenCache: null,
                    appTokenCache: null);

                var result = await cca.AcquireTokenByAuthorizationCodeAsync(code, _azureOptions.ApiScopes.Split(' '));
                context.HandleCodeRedemption(result.AccessToken, result.IdToken);

                _cache.Set(signedInUserID, result.AccessToken);
            }

            public Task OnRedirectToIdentityProvider(RedirectContext context)
            {
                var defaultPolicy = _azureOptions.DefaultPolicy;
                if (context.Properties.Items.TryGetValue(AzureAdB2COptions.PolicyAuthenticationProperty, out var policy) && 
                    !policy.Equals(defaultPolicy))
                {
                    context.ProtocolMessage.Scope = OpenIdConnectScope.OpenIdProfile;
                    context.ProtocolMessage.ResponseType = OpenIdConnectResponseType.IdToken;
                    context.ProtocolMessage.IssuerAddress = context.ProtocolMessage.IssuerAddress.ToLower().Replace(defaultPolicy.ToLower(), policy.ToLower());
                    context.Properties.Items.Remove(AzureAdB2COptions.PolicyAuthenticationProperty);
                }
                return Task.FromResult(0);
            }
 
            public Task OnRemoteFailure(FailureContext context)
            {
                context.HandleResponse();
                // Handle the error code that Azure AD B2C throws when trying to reset a password from the login page 
                // because password reset is not supported by a "sign-up or sign-in policy"
                if (context.Failure is OpenIdConnectProtocolException && context.Failure.Message.Contains("AADB2C90118"))
                {
                    // If the user clicked the reset password link, redirect to the reset password route
                    context.Response.Redirect("/Account/ResetPassword");
                }
                else if (context.Failure is OpenIdConnectProtocolException && context.Failure.Message.Contains("access_denied"))
                {
                    context.Response.Redirect("/");
                }
                else
                {
                    context.Response.Redirect("/Home/Error");
                }
                return Task.FromResult(0);
            }
        }
    }
}
