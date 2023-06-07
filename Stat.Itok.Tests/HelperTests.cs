using Newtonsoft.Json;
using Stat.Itok.Core;
using Stat.Itok.Core.Helpers;

namespace Stat.Itok.Tests
{
    [TestClass]
    public class HelperTests
    {
        [TestMethod]
        public void TestGenGraphQLBody()
        {
            var res = StatInkHelper.BuildGraphQLBody("xx", "name_1", "val_1");
        }

        [TestMethod]
        public void GetBattleIdForStatInk()
        {
            var res = BattleHelper.GetBattleIdForStatInk(
                "VnNIaXN0b3J5RGV0YWlsLXUtYXZzZHRtcXd4emJxZnNmamFubW06QkFOS0FSQToyMDIyMTEwOFQxMTU2NDJfNDExNTI2NzQtZDA1NC00ZWQ2LWFjMDMtZThiNmU3ZDhjMmY3");
            Assert.AreEqual(res, "9bd66c87-afa8-5fc6-8f3d-e92c8f2daf2f");
        }

        [TestMethod]
        public void TestConvertToStatInkBody()
        {
            var groupStr = File.ReadAllText("./convert/1/Group.json");
            var detailStr = File.ReadAllText("./convert/1/Detail.json");
            var res = BattleHelper.BuildStatInkBattleBody(detailStr, groupStr);
        }


        [TestMethod]
        public void TestConvertToStatInkBody_1()
        {
            var groupStr = File.ReadAllText("./samples/Detail_XMatch/0_group.json");
            var detail_1 = File.ReadAllText("./samples/Detail_XMatch/1.json");
            var res = BattleHelper.BuildStatInkBattleBody(detail_1, groupStr);
        }

        [TestMethod]
        public void TestWebViewParse()
        {
            var str = File.ReadAllText("./samples/resp/webview.txt");
            var res = StatInkHelper.ParseNinWebViewData(str);
        }

        [TestMethod]
        public void TestRConfigParse()
        {
            var str = File.ReadAllText("./samples/resp/nin_misc_config.json");
            var res = JsonConvert.DeserializeObject<NinMiscConfig>(str);
        }

        [TestMethod]
        public void TestTriColorConvert()
        {
            var groupStr = File.ReadAllText("./samples/tricolor/0/group.json");
            var detail_1 = File.ReadAllText("./samples/tricolor/0/detail.json");
            var res = BattleHelper.BuildStatInkBattleBody(detail_1, groupStr);
            Assert.IsFalse(string.IsNullOrEmpty(res.OurTeamColor));
            Assert.IsFalse(string.IsNullOrEmpty(res.TheirTeamColor));
            Assert.IsFalse(string.IsNullOrEmpty(res.ThirdTeamColor));
        }

        [TestMethod]
        public void TestXColorConvert()
        {
            var groupStr = File.ReadAllText("./samples/Detail_XMatch/0_group.json");
            var detail_1 = File.ReadAllText("./samples/Detail_XMatch/1.json");
            var res = BattleHelper.BuildStatInkBattleBody(detail_1, groupStr);
            Assert.IsFalse(string.IsNullOrEmpty(res.OurTeamColor));
            Assert.IsFalse(string.IsNullOrEmpty(res.TheirTeamColor));
            Assert.IsTrue(string.IsNullOrEmpty(res.ThirdTeamColor));
        }

        [TestMethod]
        public void TestRankedColorConvert()
        {
            var groupStr = File.ReadAllText("./convert/1/Group.json");
            var detail_1 = File.ReadAllText("./convert/1/Detail.json");
            var res = BattleHelper.BuildStatInkBattleBody(detail_1, groupStr);
            Assert.IsFalse(string.IsNullOrEmpty(res.OurTeamColor));
            Assert.IsFalse(string.IsNullOrEmpty(res.TheirTeamColor));
            Assert.IsTrue(string.IsNullOrEmpty(res.ThirdTeamColor));
        }

        [TestMethod]
        public void TestXMatch_0()
        {
            var groupStr = File.ReadAllText("./samples/Detail_XMatch/0_group.json");
            var detail_1 = File.ReadAllText("./samples/Detail_XMatch/1.json");
            var res = BattleHelper.BuildStatInkBattleBody(detail_1, groupStr);
            Assert.AreEqual(res.ChallengeWin, 2);
            Assert.AreEqual(res.ChallengeLose, 3);
            Assert.AreEqual(res.OurTeamPlayers[0].Crown, StatInkBoolean.No);
        }

        [TestMethod]
        public void TestSalmonRun_0()
        {
            var groupStr = File.ReadAllText("./samples/salmon/list.json");
            var detailStr = File.ReadAllText("./samples/salmon/detail_0.json");
            var res = BattleHelper.BuildStatInkSalmonBody(detailStr, groupStr, "xx", new Dictionary<string, string>());
        }

        [TestMethod]
        public void TestSalmonRun_1()
        {
            var groupStr = File.ReadAllText("./samples/salmon/1/list.json");
            var detailStr = File.ReadAllText("./samples/salmon/1/detail_0.json");
            var res = BattleHelper.BuildStatInkSalmonBody(detailStr, groupStr, "xx", new Dictionary<string, string>());
        }

        [TestMethod]
        public void TestSalmonRun_4()
        {
            var groupStr = File.ReadAllText("./samples/salmon/4/list.json");
            var detailStr = File.ReadAllText("./samples/salmon/4/detail_0.json");
            var res = BattleHelper.BuildStatInkSalmonBody(detailStr, groupStr, "xx", new Dictionary<string, string>());
        }

        [TestMethod]
        public void TestSalmonRun_2()
        {
            var groupStr = File.ReadAllText("./samples/salmon/2/list.json");
            var detailStr = File.ReadAllText("./samples/salmon/2/detail_0.json");
            var res = BattleHelper.BuildStatInkSalmonBody(detailStr, groupStr, "xx", new Dictionary<string, string>());
        }
        
        
        [TestMethod]
        public void TestEventBattle_0()
        {
            var groupStr = File.ReadAllText("./samples/EventBattleHistoriesQuery/0/group.json");
            var detailStr = File.ReadAllText("./samples/EventBattleHistoriesQuery/0/detail_1.json");
            var res = BattleHelper.BuildStatInkBattleBody(detailStr, groupStr, "zh-cn", new Dictionary<string, string>());
        }
    }
}