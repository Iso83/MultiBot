namespace FoE.Farmer.Library.Payloads
{
    public class StartupService
    {
        private const string ClassName = "StartupService";

        public static Payload GetData()
        {
            return new Payload
            {
                RequestClass = ClassName,
                RequestMethod = "getData"
            };
        }
    }
}