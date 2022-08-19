namespace FoE.Farmer.Library.Payloads
{
    public class TreasureHuntService : Payload
    {
        private const string ClassName = "HiddenRewardService";

        /// <summary>
        /// Nacte informace o honbe za pokladem
        /// </summary>
        /// <returns></returns>
        public static Payload GetOverview()
        {
            return new Payload
            {
                RequestClass = ClassName,
                RequestMethod = "getOverview"
            };
        }

        /// <summary>
        /// Posbira odmeny
        /// </summary>
        /// <returns></returns>
        public static Payload CollectTreasure()
        {
            return new Payload
            {
                RequestClass = ClassName,
                RequestMethod = "collectReward"
            };
        }
    }
}