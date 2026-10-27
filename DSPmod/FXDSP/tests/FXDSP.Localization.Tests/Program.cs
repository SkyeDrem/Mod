using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FXDSP.Core;
using FXDSP.UI;
using FXDSP.DeliveryBootstrap;

internal static class Program
{
    private static int passed;
    private static bool Chinese(string value) { return Regex.IsMatch(value, "[\\u4e00-\\u9fff]"); }
    private static void Main()
    {
        Check("Chinese remains exactly unchanged", () => {
            TextLocalization.English = false;
            Require(TextLocalization.Translate(IntroductionCopy.Body) == IntroductionCopy.Body);
            Require(TextLocalization.Translate("已平仓：本次 +1,234.50 铜矿。") == "已平仓：本次 +1,234.50 铜矿。");
        });
        Check("embedded English catalog is complete for all declared literals", () => {
            Require(TextLocalization.EntryCount >= 349);
            using (var stream = typeof(TextLocalization).Assembly.GetManifestResourceStream("FXDSP.Localization.en-US.tsv"))
            using (var reader = new StreamReader(stream))
            {
                string line; int count = 0;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Length == 0 || line[0] == '#') continue;
                    int separator = line.IndexOf('\t');
                    string source = Decode(line.Substring(0, separator)), target = Decode(line.Substring(separator + 1));
                    Require(TextLocalization.Translate(source) == target && !Chinese(target)); count++;
                }
                Require(count >= 349);
            }
        });
        Check("dynamic financial labels retain signs separators and numerical precision", () => {
            string translated = TextLocalization.Translate("浮动盈亏    -12,345.67 电磁矩阵");
            Require(translated.Contains("-12,345.67") && translated.Contains("Electromagnetic Matrix") && !Chinese(translated));
            Require(TextLocalization.Translate("最新 1.2345") == "Last 1.2345");
            Require(TextLocalization.Translate("手续费 10.00%  25.00\n买入需 275.00 建筑币\n回收得 225.00 建筑币").Contains("275.00"));
        });
        Check("historical Chinese logs format naturally in English without rewriting saves", () => {
            var market = new MarginMarket(); market.Deposit(2000); market.Open(PositionSide.Long, 1234.50m, 1);
            byte[] before = Bytes(market);
            Require(TradingLogDisplay.Format("开多1,234.50保证金") == "Open long 1,234.50 margin");
            Require(TradingLogDisplay.Format("已平仓：本次 -12.345678 铜矿。").Contains("-12.35 Copper Ore."));
            foreach (string log in market.RecentLogs) Require(!Chinese(TextLocalization.Translate(TradingLogDisplay.Format(log))));
            Require(before.SequenceEqual(Bytes(market)));
        });
        Check("welcome preserves nine paragraphs and all twelve native-safe emphasis tags", () => {
            string body = TextLocalization.Translate(IntroductionCopy.Body);
            Require(!Chinese(body) && body.Split(new[] { "\n\n" }, StringSplitOptions.None).Length == 9);
            Require(Regex.Matches(body, "<color=").Count == 12 && Regex.Matches(body, "</color>").Count == 12);
            Require(body.Contains("<color=#D45954>long</color>") && body.Contains("<color=#52BA80>short</color>"));
            Require(!body.Contains("<b>") && !body.Contains("**") && body.Contains("<color=#69CCD6>F8</color>"));
            Require(body.Contains("FX Warrior Icarus"));
        });
        Check("technology names descriptions and effect lines are translated independently", () => {
            foreach (var node in TradeResearch.Definitions)
            {
                Require(!Chinese(TextLocalization.Translate(node.Name)) && !Chinese(TextLocalization.Translate(node.Description)));
                string effect = TextLocalization.Translate(node.UnlockText);
                Require(!Chinese(effect));
                if (node.Id >= 7801) Require(effect.Split('\n').Length == 2);
            }
        });
        Check("delivery names and complete descriptions translate without altering definitions", () => {
            foreach (var definition in DeliveryRecipes.Definitions)
            {
                Require(Chinese(definition.Description));
                Require(!Chinese(TextLocalization.Translate(definition.Description)));
                Require(TextLocalization.Translate(definition.Name).Contains("Delivery Platform"));
            }
        });
        Check("switching languages and translating English twice are stable", () => {
            string english = TextLocalization.Translate("交易系统");
            Require(english == "Trading System" && TextLocalization.Translate(english) == english);
            TextLocalization.English = false; Require(TextLocalization.Translate("交易系统") == "交易系统");
            TextLocalization.English = true; Require(TextLocalization.Translate("交易系统") == english);
        });
        Check("native prototype translation preserves stable registration identity", () => {
            var definition = TradeResearch.Find(TradeResearch.AccessTech);
            var tech = new TechProto { ID = definition.Id, Name = definition.Name, name = definition.Name };
            PrototypeOwnership.Mark("tech", tech.ID, tech); LDB.techs.Entries[tech.ID] = tech;
            PrototypeTranslation.Apply(tech);
            Require(tech.Name == "交易系统" && tech.name == "Trading System" && !Chinese(tech.description));
            Localization.isZHCN = true; PrototypeTranslation.Apply(tech);
            Require(tech.name == definition.Name && tech.description == definition.Description);
        });
        Check("foreign prototypes sharing our IDs are never renamed", () => {
            var foreign = new TechProto { ID = TradeResearch.AccessTech, name = "Foreign", description = "Keep" };
            PrototypeTranslation.Apply(foreign); Require(foreign.name == "Foreign" && foreign.description == "Keep");
            var item = new ItemProto { ID = DeliveryBuildings.Planetary, name = "Foreign tower", description = "Keep" };
            PrototypeTranslation.Apply(item); Require(item.name == "Foreign tower" && item.description == "Keep");
        });
        Check("delivery item and recipe both follow language changes", () => {
            var definition = DeliveryRecipes.Definitions[0];
            var item = new ItemProto { ID = definition.ItemId, Name = definition.Name };
            var recipe = new RecipeProto { ID = definition.ItemId, Name = definition.Name };
            PrototypeOwnership.Mark("item", item.ID, item); PrototypeOwnership.Mark("recipe", recipe.ID, recipe);
            PrototypeTranslation.Apply(item); PrototypeTranslation.Apply(recipe);
            Require(item.name == "Planetary Delivery Platform" && recipe.name == item.name && !Chinese(item.description));
            Localization.isZHCN = true; PrototypeTranslation.Apply(item); PrototypeTranslation.Apply(recipe);
            Require(item.name == definition.Name && recipe.description == definition.Description && item.Name == definition.Name);
        });
        Check("inline prerequisite names update only owned tags and preserve icon data", () => {
            var tech = new TechProto { ID = 7701, Name = "建筑兑换" }; PrototypeOwnership.Mark("tech", tech.ID, tech);
            var ours = new Icon { tag = "fx7701", name = "old", UvMarker = 10 };
            var foreign = new Icon { tag = "other", name = "Keep", UvMarker = 20 };
            Configs.builtin.generalIconMapping.iconMappings = new[] { ours, foreign };
            PrototypeTranslation.Apply(tech);
            Require(ours.name == "Building Exchange" && ours.UvMarker == 10 && foreign.name == "Keep" && foreign.UvMarker == 20);
        });
        Check("native item names can replace fallback names in historical messages", () => {
            TextLocalization.RegisterNativeName("测试矿石", "Test Ore");
            Require(TextLocalization.Translate("存入 10 测试矿石。") == "Deposited 10 Test Ore.");
            TextLocalization.RegisterNativeName("测试矿石", "Renamed Ore");
            Require(TextLocalization.Translate("存入 10 测试矿石。") == "Deposited 10 Renamed Ore.");
        });
        Check("settlement logs show fee-adjusted returns in both languages and preserve saved data", () => {
            var market = new MarginMarket(); market.Deposit(1000); market.Open(PositionSide.Long, 100, 5); market.Close();
            var result = market.LastSettlement; Require(result.ReturnRatio == result.NetPnL / result.Margin);
            string source = market.RecentLogs[market.RecentLogs.Count - 1];
            string rate = MoneyDisplay.Percent(result.ReturnRatio);
            Require(source.Contains("收益率 " + rate));
            byte[] before = Bytes(market);
            string english = TradingLogDisplay.Format(source);
            Require(english.Contains("Return " + rate) && !Chinese(english));
            TextLocalization.English = false; Require(TradingLogDisplay.Format(source).Contains("收益率 " + rate));
            Require(before.SequenceEqual(Bytes(market)));
        });
        Check("unknown external text and identifiers pass through safely", () => {
            Require(TextLocalization.Translate("External Mod [abc] -1,234.50 / 1.2345") == "External Mod [abc] -1,234.50 / 1.2345");
            Require(TextLocalization.Translate("\\fx1951-;") == "\\fx1951-;");
        });
        Check("market and portfolio snapshots expose the same localized position percentage", () => {
            var market = new FXDSP.Runtime.MarketSnapshot { HasPosition = true, PositionMargin = 100, UnrealizedPnL = 12.5m, ReturnRatio = 3m };
            var position = new FXDSP.Runtime.PositionSnapshot { Margin = 100, UnrealizedPnL = 12.5m, Leverage = 50 };
            Require(market.UnrealizedReturnRatio == .125m && position.UnrealizedReturnRatio == market.UnrealizedReturnRatio);
            Require(MoneyDisplay.Percent(market.UnrealizedReturnRatio) == "+12.50%");
            TextLocalization.English = false; Require(MoneyDisplay.Percent(position.UnrealizedReturnRatio) == "+12.50%");
            market.HasPosition = false; Require(MoneyDisplay.Percent(market.UnrealizedReturnRatio) == "—");
            position.Margin = 0; Require(MoneyDisplay.Percent(position.UnrealizedReturnRatio) == "—");
        });
        Check("holding time snapshots agree and labels switch language without showing units", () => {
            long currentTick = 17 + MarginMarket.QuoteIntervalTicks * 2;
            var market = new FXDSP.Runtime.MarketSnapshot { HasPosition = true, PositionOpenTick = 17, CurrentTick = currentTick };
            var position = new FXDSP.Runtime.PositionSnapshot { OpenTick = 17, CurrentTick = currentTick };
            Require(market.HeldCandles == 2 && position.HeldCandles == 2);
            Require(TextLocalization.Translate("持有时间 2") == "Holding Time 2");
            Require(TextLocalization.Translate("收益率") == "Return");
            market.HasPosition = false; Require(!market.HeldCandles.HasValue);
            TextLocalization.English = false; Require(TextLocalization.Translate("持有时间 2") == "持有时间 2");
        });
        Check("all achievement names descriptions status and notifications translate and restore Chinese", () => {
            foreach(var achievement in AchievementCatalog.All)
            {
                string name=TextLocalization.Translate(achievement.Name), description=TextLocalization.Translate(achievement.Description);
                Require(!Chinese(name) && !Chinese(description));
                Require(!Chinese(TextLocalization.Translate("成就达成 · "+achievement.Name)));
                TextLocalization.English=false;
                Require(TextLocalization.Translate(achievement.Name)==achievement.Name && TextLocalization.Translate(achievement.Description)==achievement.Description);
                TextLocalization.English=true;
            }
            Require(TextLocalization.Translate("成就    3 / 18")=="Achievements    3 / 18");
            Require(TextLocalization.Translate("已达成")=="Achieved" && TextLocalization.Translate("未达成")=="Not Achieved");
            Require(AchievementCatalog.Find(AchievementId.NoFactory).Name=="工厂？不需要了！");
            Require(TextLocalization.Translate(AchievementCatalog.Find(AchievementId.Leaderboard).Description).Contains("no prior withdrawals"));
        });
        TextLocalization.English = false;
        Console.WriteLine("All " + passed + " localization checks passed.");
    }
    private static void Check(string name, Action action)
    { TextLocalization.English = true; Localization.isZHCN = false; action(); passed++; Console.WriteLine("PASS " + name); }
    private static string Decode(string text) { return text.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t").Replace("\\\\", "\\"); }
    private static byte[] Bytes(MarginMarket market) { using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){market.Export(writer);return stream.ToArray();} }
    private static void Require(bool condition) { if (!condition) throw new Exception("Localization invariant failed."); }
}
