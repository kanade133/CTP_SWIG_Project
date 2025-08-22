using System.Text.Json;

namespace Test
{
    internal class Program
    {
        static string traderFrontAddress = "tcp://182.254.243.31:40001";
        static string marketFrontAddress = "tcp://182.254.243.31:40011";
        static string brokerId = "9999";
        static string userId = "xxxxxx";
        static string password = "xxxxxx";

        static async Task Main(string[] args)
        {
            //await MarketTest();
            await TradeTest();

            Console.WriteLine("End.");
        }

        private static async Task MarketTest()
        {
            Console.WriteLine("Reading config.json");

            string configPath = "config.json";
            string configDevPath = "config.dev.json";
            if (File.Exists(configDevPath))
            {
                configPath = configDevPath;
            }
            var config = JsonSerializer.Deserialize<JsonDocument>(File.ReadAllText(configPath))!;
            marketFrontAddress = config.RootElement.GetProperty("frontAddress").GetString()!;
            brokerId = config.RootElement.GetProperty("brokerId").GetString()!;
            userId = config.RootElement.GetProperty("userId").GetString()!;
            password = config.RootElement.GetProperty("password").GetString()!;
            Console.WriteLine(marketFrontAddress);
            Console.WriteLine(brokerId);
            Console.WriteLine(userId);
            Console.WriteLine(password);

            Console.WriteLine(CThostFtdcMdApi.GetApiVersion());

            var mdSpi = new MyMdSpi();
            await mdSpi.Init();
            Console.WriteLine($"IsConnected: {mdSpi.IsConnected}");
            await mdSpi.Login();
            Console.WriteLine($"IsLogin: {mdSpi.IsLogin}");
            mdSpi.SubscribeMarketData("IF2509");
            await Task.Delay(Timeout.Infinite);
        }
        private static async Task TradeTest()
        {
            Console.WriteLine(CThostFtdcTraderApi.GetApiVersion());
            var tradeSpi = new MyTradeSpi();
            tradeSpi.Init();
            await Task.Delay(Timeout.Infinite);
        }

        class MyMdSpi : CThostFtdcMdSpi
        {
            public bool IsConnected { get; private set; } = false;
            public bool IsLogin { get; private set; } = false;

            private CThostFtdcMdApi? _mdApi;
            private int _requestId = 0;
            private TaskCompletionSource? _tcs;

            public Task Init()
            {
                System.IO.Directory.CreateDirectory("spi/MD_");
                _mdApi = CThostFtdcMdApi.CreateFtdcMdApi("spi/MD_");
                _mdApi.RegisterSpi(this);
                _mdApi.RegisterFront(marketFrontAddress);
                var task = Request();
                _mdApi.Init();
                Console.WriteLine("Initing...");
                return task;
            }
            public Task Login()
            {
                var task = Request();
                int? requestCode = _mdApi?.ReqUserLogin(new CThostFtdcReqUserLoginField
                {
                    BrokerID = brokerId,
                    UserID = userId,
                    Password = password,
                }, ++_requestId);
                Console.WriteLine($"Request User Login: {requestCode}");
                if (requestCode == 0)
                {
                    return task;
                }
                else
                {
                    Response();
                    return Task.CompletedTask;
                }
            }
            public bool SubscribeMarketData(string instrumentId)
            {
                int? requestCode = _mdApi?.SubscribeMarketData([instrumentId], 1);
                Console.WriteLine($"Request Subscribe Market Data: instrumentId: {instrumentId}, requestCode: {requestCode}");
                return requestCode == 0;
            }
            private Task Request()
            {
                if (_tcs != null && !_tcs.Task.IsCompleted)
                {
                    return Task.FromException(new InvalidOperationException("Request already in progress."));
                }
                else
                {
                    _tcs = new TaskCompletionSource();
                    return _tcs.Task;
                }
            }
            private bool Response()
            {
                if (_tcs == null || _tcs.Task.IsCompleted)
                {
                    Console.WriteLine("No request in progress.");
                    return false;
                }
                else
                {
                    var tcs = _tcs;
                    _tcs = null;
                    tcs.SetResult();
                    return true;
                }
            }

            public override void OnFrontConnected()
            {
                IsConnected = true;
                Console.WriteLine("Front connected.");
                Response();
            }
            public override void OnFrontDisconnected(int nReason)
            {
                IsConnected = false;
                Console.WriteLine($"Front disconnected. Reason: {nReason}");
            }
            public override void OnRspError(CThostFtdcRspInfoField pRspInfo, int nRequestID, bool bIsLast)
            {
                Console.WriteLine($"Error response received. ErrorID: {pRspInfo.ErrorID}, ErrorMsg: {pRspInfo.ErrorMsg}");
            }
            public override void OnHeartBeatWarning(int nTimeLapse)
            {
                Console.WriteLine($"Heartbeat warning. Time lapse: {nTimeLapse}");
            }
            public override void OnRspUserLogin(CThostFtdcRspUserLoginField pRspUserLogin, CThostFtdcRspInfoField pRspInfo, int nRequestID, bool bIsLast)
            {
                IsLogin = pRspInfo.ErrorID == 0;
                Console.WriteLine($"User login response received. ErrorID: {pRspInfo.ErrorID}, ErrorMsg: {pRspInfo.ErrorMsg}");
                Response();
            }
            public override void OnRspUserLogout(CThostFtdcUserLogoutField pUserLogout, CThostFtdcRspInfoField pRspInfo, int nRequestID, bool bIsLast)
            {
                IsLogin = pRspInfo.ErrorID == 0;
                Console.WriteLine($"User logout response received. ErrorID: {pRspInfo.ErrorID}, ErrorMsg: {pRspInfo.ErrorMsg}");
            }
            public override void OnRtnDepthMarketData(CThostFtdcDepthMarketDataField pDepthMarketData)
            {
                Console.WriteLine($"Market data: {pDepthMarketData.InstrumentID} - {pDepthMarketData.LastPrice}");
            }
            public override void OnRspSubMarketData(CThostFtdcSpecificInstrumentField pSpecificInstrument, CThostFtdcRspInfoField pRspInfo, int nRequestID, bool bIsLast)
            {
                Console.WriteLine($"Subscription response for market data: {pSpecificInstrument.InstrumentID}");
            }
            public override void OnRspUnSubMarketData(CThostFtdcSpecificInstrumentField pSpecificInstrument, CThostFtdcRspInfoField pRspInfo, int nRequestID, bool bIsLast)
            {
                Console.WriteLine($"Unsubscription response for market data: {pSpecificInstrument.InstrumentID}");
            }
            public override void OnRspQryMulticastInstrument(CThostFtdcMulticastInstrumentField pMulticastInstrument, CThostFtdcRspInfoField pRspInfo, int nRequestID, bool bIsLast)
            {
                Console.WriteLine($"Multicast instrument query response: {pMulticastInstrument.InstrumentID}");
            }
            public override void OnRspSubForQuoteRsp(CThostFtdcSpecificInstrumentField pSpecificInstrument, CThostFtdcRspInfoField pRspInfo, int nRequestID, bool bIsLast)
            {
                Console.WriteLine($"Subscription response for quote: {pSpecificInstrument.InstrumentID}");
            }
            public override void OnRspUnSubForQuoteRsp(CThostFtdcSpecificInstrumentField pSpecificInstrument, CThostFtdcRspInfoField pRspInfo, int nRequestID, bool bIsLast)
            {
                Console.WriteLine($"Unsubscription response for quote: {pSpecificInstrument.InstrumentID}");
            }
            public override void OnRtnForQuoteRsp(CThostFtdcForQuoteRspField pForQuoteRsp)
            {
                Console.WriteLine($"Quote response received for instrument: {pForQuoteRsp.InstrumentID}");
            }
        }
        class MyTradeSpi : CThostFtdcTraderSpi
        {
            private CThostFtdcTraderApi? _tradeApi;

            public void Init()
            {
                System.IO.Directory.CreateDirectory("spi/Trade_");
                _tradeApi = CThostFtdcTraderApi.CreateFtdcTraderApi("spi/Trade_");
                _tradeApi.RegisterSpi(this);
                _tradeApi.RegisterFront(traderFrontAddress);
                _tradeApi.Init();
                Console.WriteLine("Initing...");
            }

            public override void OnFrontConnected()
            {
                base.OnFrontConnected();
                Console.WriteLine("Trade front connected.");
            }
        }
    }
}
