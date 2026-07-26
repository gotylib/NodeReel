#!/bin/sh
set -eu

: "${API_UPSTREAM:=http://api:8080}"

# Strip trailing slash so proxy_pass ${API_UPSTREAM}/api/ is correct.
API_UPSTREAM=$(printf '%s' "$API_UPSTREAM" | sed 's:/*$::')
export API_UPSTREAM

envsubst '${API_UPSTREAM}' < /etc/nginx/templates/default.conf.template \
  > /etc/nginx/conf.d/default.conf

exec nginx -g 'daemon off;'