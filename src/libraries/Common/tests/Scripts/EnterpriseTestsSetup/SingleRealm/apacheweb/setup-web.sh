#!/usr/bin/env bash

# Install Kerberos client
apt-get update && \
    apt-get install -y --no-install-recommends libapache2-mod-auth-kerb procps krb5-user iputils-ping dnsutils nano && \
    apt-get clean

# Copy apache2 kerb module to the right place since the apt-get install puts in the wrong place for this docker image
cp /usr/lib/apache2/modules/mod_auth_kerb.so /usr/local/apache2/modules
