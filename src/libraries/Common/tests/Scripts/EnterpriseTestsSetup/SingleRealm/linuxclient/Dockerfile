ARG SDK_BASE_IMAGE=mcr.microsoft.com/dotnet/core/sdk:3.0.100-bionic
FROM $SDK_BASE_IMAGE

# environment variables
ENV DEBIAN_FRONTEND noninteractive

WORKDIR /etc
COPY ./common/krb5.conf .

WORKDIR /setup
COPY ./linuxclient/*.sh ./
RUN chmod +x *.sh

RUN ./setup-client.sh

RUN mkdir /testresults

ENTRYPOINT ["/bin/bash", "/setup/run.sh"]
