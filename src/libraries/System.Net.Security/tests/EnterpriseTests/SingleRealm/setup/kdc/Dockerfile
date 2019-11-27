FROM mcr.microsoft.com/dotnet/core/sdk:3.0.100-bionic

# environment variables
ENV DEBIAN_FRONTEND noninteractive

COPY ./kdc/kadm5.acl /etc/krb5kdc/
COPY ./kdc/kdc.conf /etc/krb5kdc/
COPY ./common/krb5.conf /etc/

WORKDIR /setup
COPY ./kdc/setup-kdc.sh .
RUN chmod +x setup-kdc.sh
RUN ./setup-kdc.sh

COPY ./kdc/run.sh .
RUN chmod +x run.sh

ENTRYPOINT ["/bin/bash", "/setup/run.sh"]
