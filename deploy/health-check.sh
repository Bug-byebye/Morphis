#!/bin/bash
#
# 服务健康检查脚本
# 用途：定期检查服务状态，自动重启失败的服务
#
# 添加到 crontab:
#   */5 * * * * /path/to/health-check.sh >> /var/log/morphis-health.log 2>&1
#

LOG_FILE="/var/log/morphis-health.log"
DATE=$(date '+%Y-%m-%d %H:%M:%S')

log() {
    echo "[$DATE] $1" | tee -a "$LOG_FILE"
}

# 检查 Backend
if ! curl -s -f http://localhost:8000/health > /dev/null 2>&1; then
    log "ERROR: Backend is down! Restarting..."
    sudo systemctl restart morphis-backend
    sleep 5
    if curl -s -f http://localhost:8000/health > /dev/null 2>&1; then
        log "SUCCESS: Backend restarted successfully"
    else
        log "CRITICAL: Backend restart failed!"
    fi
else
    log "OK: Backend is healthy"
fi

# 检查 Unity Server
if ! sudo netstat -tulpn | grep :7777 > /dev/null 2>&1; then
    log "ERROR: Unity Server is down! Restarting..."
    sudo systemctl restart morphis-server
    sleep 10
    if sudo netstat -tulpn | grep :7777 > /dev/null 2>&1; then
        log "SUCCESS: Unity Server restarted successfully"
    else
        log "CRITICAL: Unity Server restart failed!"
    fi
else
    log "OK: Unity Server is healthy"
fi

# 检查磁盘空间
DISK_USAGE=$(df -h / | awk 'NR==2 {print $5}' | sed 's/%//')
if [ "$DISK_USAGE" -gt 80 ]; then
    log "WARNING: Disk usage is ${DISK_USAGE}%"
fi

# 检查内存使用
MEM_USAGE=$(free | awk 'NR==2 {printf "%.0f", $3/$2*100}')
if [ "$MEM_USAGE" -gt 90 ]; then
    log "WARNING: Memory usage is ${MEM_USAGE}%"
fi
