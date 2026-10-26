"""Compare five processing factors offline without changing runtime prices."""
import argparse
from fractions import Fraction as F
import hashlib
import json
from pathlib import Path
import re
import runpy

ROOT = Path(__file__).resolve().parents[1]
LIVE = ROOT / 'src/FXDSP/Core/BuildingValues.cs'
REPORT = ROOT / 'docs/building-price-five-schemes.md'
RESULTS = ROOT / 'docs/data/building-price-five-schemes.json'


def generate(check=False):
    old = runpy.run_path(str(ROOT / 'scripts/compare-building-values.py'))
    reference = runpy.run_path(str(ROOT / 'scripts/generate-building-values.py'))
    Scheme, ceil, exact, fixed = (old[key] for key in ('Scheme', 'ceil', 'exact', 'fixed'))
    schemes = (
        Scheme('double', '原 ×2', False, False),
        Scheme('ingredients', '1+k', True, False),
        Scheme('ingredients_half', '1+k/2', True, False, 2),
        Scheme('one_half_plus_half', '1.5+k/2（现行）', True, False, 2, F(3, 2)),
        Scheme('two_plus_half', '2+k/2', True, False, 2, F(2)),
    )
    source = ROOT / 'docs/data/production-recipes.json'
    data_bytes, live_before = source.read_bytes(), LIVE.read_bytes()
    data = json.loads(data_bytes)
    items, bases, chosen, values, eligible = old['calculate'](data, reference, schemes)
    active = reference['PRICING_SCHEME']
    assert active in {scheme.key for scheme in schemes}
    live = {int(item): int(price) for item, price in re.findall(r'\{\s*(\d+),\s*(\d+)L\s*\}', live_before.decode('utf-8'))}
    assert set(live) == set(eligible)
    assert all(live[item] == ceil(values[active][item]) for item in eligible)
    # Hand-derived conveyor values check fractions and recursive processing.
    assert [values[scheme.key][2001] for scheme in schemes] == [F(16, 3), F(8), F(7, 2), F(20, 3), F(45, 4)]

    def factor(scheme, kinds):
        return scheme.factor_constant + F(kinds, scheme.factor_divisor) if scheme.ingredient_factor else F(2)

    def prices(item):
        return [ceil(values[scheme.key][item]) for scheme in schemes]

    def name(item):
        return items[item]['Name']

    def formula(item):
        recipe = chosen[item]
        side = lambda ids, amounts: ' + '.join(f'{amount} {name(part)}' for part, amount in zip(ids, amounts))
        return side(recipe['Items'], recipe['ItemCounts']) + ' → ' + side(recipe['Results'], recipe['ResultCounts'])

    labels = ' | '.join(scheme.name for scheme in schemes)

    def table(ids):
        rows = [f'| ID | 建筑或单位 | {labels} |', '| --- | --- | ' + ' | '.join(['---:'] * 5) + ' |']
        for item in ids:
            rows.append('| ' + ' | '.join([str(item), name(item)] + [f'{price:,}' for price in prices(item)]) + ' |')
        return rows

    digest = hashlib.sha256(data_bytes).hexdigest()
    doc = [
        '# 建筑币五种加工倍数对比', '',
        '**用户已选定 1.5+k/2，0.18.4 起正式采用；其余四列保留对照。比较脚本本身只生成文档和数据，不修改游戏价格或部署。**', '',
        'k 为一道配方中正数量输入的不同物品种类数，不是材料件数，也不是展开后的原矿种类。', '',
        f'使用同一份原版事实表：{len(items)} 种物品、{len(data["Recipes"])} 条配方、{len(eligible)} 项兑换物品。',
        f'配方 SHA256：`{digest}`。现行列已逐项对照正式 `BuildingValues.cs`，71 项一致。', '',
        '## 计算规则', '',
        '设某道配方的投入材料价值总和为 S，批产量为 q；每件价值 = S × 加工倍数 ÷ q。',
        '每一套都独立递归计算全部上游材料，原料基准仍为 1，中间保留精确分数；仅最终建筑/单位兑换价向上取整。五套均不加加工时间倍数，不进行整体币值缩放，表中不含兑换手续费。', '',
        f'| 输入种类 k | {labels} |', '| ---: | ' + ' | '.join(['---:'] * 5) + ' |',
    ]
    max_kinds = max(len(set(recipe['Items'])) for recipe in chosen.values())
    for kinds in range(1, max_kinds + 1):
        doc.append('| ' + ' | '.join([str(kinds)] + [exact(factor(scheme, kinds)) for scheme in schemes]) + ' |')
    doc += ['',
        '例如普通传送带输入铁块、齿轮两种材料，五种倍数分别为 2、3、2、2.5、3；其上游铁块和齿轮各只有一种输入，五种倍数分别为 2、2、1.5、2、2.5。因此不能只对最终传送带的旧价格乘一次比例。', '',
        '沿用同一套原矿/黑雾掉落基准、氢与临界光子基准、普通最小 ID 配方以及联合产物总件数分摊。硫酸、有机晶体、重氢继续按普通配方计算，不按珍奇采集物定为 1。满蓄电器继续视为空蓄电器再加工一道，该道只有一种输入，分别使用 2、2、1.5、2、2.5。两种交付平台不加入兑换。', '',
        '## 重点建筑与单位', '',
    ]
    doc += table([2001, 2002, 2003, 2103, 2104, 2105, 2303, 2305, 2312, 2902, 5002, 5112])
    doc += ['', '## 星际塔与极速传送带比价', '',
            '下表按最终整数价格相除，不含双向手续费。', '',
            '| 加工倍数 | 星际塔单价 | 极速带单价 | 一座星际塔折合极速带数量 |',
            '| --- | ---: | ---: | ---: |']
    for scheme in schemes:
        tower, belt = ceil(values[scheme.key][2104]), ceil(values[scheme.key][2003])
        doc.append(f'| {scheme.name} | {tower:,} | {belt:,} | {fixed(F(tower, belt))} |')
    doc += ['', '## 相对当前正式价格的变化', '',
            '| 加工倍数 | 涨价项 | 相同项 | 降价项 | 最低倍数 | 最高倍数 | 最高倍数物品 |',
            '| --- | ---: | ---: | ---: | ---: | ---: | --- |']
    for scheme in schemes:
        ratios = {item: F(ceil(values[scheme.key][item]), live[item]) for item in eligible}
        largest = max(ratios.values())
        most = [name(item) for item in eligible if ratios[item] == largest]
        names = '全部 71 项相同' if all(ratio == 1 for ratio in ratios.values()) else '、'.join(most[:3]) + (f'等 {len(most)} 项' if len(most) > 3 else '')
        doc.append('| ' + ' | '.join([scheme.name, str(sum(v > 1 for v in ratios.values())),
            str(sum(v == 1 for v in ratios.values())), str(sum(v < 1 for v in ratios.values())),
            fixed(min(ratios.values())) + '×', fixed(largest) + '×', names]) + ' |')
    doc += ['', '这些是逐层加工后的结果。常数项从 1 增加到 1.5 或 2，会同时提高单材料加工和多材料加工的倍数，深层产物会反复承受这项加成。', '',
            '## 全部兑换价格', '']
    doc += table(eligible)
    doc += ['', '## 兑换物品配方与种类数', '',
            '| ID | 建筑或单位 | 本批配方 | 输入种类 k | 总产出 q |',
            '| --- | --- | --- | ---: | ---: |']
    for item in eligible:
        recipe = chosen[item]
        doc.append(f'| {item} | {name(item)} | {formula(item)} | {len(set(recipe["Items"]))} | {sum(recipe["ResultCounts"])} |')
    doc += ['', '## 上游材料价值', '',
            '仅显示非基准、非兑换物品；基准物品五套均为 1。此处为显示方便保留六位小数，计算始终使用精确分数。', '',
            f'| ID | 材料或其它物品 | {labels} |', '| --- | --- | ' + ' | '.join(['---:'] * 5) + ' |']
    for item in sorted(set(items) - set(eligible) - set(bases)):
        doc.append('| ' + ' | '.join([str(item), name(item)] + [fixed(values[scheme.key][item], 6) for scheme in schemes]) + ' |')
    doc += ['', '## 复算', '',
            '运行 `python scripts/compare-five-building-values.py` 生成；加 `--check` 仅验证文档和离线数据，不写入文件。',
            '五种方案的 175 项精确价值与 71 项整数兑换价保存在 `docs/data/building-price-five-schemes.json`。',
            '普通传送带五套精确价值依次为 16/3、8、7/2、20/3、45/4，最终价格为 6、8、4、7、12；当前 71 项价格全部与正式源码一致。', '']
    payload = {
        'Status': 'offline-only', 'AppliedScheme': active, 'RecipeFactsSha256': digest,
        'RuntimePricesSha256': hashlib.sha256(live_before).hexdigest(),
        'Schemes': [{'Key': scheme.key, 'Name': scheme.name,
                     'Constant': exact(scheme.factor_constant) if scheme.ingredient_factor else '2',
                     'CoefficientOfIngredientKinds': exact(F(1, scheme.factor_divisor)) if scheme.ingredient_factor else '0'} for scheme in schemes],
        'Items': [],
    }
    for item in sorted(items):
        row = {'ID': item, 'Name': name(item), 'ExchangeEligible': item in eligible,
               'Values': {scheme.key: exact(values[scheme.key][item]) for scheme in schemes}}
        if item in eligible:
            row['ExchangePrices'] = {scheme.key: ceil(values[scheme.key][item]) for scheme in schemes}
        if item in bases:
            row['BaseReason'] = bases[item]
        else:
            row['Recipe'] = chosen[item]
        payload['Items'].append(row)
    outputs = {REPORT: '\n'.join(doc), RESULTS: json.dumps(payload, ensure_ascii=False, indent=2) + '\n'}
    for path, content in outputs.items():
        if check:
            if path.read_text(encoding='utf-8') != content:
                raise ValueError(f'离线结果不同: {path}')
        else:
            path.write_text(content, encoding='utf-8')
    assert LIVE.read_bytes() == live_before, '正式价格被修改'
    print('Five schemes / 175 items / 71 exchange prices; runtime prices unchanged.')
    for item in [2001, 2003, 2103, 2104, 2105]:
        print(str(item) + ': ' + ' / '.join(f'{price:,}' for price in prices(item)))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    generate(parser.parse_args().check)
