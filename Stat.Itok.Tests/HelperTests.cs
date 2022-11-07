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
        public void TestExtarctBattleId()
        {
            var files = Directory.GetFiles("./samples");
            foreach (var file in files)
            {
                var content = File.ReadAllText(file);
                var res = StatHelper.ExtractBattleIds(content,
                    QueryHash.BattleQueries[Path.GetFileNameWithoutExtension(file)]);
                if (Path.GetFileNameWithoutExtension(file) != "PrivateBattleHistories")
                    Assert.IsTrue(res.Any());
            }
        }

        [TestMethod]
        public void TestConvertToStatInkBody()
        {
            var groupStr = File.ReadAllText("./convert/1/Group.json");
            var detailStr = File.ReadAllText("./convert/1/Detail.json");
            var res = StatHelper.BuildStatInkBattleBody(detailStr, groupStr);
        }
    }
}