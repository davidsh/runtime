#!/bin/sh
set -e

# Apache gets grumpy about PID files pre-existing
rm -f /usr/local/apache2/logs/httpd.pid

cp /SHARED/apacheweb.keytab /etc/krb5.keytab
chmod +r /etc/krb5.keytab

exec httpd -DFOREGROUND "$@"
# Keep the container running
#tail -f /dev/null
