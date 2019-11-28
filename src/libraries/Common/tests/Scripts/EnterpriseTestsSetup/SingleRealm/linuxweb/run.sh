#!/usr/bin/env bash

export ASPNETCORE_URLS="http://+:80;https://+:443"
export ASPNETCORE_ENVIRONMENT="Development"

cp /SHARED/linuxweb.keytab /etc/krb5.keytab

# Build and run NegotiateStream server listening on port 8080
cd /negserver
dotnet build
bin/Debug/netcoreapp3.0/negserver 8080 &> /SHARED/negserver.log &

# Build and run ASP.NET Core server which uses Negotiate authentication
cd /webserver
dotnet build
bin/Debug/netcoreapp3.0/webserver &> /SHARED/webserver.log &

# Keep the container running since both servers are running the background
tail -f /dev/null
