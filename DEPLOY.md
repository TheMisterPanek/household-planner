# Deployment Guide

## SSL Certificate (Certbot)

Once nginx is running on port 80, run this one-liner to provision a certificate:

```bash
docker run --rm \
  -v $(pwd)/nginx/certs:/etc/letsencrypt \
  -v $(pwd)/html-landing:/var/www/html \
  certbot/certbot certonly --webroot \
  -w /var/www/html -d yourdomain.com \
  --email you@example.com --agree-tos --non-interactive
```

Then:

1. Uncomment the HTTPS server block at the bottom of `nginx/nginx.conf`
2. Replace `yourdomain.com` with your actual domain
3. Run `docker compose restart nginx`
