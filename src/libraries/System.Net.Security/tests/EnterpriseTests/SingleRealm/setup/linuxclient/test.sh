#!/usr/bin/env bash

kdestroy
echo password | kinit user1
curl --verbose --negotiate -u: http://webserver.linux.contoso.com
kdestroy
