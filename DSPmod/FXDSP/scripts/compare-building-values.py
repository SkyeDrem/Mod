"""Offline pricing experiments. Never writes runtime prices or deploys a plugin."""
import argparse
from dataclasses import dataclass
from fractions import Fraction
import hashlib
import json
from pathlib import Path
import re
import runpy

ROOT = Path(__file__).resolve().parents[1]
DATA = ROOT / 'docs/data/production-recipes.json'
LIVE = ROOT / 'src/FXDSP/Core/BuildingValues.cs'
REPORT = ROOT / 'docs/building-price-scenarios.md'
RESULTS = ROOT / 'docs/data/building-price-scenarios.json'


@dataclass(frozen=True)
class Scheme:
    key: str
    name: str
    ingredient_factor: bool
    time_factor: bool
    factor_divisor: int = 1
    factor_constant: Fraction = Fraction(1)


SCHEMES = (
    Scheme('current', '原方案：每道加工乘二', False, False),
    Scheme('ingredients', '方案一：仅材料种类', True, False),
    Scheme('time', '方案二：仅加工时间', False, True),
    Scheme('combined', '方案三：材料种类与时间', True, True),
)


def ceil(value):
    return (value.numerator + value.denominator - 1) // value.denominator


def exact(value):
    return str(value.numerator) if value.denominator == 1 else f'{value.numerator}/{value.denominator}'


def fixed(value, digits=2):
    """Display rounding only; do not pass any rounded value back into the model."""
    negative = value < 0
    value = abs(value)
    scale = 10 ** digits
    scaled = (value.numerator * scale * 2 + value.denominator) // (2 * value.denominator)
    integer, decimal = divmod(scaled, scale)
    return ('-' if negative else '') + f'{integer:,}.{decimal:0{digits}d}'


def recipe_parameters(recipe):
    count = sum(recipe['ResultCounts'])
    kinds = len({item for item, amount in zip(recipe['Items'], recipe['ItemCounts']) if amount > 0})
    seconds = Fraction(recipe['TimeSpend'], 60)
    per_item = seconds / count
    return count, kinds, per_item, 1 + per_item / 60


def calculate(data, reference, schemes=None):
    items = {item['ID']: item for item in data['Items']}
    recipes = sorted(data['Recipes'], key=lambda recipe: recipe['ID'])
    bases = {item['ID']: '原矿/水/木材等直接采集物' for item in items.values() if item['Type'] == 1}
    bases.update({item: '黑雾专属掉落' for item in range(5201, 5207)})
    bases.update(reference['SPECIAL_BASES'])
    chosen = {}
    for item in items:
        if item in bases:
            continue
        if item == 2207:
            chosen[item] = {'ID': 0, 'TimeSpend': 0, 'Items': [2206], 'ItemCounts': [1],
                            'Results': [2207], 'ResultCounts': [1]}
        else:
            chosen[item] = next(recipe for recipe in recipes if item in recipe['Results'])
    values = {}
    if schemes is None:
        schemes = SCHEMES + (Scheme('ingredients_half', '1+k/2', True, False, 2),
                             Scheme('one_half_plus_half', '1.5+k/2', True, False, 2, Fraction(3, 2)))
    for scheme in schemes:
        cache, active = {}, set()

        def value(item):
            if item in cache:
                return cache[item]
            if item in bases:
                cache[item] = Fraction(1)
                return cache[item]
            if item in active:
                raise ValueError(f'循环配方: {item}')
            active.add(item)
            try:
                recipe = chosen[item]
                output, kinds, _, time = recipe_parameters(recipe)
                inputs = sum((value(part) * amount for part, amount in zip(recipe['Items'], recipe['ItemCounts'])), Fraction(0))
                processing = scheme.factor_constant + Fraction(kinds, scheme.factor_divisor) if scheme.ingredient_factor else 2
                result = inputs * processing / output * (time if scheme.time_factor else 1)
                if result <= 0:
                    raise ValueError(f'非正价格: {item}')
                cache[item] = result
                return result
            finally:
                active.remove(item)

        for item in items:
            value(item)
        values[scheme.key] = cache
    eligible = sorted((item['ID'] for item in items.values() if item['CanBuild'] or item['ID'] in reference['UNITS']))
    return items, bases, chosen, values, eligible


def verify(chosen, values, eligible, applied):
    # Independent hand calculations catch time units, batch size, recursive rounding
    # and ingredient-count-vs-quantity mistakes before generating a review document.
    assert recipe_parameters(chosen[2001]) == (3, 2, Fraction(1, 3), Fraction(181, 180))
    assert recipe_parameters(chosen[2003]) == (3, 3, Fraction(1, 3), Fraction(181, 180))
    assert recipe_parameters(chosen[2103]) == (1, 4, Fraction(20), Fraction(4, 3))
    assert recipe_parameters(chosen[2104]) == (1, 3, Fraction(30), Fraction(3, 2))
    assert values['current'][2001] == Fraction(16, 3)
    assert values['ingredients'][2001] == 8
    assert values['time'][2001] == Fraction(1335961, 243000)
    assert values['combined'][2001] == Fraction(1335961, 162000)
    current = {int(item): int(price) for item, price in re.findall(r'\{\s*(\d+),\s*(\d+)L\s*\}', LIVE.read_text(encoding='utf-8'))}
    assert set(current) == set(eligible)
    assert all(current[item] == ceil(values[applied][item]) for item in eligible), '已选方案与游戏价格不同'
    for item in values['current']:
        assert values['time'][item] >= values['current'][item]
        assert values['ingredients'][item] >= values['current'][item]
        assert values['combined'][item] >= max(values['time'][item], values['ingredients'][item])


def generate(check=False):
    live_before = LIVE.read_bytes()
    data_bytes = DATA.read_bytes()
    data = json.loads(data_bytes)
    # Read shared catalog constants without invoking the production generator.
    reference = runpy.run_path(str(ROOT / 'scripts/generate-building-values.py'))
    items, bases, chosen, values, eligible = calculate(data, reference)
    applied = reference['PRICING_SCHEME']
    verify(chosen, values, eligible, applied)
    digest = hashlib.sha256(data_bytes).hexdigest()

    def name(item):
        return items[item]['Name']

    def formula(recipe):
        side = lambda ids, counts: ' + '.join(f'{amount} {name(item)}' for item, amount in zip(ids, counts))
        return side(recipe['Items'], recipe['ItemCounts']) + ' → ' + side(recipe['Results'], recipe['ResultCounts'])

    def prices(item):
        return [ceil(values[scheme.key][item]) for scheme in SCHEMES]

    def price_row(item):
        return '| ' + ' | '.join([str(item), name(item)] + [f'{price:,}' for price in prices(item)]) + ' |'

    def comparison_table(ids):
        return ['| ID | 建筑或单位 | 原×2方案 | 一：原材料种类方案 | 二：加工时间 | 三：两者结合 |',
                '| --- | --- | ---: | ---: | ---: | ---: |'] + [price_row(item) for item in ids]

    doc = [
        '# 建筑币四套方案对比（离线研究）', '',
        '**历史对照：0.18.2 曾采用 k+1，0.18.3 曾采用 1+k/2；0.18.4 已采用 1.5+k/2，不叠加时间。当前正式价格见 building-values-0.14.0.md，本报告保留原四套数值。**', '',
        f'原始数据：本机原版配方，{len(items)} 种物品、{len(data["Recipes"])} 条配方、{len(eligible)} 项兑换物品。',
        f'配方事实 SHA256：`{digest}`。当前所选规则 {applied} 已另行逐项对照 `BuildingValues.cs`，{len(eligible)} 项完全一致；下方四列是历史研究方案。', '',
        '## 四套公式与取整', '',
        '设某配方投入的材料价值总和为 S，正数量输入中不同物品 ID 的个数为 k，整批总产出件数为 q，配方耗时为 t 秒。',
        '每件加工耗时 x = t/q，额外时间倍数 T = 1 + x/60 = 1 + t/(60q)。原型 TimeSpend 是 tick，须先除以 60 换算为秒。', '',
        '| 方案 | 本道加工后每件精确价值 |', '| --- | --- |',
        '| 原×2方案 | S × 2 ÷ q |', '| 一：原材料种类方案 | S × (k+1) ÷ q |',
        '| 二：仅增加加工时间倍数 | S × 2 ÷ q × T |',
        '| 三：两项同时采用 | S × (k+1) ÷ q × T |', '',
        '四套均从原料递归计算上游材料，用各自方案的材料值代入 S；不是只对最终建筑再乘一次。中间全部使用精确分数，最终兑换价格向上取整；表中不含买入/回收手续费，不追加整体缩放或新的原料权重。', '',
        '“材料种类”只数不同输入物品，不数数量，也不数展开后的原矿种类。例如传送带两种输入倍数为 3；行星塔四种输入倍数为 5；星际塔三种输入倍数为 4。', '',
        '## 沿用的配方与特殊约定', '',
        '- 原矿、水、原油等直接采集物及六种黑雾掉落仍为 1；氢与临界光子保留现行基准 1。硫酸、有机晶体、重氢仍走原来的普通加工路线。',
        '- 多配方继续采用当前最小配方 ID 的普通路线，不加入珍奇、高效、增产或设备速度修正。',
        '- 多产物继续按全部产物件数之和 q 分摊成本和加工时间，确保只比较本次两项参数；没有顺便改变联合产物分摊方法。',
        '- 满蓄电器保留“空蓄电器再加工一道”的现行折算：一种输入，材料倍数仍为 2。充电没有配方 TimeSpend，因此本次 t=0、额外时间倍数为 1；上游空蓄电器仍完整应用对应方案。这是暂定约定，不代表实际充电耗时为零。',
        '- 两种交付平台仍不在兑换目录内。', '',
        '## 重点建筑', '',
    ]
    doc += comparison_table([2001, 2002, 2003, 2103, 2104, 2105, 2305, 2312, 2902])
    doc += ['', '## 一座星际塔能换多少条极速传送带', '',
            '用各方案最终整数兑换价相除，不计手续费。', '', '| 方案 | 星际塔 ÷ 极速传送带 |', '| --- | ---: |']
    for scheme in SCHEMES:
        ratio = Fraction(ceil(values[scheme.key][2104]), ceil(values[scheme.key][2003]))
        doc.append(f'| {scheme.name} | {fixed(ratio)} |')
    doc += ['', '## 价格变化概览', '',
            '各行相对原×2方案最终整数兑换价。倍数为逐项比较，不是把最终统一乘某个系数。', '',
            '| 新方案 | 上涨项 | 不变项 | 最低价格倍数 | 最高价格倍数 | 最高倍数物品 |',
            '| --- | ---: | ---: | ---: | ---: | --- |']
    for scheme in SCHEMES[1:]:
        ratios = {item: Fraction(ceil(values[scheme.key][item]), ceil(values['current'][item])) for item in eligible}
        maximum = max(ratios.values())
        most = '、'.join(name(item) for item in eligible if ratios[item] == maximum)
        doc.append(f'| {scheme.name} | {sum(ratio > 1 for ratio in ratios.values())} | {sum(ratio == 1 for ratio in ratios.values())} | {fixed(min(ratios.values()))}× | {fixed(maximum)}× | {most} |')
    doc += ['', '材料种类和时间倍数都作用于每道上游加工，会沿生产链累积；最终价格增幅通常大于最终建筑这一步的倍数变化。时间一列不等于此前讨论的“加工工时加法定价”，仍是逐层相乘。', '',
            '## 全部兑换价格', '']
    doc += comparison_table(eligible)
    doc += ['', '## 兑换物品本批配方与新增倍数', '',
            '| ID | 建筑或单位 | 本批配方 | k | q | 每件耗时 x（秒） | 材料倍数 k+1 | 时间倍数 T |',
            '| --- | --- | --- | ---: | ---: | ---: | ---: | ---: |']
    for item in eligible:
        recipe = chosen[item]
        output, kinds, per_item, time = recipe_parameters(recipe)
        source = '1 蓄电器 → 1 蓄电器（满）；充电耗时暂不计' if item == 2207 else formula(recipe)
        doc.append(f'| {item} | {name(item)} | {source} | {kinds} | {output} | {exact(per_item)} | {kinds+1} | {exact(time)} |')
    doc += ['', '## 上游材料价值对照', '',
            '下表仅作显示保留六位小数；后续加工使用 JSON 中的精确分数，不使用这里显示的近似值。原矿等基准物品四套均为 1，省略重复行。', '',
            '| ID | 材料或其它物品 | 采用的本批配方 | 原×2方案 | 一：原材料种类方案 | 二：加工时间 | 三：两者结合 |',
            '| --- | --- | --- | ---: | ---: | ---: | ---: |']
    for item in sorted(set(items) - set(eligible) - set(bases)):
        doc.append('| ' + ' | '.join([str(item), name(item), formula(chosen[item])] + [fixed(values[scheme.key][item], 6) for scheme in SCHEMES]) + ' |')
    doc += ['', '## 手算核对与复算', '',
            '- 普通传送带：原×2方案精确价 16/3，取整为 6；历史方案一精确价 8。其铁块和齿轮均只有一种输入，仍乘 2，只有传送带自身改成两种输入乘 3。',
            '- 铁块的时间倍数为 61/60，齿轮也为 61/60。方案二传送带精确值为 1335961/243000，方案三为 1335961/162000，分别取整为 6 和 9；验证时间加成递归进入材料，且没有中途取整。',
            '- 极速传送带配方为 1 秒产 3 条，x=1/3，T=181/180；三种输入在方案一、三中乘 4。',
            '- 行星塔配方 20 秒产 1 座，T=4/3；星际塔 30 秒产 1 座，T=3/2。', '',
            '运行 `python scripts/compare-building-values.py` 生成；加 `--check` 只读验证报告与离线数据一致。',
            '精确分数、各物品所选配方、每件耗时及四套最终价格保存在 `docs/data/building-price-scenarios.json`。此脚本不调用正式价格生成或部署脚本，不写入 `BuildingValues.cs`。', '']

    payload = {
        'Status': 'historical-comparison', 'AppliedScheme': applied, 'RecipeFactsSha256': digest,
        'CurrentRuntimePricesSha256': hashlib.sha256(live_before).hexdigest(),
        'Rules': {'TimeSpendTicksPerSecond': 60, 'TimeDenominatorSeconds': 60,
                  'CoproductAllocation': 'total output counts; shared with current scheme',
                  'FullAccumulatorChargingSeconds': None, 'FullAccumulatorTimeFactor': '1',
                  'Rounding': 'exact fractions recursively; ceiling exchange prices only'},
        'Schemes': [{'Key': scheme.key, 'Name': scheme.name, 'IngredientFactor': scheme.ingredient_factor,
                     'TimeFactor': scheme.time_factor} for scheme in SCHEMES],
        'Items': [],
    }
    for item in sorted(items):
        row = {'ID': item, 'Name': name(item), 'ExchangeEligible': item in eligible,
               'Values': {scheme.key: exact(values[scheme.key][item]) for scheme in SCHEMES}}
        if item in eligible:
            row['ExchangePrices'] = {scheme.key: ceil(values[scheme.key][item]) for scheme in SCHEMES}
        if item in bases:
            row['BaseReason'] = bases[item]
        else:
            recipe = chosen[item]
            output, kinds, per_item, time = recipe_parameters(recipe)
            row.update({'Recipe': recipe, 'IngredientKinds': kinds, 'BatchOutput': output,
                        'SecondsPerItem': exact(per_item), 'TimeMultiplier': exact(time)})
        payload['Items'].append(row)
    outputs = {REPORT: '\n'.join(doc), RESULTS: json.dumps(payload, ensure_ascii=False, indent=2) + '\n'}
    for path, content in outputs.items():
        if check:
            if path.read_text(encoding='utf-8') != content:
                raise ValueError(f'离线生成结果不同: {path}')
        else:
            path.write_text(content, encoding='utf-8')
    assert LIVE.read_bytes() == live_before, '正式兑换价格被意外修改'
    print(f'{len(eligible)} exchange items / {len(items)} total items, four exact scenarios verified; runtime prices unchanged.')
    for item in [2001, 2003, 2103, 2104, 2105]:
        print(f'{item}: ' + ' / '.join(f'{price:,}' for price in prices(item)))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true', help='Verify generated offline artifacts without writing them.')
    generate(parser.parse_args().check)
