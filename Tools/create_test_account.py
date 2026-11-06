"""创建（或复用）一个满级测试账号，并把“全部属性满、全部关卡已通”的存档上传到它的云端槽位。

用法（服务器需已启动）：
    python Tools/create_test_account.py
    python Tools/create_test_account.py --username test_full --password Test12345 --slot 1

游戏里登录该账号后，在“继续游戏”里双击对应槽位即可从云端恢复到本地。
再次运行会覆盖同一槽位，可随时把测试账号恢复到满级状态。
"""
import argparse
import json
import sys
import urllib.error
import urllib.request
from datetime import datetime

MAX_PLAYER_LEVEL = 81   # 数值总表的等级上限（目前等级只用于显示）
MAX_SKILL_LEVEL = 6     # Resources/Balance/Common/Progression.asset 的 Skills.MaxLevel
MAX_GAME_LEVEL = 9      # 第 1～9 关全部可进入
ALL_CHARACTERS = 15     # 四个角色的解锁位掩码：悟空/八戒/沙僧/唐僧


def full_save(name: str) -> dict:
    """字段与 SaveData（JsonUtility 序列化）一致。"""
    return {
        "players": [
            {"id": pid, "playerLevel": MAX_PLAYER_LEVEL, "exp": 0.0,
             "skillLevels": [MAX_SKILL_LEVEL] * 3}
            for pid in range(4)
        ],
        "gameLevel": MAX_GAME_LEVEL,
        "rebirth": 0,
        "rebirthAvailable": True,   # 视为已通第 9 关，可直接测试轮回
        "unlocked": ALL_CHARACTERS,
        "name": name,
        "date": datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
        "gold": 99_999_999,
        "defeatedBossLevels": list(range(1, MAX_GAME_LEVEL + 1)),
    }


def post(url: str, body: dict, token: str | None = None) -> tuple[int, dict]:
    headers = {"Content-Type": "application/json; charset=utf-8"}
    if token:
        headers["Authorization"] = "Bearer " + token
    request = urllib.request.Request(url, json.dumps(body).encode("utf-8"), headers, method="POST")
    try:
        with urllib.request.urlopen(request, timeout=10) as response:
            text = response.read().decode("utf-8")
            return response.status, json.loads(text) if text else {}
    except urllib.error.HTTPError as error:
        text = error.read().decode("utf-8", "replace")
        try:
            return error.code, json.loads(text)
        except json.JSONDecodeError:
            return error.code, {"message": text}


def main() -> int:
    parser = argparse.ArgumentParser(description="创建满级测试账号并上传满级存档")
    parser.add_argument("--server", default="http://localhost:5223")
    parser.add_argument("--username", default="test_full")
    parser.add_argument("--password", default="Test12345")
    parser.add_argument("--slot", type=int, default=1, help="云端槽位 1～6，对应游戏里第 1～6 个存档")
    parser.add_argument("--name", default="满级测试", help="存档显示名")
    args = parser.parse_args()
    api = args.server.rstrip("/") + "/api"

    try:
        status, body = post(api + "/account/register", {"username": args.username, "password": args.password})
    except urllib.error.URLError as error:
        print(f"服务器未连接：{error.reason}（请先启动 ZaoMengXiYou3.Server）")
        return 1
    if status == 201:
        print(f"已注册账号 {args.username}")
    elif status == 409:
        print(f"账号 {args.username} 已存在，直接登录")
    else:
        print(f"注册失败 HTTP {status}: {body}")
        return 1

    status, body = post(api + "/account/login", {"username": args.username, "password": args.password})
    token = body.get("accessToken")
    if status != 200 or not token:
        print(f"登录失败 HTTP {status}: {body}")
        return 1

    save = full_save(args.name)
    status, body = post(api + "/save/upload",
                        {"slotNumber": args.slot, "dataJson": json.dumps(save, ensure_ascii=False)}, token)
    if status != 200:
        print(f"上传存档失败 HTTP {status}: {body}")
        return 1

    print(f"已上传满级存档到云端槽位 {args.slot}（游戏里第 {args.slot} 个存档）")
    print(f"账号：{args.username}  密码：{args.password}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
