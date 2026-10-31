#!/bin/sh
set -eu

# Render absolute sharing URLs on startup; leave asset links relative so the
# same page works in local previews and in the combined Unity hosting image.
LANDING_URL="${LANDING_URL:-https://littlelegion.app/landing/}"
case "$LANDING_URL" in
    http://?*|https://?*) ;;
    *) echo "LANDING_URL must be an absolute http(s) URL" >&2; exit 1 ;;
esac
case "$LANDING_URL" in
    *[!a-zA-Z0-9:/._~-]*) echo "LANDING_URL must contain only a host, optional port and path" >&2; exit 1 ;;
esac
sed "s#https://littlelegion.app/landing/#${LANDING_URL%/}/#g" \
    /opt/landing-index.html > /usr/share/nginx/html/landing/index.html
