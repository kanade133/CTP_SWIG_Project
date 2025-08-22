using System.Text.Json;

namespace Test
{
    internal class Program
    {
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
        };
        private static readonly Config _config = LoadConfig();

        static async Task Main(string[] args)
        {
            Console.WriteLine("Input what to do:");
            Console.WriteLine("1. Market; 2. Trader");
            string? key = Console.ReadLine();
            if (key == "1")
            {
                await MarketTest();
            }
            else if (key == "2")
            {
                await TraderTest();
            }
            else
            {
                Console.WriteLine(" Invalid selection.");
            }
            Console.WriteLine("End.");
        }

        private static async Task MarketTest()
        {
            Console.WriteLine(CThostFtdcMdApi.GetApiVersion());

            var mdSpi = new MyMdSpi();
            await mdSpi.Init();
            Console.WriteLine($"IsConnected: {mdSpi.IsConnected}");
            await mdSpi.Login();
            Console.WriteLine($"IsLogin: {mdSpi.IsLogin}");
            mdSpi.SubscribeMarketData("IF2509");
            await Task.Delay(Timeout.Infinite);
        }
        private static async Task TraderTest()
        {
            Console.WriteLine(CThostFtdcTraderApi.GetApiVersion());
            var tradeSpi = new MyTraderSpi();
            await tradeSpi.Init();
            Console.WriteLine("Trader API initialized.");
            await tradeSpi.Login();
            Console.WriteLine("Trader API login.");
            await tradeSpi.ReqQryTradingAccount();
            await Task.Delay(Timeout.Infinite);
        }
        private static Config LoadConfig()
        {
            Console.WriteLine("Reading config.json");
            string configPath = "config.json";
            string configDevPath = "config.dev.json";
            if (File.Exists(configDevPath))
            {
                configPath = configDevPath;
            }
            return JsonSerializer.Deserialize<Config>(File.ReadAllText(configPath), _jsonSerializerOptions)!;
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
                _mdApi.RegisterFront(_config.MarketFrontAddress);
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
                    BrokerID = _config.BrokerId,
                    UserID = _config.UserId,
                    Password = _config.Password,
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
        class MyTraderSpi : CThostFtdcTraderSpi
        {
            private CThostFtdcTraderApi? _tradeApi;
            private int _requestId = 0;
            private TaskCompletionSource? _tcs;

            public Task Init()
            {
                System.IO.Directory.CreateDirectory("spi/Trade_");
                _tradeApi = CThostFtdcTraderApi.CreateFtdcTraderApi("spi/Trade_");
                _tradeApi.RegisterSpi(this);
                _tradeApi.RegisterFront(_config.TraderFrontAddress);
                var task = Request();
                _tradeApi.Init();
                Console.WriteLine("Initing...");
                return task;
            }
            public Task Login()
            {
                var task = Request();
                int? requestCode = _tradeApi?.ReqUserLogin(new CThostFtdcReqUserLoginField
                {
                    BrokerID = _config.BrokerId,
                    UserID = _config.UserId,
                    Password = _config.Password,
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
            public Task ReqQryTradingAccount()
            {
                var task = Request();
                var requestCode = _tradeApi?.ReqQryTradingAccount(new CThostFtdcQryTradingAccountField
                {
                    BrokerID = _config.BrokerId,
                    InvestorID = _config.UserId,
                }, ++_requestId);
                if (requestCode == 0)
                {
                    return task;
                }
                else
                {
                    Console.WriteLine($"Request Qry Trading Account failed: {requestCode}");
                    return Task.CompletedTask;
                }
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
                base.OnFrontConnected();
                Console.WriteLine("Trader front connected.");
                Response();
            }
            public override void OnFrontDisconnected(int nReason)
            {
                base.OnFrontDisconnected(nReason);
                Console.WriteLine($"Trader front disconnected. Reason: {nReason}");
            }
            public override void OnRspUserLogin(CThostFtdcRspUserLoginField pRspUserLogin, CThostFtdcRspInfoField pRspInfo, int nRequestID, bool bIsLast)
            {
                base.OnRspUserLogin(pRspUserLogin, pRspInfo, nRequestID, bIsLast);
                Console.WriteLine($"Trader user login response received. ErrorID: {pRspInfo.ErrorID}, ErrorMsg: {pRspInfo.ErrorMsg}");
                if (pRspInfo.ErrorID == 0)
                {
                    Response();
                }
            }
            public override void OnRspQryTradingAccount(CThostFtdcTradingAccountField pTradingAccount, CThostFtdcRspInfoField pRspInfo, int nRequestID, bool bIsLast)
            {
                base.OnRspQryTradingAccount(pTradingAccount, pRspInfo, nRequestID, bIsLast);
                Console.WriteLine($"Trading account query response received. Account: {pTradingAccount.BrokerID} - {pTradingAccount.Available}");
                Response();
            }
        }
        class Config
        {
            public string TraderFrontAddress { get; set; } = "";
            public string MarketFrontAddress { get; set; } = "";
            public string BrokerId { get; set; } = "";
            public string UserId { get; set; } = "";
            public string Password { get; set; } = "";
        }
    }
}
