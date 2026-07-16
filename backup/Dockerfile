FROM alpine:3.20

RUN apk add --no-cache sqlite curl openssl

COPY backup.sh /backup.sh
RUN chmod +x /backup.sh

# Daily at midnight. Use busybox crond (built in), NOT dcron:
# dcron's crond fails with "setpgid: Operation not permitted" when run as PID 1.
# busybox crond reads /var/spool/cron/crontabs/<user>.
RUN mkdir -p /var/spool/cron/crontabs \
    && echo "0 0 * * * /backup.sh >> /var/log/backup.log 2>&1" > /var/spool/cron/crontabs/root \
    && chmod 600 /var/spool/cron/crontabs/root

CMD ["crond", "-f", "-l", "8", "-L", "/dev/stdout"]
