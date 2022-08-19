using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace FoE.Farmer.Library.Services
{
    public class TreasureHuntService
    {
        public static DateTime NextCheckTime { get; set; } = DateTime.MinValue;

        public static async void CheckTreasureHunt()
        {
            if (NextCheckTime > DateTime.Now) return;
            Manager.Log("Checking treasure hunt...");

            var plainData = await Payloads.TreasureHuntService.GetOverview().Send();
            var timeService = Helper.GetObjectByClass(plainData, "TimeService", "updateTime");
            var curServerTime = timeService["responseData"]["time"].ToObject<long>();

            var data = Helper.GetObjectByClass(plainData, "HiddenRewardService", "getOverview");

        changedChestList:
            var chests = data["responseData"]["hiddenRewards"] as JArray;
            if (chests == null) return;

            for (var i = 0; i < chests.Count; i++)
            {
                var chest = chests[i];
                if (chest["__class__"].ToString() == "HiddenReward")
                {
                    long
                        showingTime = chest["startTime"].ToObject<long>(),
                        expireTime = chest["expireTime"].ToObject<long>();

                    if (showingTime < curServerTime && expireTime >= curServerTime)
                    {
                        if (TimeSpan.FromTicks(curServerTime - showingTime) < Helper.GetRandomMinutes(2, 8)) // collect
                        {
                            var treasurePayLoad = Payloads.TreasureHuntService.CollectTreasure();
                            treasurePayLoad.RequestData = new JArray(chest["hiddenRewardId"]);
                            var treasureResponse = await treasurePayLoad.Send();

                            Manager.Log($"- Treasure hunt - collected reward({chest["type"].ToObject<string>()})");
                            System.Threading.Thread.Sleep(Helper.GetRandomSeconds(10, 25).Milliseconds);

                            var newData = Helper.GetObjectByClass(treasureResponse, "HiddenRewardService", "getOverview");
                            if (newData != null)
                            {
                                data = newData;
                                goto changedChestList;
                            }
                        }
                    }
                }
            }

            NextCheckTime = DateTime.Now + Helper.GetRandomMinutes();
            Manager.Log("- Treasure hunt - Complete treasure hunt, next start: " + NextCheckTime.ToLocalTime());
        }
    }
}