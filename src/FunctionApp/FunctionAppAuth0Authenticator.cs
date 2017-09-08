﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.IdentityModel.Tokens;

namespace FunctionApp
{
    // This is a singleton, since we want to share the Auth0Authenticator instance across all function app invocations in this appdomain.
    public static class FunctionAppAuth0Authenticator
    {
        // I'm using a Lazy here just so that exceptions on startup are in the scope of a function execution.
        // I'm using PublicationOnly so that exceptions during creation are retried on the next execution.
        private static readonly Lazy<Auth0Authenticator> Authenticator = new Lazy<Auth0Authenticator>(() => new Auth0Authenticator(Constants.Auth0Domain, new [] { Constants.Audience, Constants.ClientId }));

        public static async Task<(ClaimsPrincipal User, SecurityToken ValidatedToken)> AuthenticateAsync(this HttpRequestMessage request, TraceWriter log)
        {
            var authenticator = Authenticator.Value;
            try
            {
                var result = await authenticator.AuthenticateAsync(request);

                // If we have any additional tokens, then authenticate them and merge those identities into our principal.
                if (request.Headers.TryGetValues("X-Additional-Token", out var values))
                {
                    var extraTokenResults = await Task.WhenAll(values.Select(v => authenticator.AuthenticateAsync(v)));
                    result.User.AddIdentities(extraTokenResults.SelectMany(x => x.User.Identities));
                }

                return result;
            }
            catch (Exception ex)
            {
                log.Error("Authorization failed", ex);
                throw new ExpectedException(HttpStatusCode.Forbidden);
            }
        }
    }
}
