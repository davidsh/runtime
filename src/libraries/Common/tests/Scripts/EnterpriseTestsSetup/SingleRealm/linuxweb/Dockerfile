ARG SDK_BASE_IMAGE=mcr.microsoft.com/dotnet/core/sdk:3.0.100-bionic
FROM $SDK_BASE_IMAGE

ENV DEBIAN_FRONTEND noninteractive

COPY ./common/krb5.conf /etc/

WORKDIR /setup
COPY ./linuxweb/*.sh ./
RUN chmod +x *.sh

RUN ./setup-web.sh

COPY ./linuxweb/ntlm_user_file .
ENV NTLM_USER_FILE=/setup/ntlm_user_file

COPY ./testservers/webserver/*.* /webserver/
COPY ./testservers/negserver/*.* /negserver/

EXPOSE 80
EXPOSE 8080

ENTRYPOINT ["/bin/bash", "/setup/run.sh"]
