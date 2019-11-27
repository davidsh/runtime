#!/usr/bin/env bash

# Install KDC
apt-get update && \
    apt-get install -y --no-install-recommends krb5-kdc krb5-admin-server iputils-ping dnsutils nano && \
    apt-get clean

# Kerbose Logging
mkdir -pv /var/log/kerberos/
touch /var/log/kerberos/krb5.log
touch /var/log/kerberos/kadmin.log
touch /var/log/kerberos/krb5lib.log

# Create Kerberos database
kdb5_util create -r LINUX.CONTOSO.COM -P password -s

# Start KDC service
krb5kdc

# Add users
kadmin.local -q "add_principal -pw password root/admin@LINUX.CONTOSO.COM"
kadmin.local -q "add_principal -pw password defaultcred@LINUX.CONTOSO.COM"
kadmin.local -q "add_principal -pw password user1@LINUX.CONTOSO.COM"
kadmin.local -q "add_principal -pw password user2@LINUX.CONTOSO.COM"
kadmin.local -q "add_principal -pw password user4krb@LINUX.CONTOSO.COM"

# Add SPNs for services for realm
kadmin.local -q "add_principal -pw password HTTP/apacheweb.linux.contoso.com"
kadmin.local -q "add_principal -pw password HOST/apacheweb.linux.contoso.com"
kadmin.local -q "add_principal -pw password HTTP/linuxweb.linux.contoso.com"
kadmin.local -q "add_principal -pw password HOST/webserver.linux.contoso.com" # This uses a CNAME "webserver"
kadmin.local -q "add_principal -pw password NEWSERVICE/linuxweb.linux.contoso.com"
kadmin.local -q "add_principal -pw password NEWSERVICE/localhost"
kadmin.local -q "add_principal -pw password HOST/linuxclient.linux.contoso.com"
kadmin.local -q "add_principal -pw password HOST/localhost"

# Create keytab files for other machines
kadmin.local ktadd -k /setup/apacheweb.keytab -norandkey HTTP/apacheweb.linux.contoso.com
kadmin.local ktadd -k /setup/apacheweb.keytab -norandkey HOST/apacheweb.linux.contoso.com
kadmin.local ktadd -k /setup/linuxweb.keytab -norandkey HTTP/linuxweb.linux.contoso.com
kadmin.local ktadd -k /setup/linuxweb.keytab -norandkey HOST/webserver.linux.contoso.com
kadmin.local ktadd -k /setup/linuxclient.keytab -norandkey HOST/linuxclient.linux.contoso.com
kadmin.local ktadd -k /setup/linuxclient.keytab -norandkey HOST/localhost
