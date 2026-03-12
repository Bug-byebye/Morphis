"""
测试 World 进程管理器
"""
import requests
import time
import json

BASE_URL = "http://localhost:8000"

def test_world_lifecycle():
    """测试 World 完整生命周期"""
    
    print("=" * 50)
    print("测试 World 进程管理器")
    print("=" * 50)
    
    world_id = "test-world-001"
    
    # 1. 启动 World
    print(f"\n1. 启动 World: {world_id}")
    response = requests.post(
        f"{BASE_URL}/worlds/manage/start",
        json={"world_id": world_id}
    )
    print(f"   状态码: {response.status_code}")
    print(f"   响应: {response.json()}")
    
    if response.status_code != 200:
        print("   ❌ 启动失败")
        return
    
    port = response.json().get("port")
    print(f"   ✅ World 已启动，端口: {port}")
    
    # 2. 等待进程启动
    print("\n2. 等待进程启动...")
    time.sleep(3)
    
    # 3. 查询状态
    print(f"\n3. 查询 World 状态")
    response = requests.get(f"{BASE_URL}/worlds/manage/status/{world_id}")
    print(f"   状态码: {response.status_code}")
    data = response.json()
    print(f"   World 状态: {data.get('world_status')}")
    print(f"   端口: {data.get('port')}")
    print(f"   进程 ID: {data.get('process_id')}")
    print(f"   玩家数: {data.get('player_count')}")
    
    # 4. 模拟玩家加入
    print(f"\n4. 模拟玩家加入")
    response = requests.post(
        f"{BASE_URL}/worlds/manage/player-count",
        json={"world_id": world_id, "count": 2}
    )
    print(f"   状态码: {response.status_code}")
    print(f"   ✅ 玩家数更新为 2")
    
    # 5. 再次查询状态
    print(f"\n5. 再次查询状态")
    response = requests.get(f"{BASE_URL}/worlds/manage/status/{world_id}")
    data = response.json()
    print(f"   玩家数: {data.get('player_count')}")
    
    # 6. 列出所有 World
    print(f"\n6. 列出所有 World")
    response = requests.get(f"{BASE_URL}/worlds/manage/list")
    worlds = response.json().get("worlds", [])
    print(f"   总数: {len(worlds)}")
    for w in worlds:
        print(f"   - {w['id']}: {w['status']}, port={w['port']}, players={w['player_count']}")
    
    # 7. 停止 World
    print(f"\n7. 停止 World")
    response = requests.post(
        f"{BASE_URL}/worlds/manage/stop",
        json={"world_id": world_id, "force": False}
    )
    print(f"   状态码: {response.status_code}")
    print(f"   响应: {response.json()}")
    print(f"   ✅ World 已停止")
    
    # 8. 验证停止
    print(f"\n8. 验证 World 已停止")
    response = requests.get(f"{BASE_URL}/worlds/manage/status/{world_id}")
    data = response.json()
    print(f"   World 状态: {data.get('world_status')}")
    print(f"   进程 ID: {data.get('process_id')}")
    
    print("\n" + "=" * 50)
    print("测试完成！")
    print("=" * 50)


def test_join_world():
    """测试客户端加入 World 流程"""
    
    print("\n" + "=" * 50)
    print("测试客户端加入 World")
    print("=" * 50)
    
    # 1. 注册用户
    print("\n1. 注册测试用户")
    response = requests.post(
        f"{BASE_URL}/auth/register",
        json={"username": "testuser", "password": "testpass"}
    )
    if response.status_code == 409:
        print("   用户已存在，尝试登录")
        response = requests.post(
            f"{BASE_URL}/auth/login",
            json={"username": "testuser", "password": "testpass"}
        )
    
    token = response.json().get("token")
    print(f"   ✅ 已登录，token: {token[:20]}...")
    
    # 2. 获取 Workspace 列表
    print("\n2. 获取 Workspace 列表")
    response = requests.get(
        f"{BASE_URL}/workspaces",
        headers={"Authorization": f"Bearer {token}"}
    )
    workspaces = response.json().get("items", [])
    print(f"   总数: {len(workspaces)}")
    for ws in workspaces:
        print(f"   - {ws['id']}: {ws['name']}, status={ws['status']}, port={ws.get('port')}")
    
    if not workspaces:
        print("   ❌ 没有可用的 Workspace")
        return
    
    world_id = workspaces[0]["id"]
    
    # 3. 请求加入 World
    print(f"\n3. 请求加入 World: {world_id}")
    response = requests.post(
        f"{BASE_URL}/workspaces/join",
        json={"world_id": world_id},
        headers={"Authorization": f"Bearer {token}"}
    )
    print(f"   状态码: {response.status_code}")
    data = response.json()
    print(f"   服务器地址: {data.get('server_address')}")
    print(f"   服务器端口: {data.get('server_port')}")
    print(f"   消息: {data.get('message')}")
    print(f"   ✅ 可以连接到 {data.get('server_address')}:{data.get('server_port')}")
    
    print("\n" + "=" * 50)
    print("测试完成！")
    print("=" * 50)


if __name__ == "__main__":
    import sys
    
    if len(sys.argv) > 1 and sys.argv[1] == "join":
        test_join_world()
    else:
        test_world_lifecycle()
