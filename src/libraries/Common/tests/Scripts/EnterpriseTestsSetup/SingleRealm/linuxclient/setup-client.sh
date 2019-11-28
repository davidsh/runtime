#!/usr/bin/env bash

# Install Kerberos client and NTLM support
apt-get update && \
    apt-get install -y --no-install-recommends krb5-user gss-ntlmssp iputils-ping dnsutils nano && \
    apt-get clean
