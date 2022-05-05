﻿namespace ServiceControl.Transports.ASBS
{
    using Azure.Core;

    public class TokenCredentialAuthentication : AuthenticationSettings
    {
        public TokenCredentialAuthentication(string fullyQualifiedNamespace, TokenCredential credential)
        {
            FullyQualifiedNamespace = fullyQualifiedNamespace;
            Credential = credential;
        }

        public string FullyQualifiedNamespace { get; }

        public TokenCredential Credential { get; }
    }
}