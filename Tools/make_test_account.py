"""在本机服务器上建一个满配测试账号，并把满配存档上传到云端槽位 1。
满配：四人 60 级、技能 6 级、被动全满、轮回 10（敌人倍率封顶的一轮）、九关全开、
每人最高档传说武器/防具/饰品、五件法宝 10 级（四件装备在身上，一件在背包），背包里另放几件不同品质的装备方便演示。
账号密码写进 Docs/测试账号.md。"""
import json, os, re, secrets, string, urllib.request, urllib.error, datetime
BASE = 'http://localhost:5223'
USER = 'test_max'
DOC = 'Docs/测试账号.md'

# ---------- 读装备数据库 ----------
t = open('Assets/Projects/Resources/Equipment/EquipmentDatabase.asset', encoding='utf-8').read()
items = []
for m in re.finditer(r'  - id: (\S+)\n(?:    .*\n)*?    slot: (\d+)\n    owner: (-?\d+)\n    tier: (-?\d+)\n    grade: ([\d.]+)\n', t):
    items.append(dict(id=m.group(1), slot=int(m.group(2)), owner=int(m.group(3)), tier=int(m.group(4)), grade=float(m.group(5))))
assert items, 'database parse failed'


def best(slot, owner):
    c = [i for i in items if i['slot'] == slot and i['owner'] == owner]
    return max(c, key=lambda i: i['grade'])['id']


equipment = []
def add(item_id, rarity, level=0):
    uid = len(equipment) + 1
    equipment.append({'uid': uid, 'id': item_id, 'rarity': rarity, 'level': level})
    return uid


PASSIVE_MAX = [[10, 10, 10, 10, 2, 2, 10], [10, 10, 10, 10, 5, 2, 10], [10, 10, 10, 10, 3, 3, 10], [10, 10, 10, 10, 3, 4, 10]]
TREASURE_FOR = {0: 'treasure_3', 1: 'treasure_2', 2: 'treasure_5', 3: 'treasure_4'}  # 悟空飞剑、八戒葫芦、沙僧铃铛、唐僧伞
players = []
for pid in range(4):
    players.append({
        'id': pid, 'playerLevel': 60, 'exp': 0.0, 'skillLevels': [6, 6, 6], 'passiveLevels': PASSIVE_MAX[pid],
        'weaponUid': add(best(0, pid), 4), 'armorUid': add(best(1, pid), 4),
        'accessoryUid': add(best(2, -1), 4), 'treasureUid': add(TREASURE_FOR[pid], 0, 10),
    })
add('treasure_1', 0, 10)  # 回春灵叶放在背包
# 背包里放几件不同品质的装备，演示背包、悬停说明、出售白装和整理
weapons = sorted([i for i in items if i['slot'] == 0], key=lambda i: (i['owner'], i['grade']))
armors = sorted([i for i in items if i['slot'] == 1], key=lambda i: (i['owner'], i['grade']))
for k, (it, r) in enumerate([(weapons[1], 3), (armors[2], 2), (weapons[3], 1), (armors[0], 0), (weapons[0], 0), (armors[5], 1)]):
    add(it['id'], r)

data = {
    'players': players, 'gameLevel': 9, 'rebirth': 10, 'rebirthAvailable': True, 'unlocked': 15,
    'name': '满配测试', 'date': datetime.datetime.now().strftime('%Y/%m/%d %H:%M'), 'gold': 999999,
    'defeatedBossLevels': [r * 10 + lv for r in range(11) for lv in range(1, 10)],
    'equipment': equipment, 'nextEquipmentUid': len(equipment) + 1,
}


def call(path, body, token=None):
    req = urllib.request.Request(BASE + path, data=json.dumps(body).encode('utf-8'), method='POST',
                                 headers={'Content-Type': 'application/json', **({'Authorization': 'Bearer ' + token} if token else {})})
    try:
        with urllib.request.urlopen(req, timeout=10) as r: return r.status, json.loads(r.read() or b'{}')
    except urllib.error.HTTPError as e:
        return e.code, (json.loads(e.read() or b'{}') if e.headers.get('Content-Type', '').startswith('application/json') else {})


# 已经建过就沿用文档里的密码
password = None
if os.path.exists(DOC):
    m = re.search(r'密码：`([^`]+)`', open(DOC, encoding='utf-8').read())
    if m: password = m.group(1)
if password is None:
    alphabet = string.ascii_letters + string.digits
    password = 'Test_' + ''.join(secrets.choice(alphabet) for _ in range(10))
    code, body = call('/api/account/register', {'username': USER, 'password': password})
    assert code == 201, ('register', code, body)
code, body = call('/api/account/login', {'username': USER, 'password': password})
assert code == 200, ('login', code, body)
token = body['accessToken']
code, body = call('/api/save/upload', {'slotNumber': 1, 'dataJson': json.dumps(data, ensure_ascii=False)}, token)
assert code == 200, ('upload', code, body)
print('uploaded', body)

os.makedirs('Docs', exist_ok=True)
open(DOC, 'w', encoding='utf-8').write(f'''# 测试账号（仅本机服务器）

用于录制演示和测试，账号保存在本机服务器的 `game.db` 里，换电脑或删除数据库后需要重新创建。

- 账号：`{USER}`
- 密码：`{password}`
- 存档：云端槽位 1，名字“满配测试”

## 存档内容
- 四个角色全部解锁，60 级（满级），三个技能 6 级（满级），被动技能全部点满。
- 轮回 10（敌人强度封顶的一轮），第 1～9 关全部可进，本轮可以再入轮回。
- 每人穿着自己最高档的传说武器、传说防具，以及最高档传说饰品。
- 五件法宝都是 10 级：悟空冰魄飞剑、八戒紫金红葫芦、沙僧紫金铃、唐僧混元伞，回春灵叶在背包里。
- 背包里另有几件普通到史诗品质的装备，用来演示悬停说明、出售白装和整理背包。
- 金币 999999。

## 重新生成
服务器运行时，在项目根目录执行 `python Tools/make_test_account.py` 会用同一个账号重新上传这份存档（覆盖云端槽位 1）。
''')
print('doc written')
