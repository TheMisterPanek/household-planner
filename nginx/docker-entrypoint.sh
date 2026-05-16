#!/bin/sh
set -e

# Generate landing page from template, substituting BOT_USERNAME from .env
if [ -n "$BOT_USERNAME" ]; then
  sed 's/\${BOT_USERNAME}/'"$BOT_USERNAME"'/g' /usr/share/nginx/html/index.html.template > /usr/share/nginx/html/index.html
else
  echo "WARNING: BOT_USERNAME not set, using template as-is"
  cp /usr/share/nginx/html/index.html.template /usr/share/nginx/html/index.html
fi

# Hand off to the default nginx entrypoint
exec /docker-entrypoint.sh "$@"
