FROM httpd:2.4

ENV DEBIAN_FRONTEND noninteractive

COPY ./common/krb5.conf /etc/

WORKDIR /setup
COPY ./apacheweb/*.sh ./
RUN chmod +x *.sh

RUN ./setup-web.sh

COPY ./apacheweb/httpd.conf /usr/local/apache2/conf/httpd.conf

ENTRYPOINT ["/bin/sh", "/setup/run.sh"]
