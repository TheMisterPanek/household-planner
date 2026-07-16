#!/bin/sh
set -e

TIMESTAMP=$(date +%Y-%m-%d_%H-%M-%S)
DB_SOURCE="/data/product-tracker.db"
BACKUP_DIR="/backups"
BACKUP_FILE="${BACKUP_DIR}/product-tracker-${TIMESTAMP}.db"

mkdir -p "$BACKUP_DIR"

# SQLite online backup — safe under concurrent writes
sqlite3 "$DB_SOURCE" ".backup '${BACKUP_FILE}'"
echo "[$(date)] Created: $BACKUP_FILE"

# Encrypt if passphrase is set
SEND_FILE="$BACKUP_FILE"
SEND_CAPTION="DB backup ${TIMESTAMP}"
if [ -n "$BACKUP_PASSPHRASE" ]; then
    ENCRYPTED_FILE="${BACKUP_FILE}.enc"
    openssl enc -aes-256-cbc -pbkdf2 -iter 100000 \
        -in "$BACKUP_FILE" -out "$ENCRYPTED_FILE" \
        -pass "pass:${BACKUP_PASSPHRASE}"
    rm -f "$BACKUP_FILE"
    SEND_FILE="$ENCRYPTED_FILE"
    SEND_CAPTION="DB backup ${TIMESTAMP} (AES-256 encrypted)"
    echo "[$(date)] Encrypted: $ENCRYPTED_FILE"
fi

# Send to Telegram
if [ -n "$BACKUP_CHAT_ID" ] && [ -n "$Token" ]; then
    RESPONSE=$(curl -s -w "\n%{http_code}" \
        -X POST "https://api.telegram.org/bot${Token}/sendDocument" \
        -F "chat_id=${BACKUP_CHAT_ID}" \
        -F "document=@${SEND_FILE}" \
        -F "caption=${SEND_CAPTION}")
    HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
    if [ "$HTTP_CODE" = "200" ]; then
        echo "[$(date)] Sent to Telegram chat $BACKUP_CHAT_ID"
    else
        echo "[$(date)] Telegram send failed (HTTP $HTTP_CODE)"
    fi
else
    echo "[$(date)] BACKUP_CHAT_ID not set — skipping Telegram send"
fi

# Keep last 30 backups
ls -t "${BACKUP_DIR}"/product-tracker-*.db "${BACKUP_DIR}"/product-tracker-*.db.enc 2>/dev/null | tail -n +31 | xargs -r rm -f
echo "[$(date)] Rotation done"
