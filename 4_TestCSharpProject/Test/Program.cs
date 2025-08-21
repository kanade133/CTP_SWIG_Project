using System.Text.Json;

namespace Test
{
    internal class Program
    {
        static string frontAddress = "tcp://182.254.243.31:40011";
        static string brokerId = "9999";
        static string userId = "xxxxxx";
        static string password = "xxxxxx";

        static async Task Main(string[] args)
        {
            Console.WriteLine("Reading config.json");

            string configPath = "config.json";
            string configDevPath = "config.dev.json";
            if (File.Exists(configDevPath))
            {
                configPath = configDevPath;
            }
            var config = JsonSerializer.Deserialize<JsonDocument>(File.ReadAllText(configPath))!;
            frontAddress = config.RootElement.GetProperty("frontAddress").GetString()!;
            brokerId = config.RootElement.GetProperty("brokerId").GetString()!;
            userId = config.RootElement.GetProperty("userId").GetString()!;
            password = config.RootElement.GetProperty("password").GetString()!;
            Console.WriteLine(frontAddress);
            Console.WriteLine(brokerId);
            Console.WriteLine(userId);
            Console.WriteLine(password);

            Console.WriteLine(CThostFtdcMdApi.GetApiVersion());

            var mdSpi = new MyMdSpi();
            mdSpi.Init();
            await Task.Delay(Timeout.Infinite);

            Console.WriteLine("End.");
        }

        class MyMdSpi : CThostFtdcMdSpi
        {
            CThostFtdcMdApi? mdApi;
            int requestId = 0;

            public void Init()
            {
                System.IO.Directory.CreateDirectory("spi/MD_");
                mdApi = CThostFtdcMdApi.CreateFtdcMdApi("spi/MD_");
                mdApi.RegisterSpi(this);
                mdApi.RegisterFront(frontAddress);
                mdApi.Init();
                Console.WriteLine("Initing...");
            }

            public override void OnFrontConnected()
            {
                Console.WriteLine("Front connected.");

                int? requestCode = mdApi?.ReqUserLogin(new CThostFtdcReqUserLoginField
                {
                    BrokerID = brokerId,
                    UserID = userId,
                    Password = password,
                }, ++requestId);
                Console.WriteLine($"Request User Login: {requestCode}");
            }
            public override void OnRspError(CThostFtdcRspInfoField pRspInfo, int nRequestID, bool bIsLast)
            {
                Console.WriteLine($"Error response received. ErrorID: {pRspInfo.ErrorID}, ErrorMsg: {pRspInfo.ErrorMsg}");
            }
            public override void OnHeartBeatWarning(int nTimeLapse)
            {
                Console.WriteLine($"Heartbeat warning. Time lapse: {nTimeLapse}");
            }
            public override void OnFrontDisconnected(int nReason)
            {
                Console.WriteLine($"Front disconnected. Reason: {nReason}");
            }
            public override void OnRspUserLogin(CThostFtdcRspUserLoginField pRspUserLogin, CThostFtdcRspInfoField pRspInfo, int nRequestID, bool bIsLast)
            {
                Console.WriteLine($"User login response received. ErrorID: {pRspInfo.ErrorID}, ErrorMsg: {pRspInfo.ErrorMsg}");
            }
            public override void OnRtnDepthMarketData(CThostFtdcDepthMarketDataField pDepthMarketData)
            {
                Console.WriteLine($"Market data: {pDepthMarketData.InstrumentID} - {pDepthMarketData.LastPrice}");
            }
            public override void OnRspSubMarketData(CThostFtdcSpecificInstrumentField pSpecificInstrument, CThostFtdcRspInfoField pRspInfo, int nRequestID, bool bIsLast)
            {
                Console.WriteLine($"Subscription response for market data: {pSpecificInstrument.InstrumentID}");
            }
        }
    }
}
