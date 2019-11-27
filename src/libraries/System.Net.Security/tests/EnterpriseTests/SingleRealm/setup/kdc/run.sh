#!/usr/bin/env bash

service krb5-kdc restart
service krb5-admin-server restart

cp /setup/*.keytab /SHARED

# Keep the container running
tail -f /dev/null
