#!/bin/bash
#
# 数据备份脚本
# 用途：备份数据库和配置文件
#
# 添加到 crontab:
#   0 2 * * * /path/to/backup.sh >> /var/log/morphis-backup.log 2>&1
#

BACKUP_DIR="/home/$USER/backups"
DATE=$(date +%Y%m%d_%H%M%S)
RETENTION_DAYS=7

# 创建备份目录
mkdir -p "$BACKUP_DIR"

echo "========================================="
echo "  Morphis Backup - $DATE"
echo "========================================="

# 备份数据库
echo "Backing up database..."
sudo -u postgres pg_dump morphis_db | gzip > "$BACKUP_DIR/db_$DATE.sql.gz"
if [ $? -eq 0 ]; then
    echo "✓ Database backup completed"
else
    echo "✗ Database backup failed!"
fi

# 备份配置文件
echo "Backing up configuration files..."
if [ -f "/home/$USER/MorphisServer/config.json" ]; then
    cp "/home/$USER/MorphisServer/config.json" "$BACKUP_DIR/server_config_$DATE.json"
    echo "✓ Server config backed up"
fi

if [ -f "/home/$USER/Morphis/Backend/.env" ]; then
    cp "/home/$USER/Morphis/Backend/.env" "$BACKUP_DIR/backend_env_$DATE.txt"
    echo "✓ Backend env backed up"
fi

# 删除旧备份
echo "Cleaning up old backups (older than $RETENTION_DAYS days)..."
find "$BACKUP_DIR" -type f -mtime +$RETENTION_DAYS -delete
echo "✓ Cleanup completed"

# 显示备份大小
BACKUP_SIZE=$(du -sh "$BACKUP_DIR" | cut -f1)
echo ""
echo "Backup directory size: $BACKUP_SIZE"
echo "Backup completed: $DATE"
echo "========================================="
