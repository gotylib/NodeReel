#!/bin/sh
set -eu

# Dokploy writes Environment Settings into .env next to the Dockerfile at build time.
if [ -f /app/.env ]; then
  echo "Loading /app/.env"
  while IFS= read -r line || [ -n "$line" ]; do
    # trim CR
    line=$(printf '%s' "$line" | tr -d '\r')
    case "$line" in
      ''|\#*) continue ;;
    esac
    key=${line%%=*}
    val=${line#*=}
    # strip surrounding quotes
    val=$(printf '%s' "$val" | sed -e 's/^"\(.*\)"$/\1/' -e "s/^'\(.*\)'$/\1/")
    # do not override real process env
    eval "current=\${$key-}"
    if [ -z "${current}" ]; then
      export "$key=$val"
    fi
  done < /app/.env
fi

: "${API_UPSTREAM:=http://api:8080}"

# Strip trailing slash so proxy_pass ${API_UPSTREAM}/api/ is correct.
API_UPSTREAM=$(printf '%s' "$API_UPSTREAM" | sed 's:/*$::')
export API_UPSTREAM

echo "API_UPSTREAM=${API_UPSTREAM}"

envsubst '${API_UPSTREAM}' < /etc/nginx/templates/default.conf.template \
  > /etc/nginx/conf.d/default.conf

exec nginx -g 'daemon off;'
