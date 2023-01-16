using Newtonsoft.Json;
using Stat.Itok.Core;

namespace Stat.Itok.Tests
{
    [TestClass]
    public class HelperTests
    {
        [TestMethod]
        public void TestGenGraphQLBody()
        {
            var res = StatHelper.BuildGraphQLBody("xx", "name_1", "val_1");
        }

        [TestMethod]
        public void GetBattleIdForStatInk()
        {
            var res = StatHelper.GetBattleIdForStatInk(
                "VnNIaXN0b3J5RGV0YWlsLXUtYXZzZHRtcXd4emJxZnNmamFubW06QkFOS0FSQToyMDIyMTEwOFQxMTU2NDJfNDExNTI2NzQtZDA1NC00ZWQ2LWFjMDMtZThiNmU3ZDhjMmY3");
            Assert.AreEqual(res, "9bd66c87-afa8-5fc6-8f3d-e92c8f2daf2f");
        }

        [TestMethod]
        public void TestConvertToStatInkBody()
        {
            var groupStr = File.ReadAllText("./convert/1/Group.json");
            var detailStr = File.ReadAllText("./convert/1/Detail.json");
            var res = StatHelper.BuildStatInkBattleBody(detailStr, groupStr);
        }


        [TestMethod]
        public void TestConvertToStatInkBody_1()
        {
            var groupStr = File.ReadAllText("./samples/Detail_XMatch/0_group.json");
            var detail_1 = File.ReadAllText("./samples/Detail_XMatch/1.json");
            var res = StatHelper.BuildStatInkBattleBody(detail_1, groupStr);
        }

        [TestMethod]
        public void TestWebViewParse()
        {
            var str = File.ReadAllText("./samples/resp/webview.txt");
            var res = StatHelper.ParseNinWebViewData(str);
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
            var res = StatHelper.BuildStatInkBattleBody(detail_1, groupStr);

        }
    }
}