using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TapSDK.Compliance.Internal.Http;
using TapSDK.Compliance.Model;
using TapSDK.Core;
using TapSDK.Login;
using TapSDK.Login.Internal;
using TapSDK.Login.Standalone;
using UnityEngine;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using TapSDK.Core.Standalone.Internal;
using TapSDK.Core.Standalone;
using System.Net;


namespace TapSDK.Compliance.Internal
{
    public static class Network {
        static readonly string ChinaHost = "https://tapsdk.tapapis.cn";

        private static ComplianceHttpClient
            HttpClient = new ComplianceHttpClient(ChinaHost);

        private static string gameId;

        private static string clientToken;
        
        private static bool enableTestMode;

        internal static void SetGameInfo(string gameId, string clientToken)
        {
            Network.gameId = gameId;
            Network.clientToken = clientToken;
            HttpClient.ChangeAddtionalHeader("X-LC-Id", gameId);
        }

        internal static void InitSetting()
        {
            HttpClient.ChangeAddtionalHeader("Accept-Language","zh-CN");
            string host = ChinaHost;
            if (HttpClient != null)
            {
                Type httpClientType = typeof(ComplianceHttpClient);
                var hostFieldInfo = httpClientType.GetField("serverUrl", BindingFlags.NonPublic | BindingFlags.Instance);
                hostFieldInfo?.SetValue(HttpClient, host);
            }
        }
        
        /// <summary>
        /// 拉取配置并缓存在内存
        /// 没有持久化的原因是无法判断 SDK 自带与本地持久化版本的高低
        /// </summary>
        /// <returns></returns>
        internal static async Task<RealNameConfigResult> FetchConfig(string userId) {
            string path = $"real-name/v1/get-global-config?client_id={gameId}&user_identifier={WebUtility.UrlEncode(userId)}";
            var headers = GetAuthHeaders(path,"GET", 0, null);
            RealNameConfigResponse response = await HttpClient.Get<RealNameConfigResponse>(path, headers);
            return response.Result;
        }

        /// <summary>
        /// 拉取实名认证数据
        /// </summary>
        /// <returns></returns>
        internal static async Task<VerificationResult> FetchVerification(string userId) 
        {
            string path = $"real-name/v1/anti-addiction-token?client_id={gameId}&user_identifier={WebUtility.UrlEncode(userId)}";
            var headers = GetAuthHeaders(path,"GET", 0, null);
            ServerVerificationResponse response = await HttpClient.Get<ServerVerificationResponse>(path, headers);
            return response.Result;
        }

         /// <summary>
        /// V1 升级 v2 token
        internal static async Task<VerificationResult> UpgradeToken(string userId, string oldToken) 
        {
            string path = $"real-name/v1/anti-addiction-token-upgrade?client_id={gameId}";
            var param = new Dictionary<string, object> {
                ["anti_addiction_token_v1"] = oldToken,
                ["user_identifier"] = userId
            };
            var headers = GetAuthHeaders(path,"POST", 0, param);
            ServerVerificationResponse response = await HttpClient.Post<ServerVerificationResponse>(path, headers, data:param);
            return response.Result;
        }

        /// </summary>
        /// 使用 TapToken 获取实名 token
        /// <returns></returns>
        public static async Task<VerificationResult> FetchVerificationByTapToken(string userId, AccessToken token, long timestamp = 0) {
            string path = $"real-name/v1/anti-addiction-token-taptap?client_id={gameId}&user_identifier={WebUtility.UrlEncode(userId)}";            
            var httpClientType = typeof(ComplianceHttpClient);
            var hostFieldInfo = httpClientType.GetField("serverUrl", BindingFlags.NonPublic | BindingFlags.Instance);
            string host = hostFieldInfo?.GetValue(HttpClient) as string;
            var uri = new Uri(host  + "/" +  path);

            var sign = GetMacToken(token, uri, timestamp);
            var headers = GetAuthHeaders(path,"GET", (int)timestamp, null);
            headers.Add("Authorization", sign);
            ServerVerificationResponse response = await HttpClient.Get<ServerVerificationResponse>(path, headers:headers);
            return response.Result;
        }
        
        private static string GetMacToken(AccessToken token, Uri uri, long timestamp = 0) {
            TapLogger.Debug(" uri = " + uri.Host + " path = " + uri.PathAndQuery + " token mac = "
             + token.macKey);
            int ts = (int)timestamp;
            if (ts == 0) {
                var dt = DateTime.UtcNow - new DateTime(1970, 1, 1);
                ts = (int)dt.TotalSeconds;
            }
            TapLogger.Debug(" GetMacToken ts = " + ts);
            var sign = "MAC " + LoginService.GetAuthorizationHeader(token.kid,
                token.macKey,
                token.macAlgorithm,
                "GET",
                uri.PathAndQuery,
                uri.Host,
                "443", ts);
            return sign;
        }
        
        /// <summary>
        /// 检测身份信息是否通过
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="idCard">身份证信息</param>
        /// <returns></returns>
        internal static async Task<VerificationResult> FetchVerificationManual(string userName, string idCard)
        {
            var tcs = new TaskCompletionSource<VerificationResult>();
            string path = $"real-name/v1/anti-addiction-token-manual?client_id={gameId}";
            Dictionary<string, object> data = new Dictionary<string, object>
            {
                ["name"] = userName,
                ["idcard"] = idCard,
                ["user_identifier"] = TapTapComplianceManager.UserId
            };
             var headers = GetAuthHeaders(path,"POST", 0, data);
            ServerVerificationResponse response = await HttpClient.Post<ServerVerificationResponse>(path, headers, data: data);
            tcs.TrySetResult(response.Result);
            
            return await tcs.Task;
        }

        /// <summary>
        /// 获取用户配置
        /// </summary>
        /// <returns></returns>
        internal static async Task<UserComplianceConfigResult> CheckUserConfig() 
        {
            string path;
            if (!enableTestMode) {
             path = $"anti-addiction/v1/get-config-by-token?platform=pc&client_id={gameId}&user_identifier={WebUtility.UrlEncode(TapTapComplianceManager.UserId)}";
            }else{
             path = $"anti-addiction/v1/get-config-by-token?platform=pc&client_id={gameId}&test_mode=1&user_identifier={WebUtility.UrlEncode(TapTapComplianceManager.UserId)}";
            }
            Dictionary<string, object> headers = GetAuthHeaders(path,"GET",0,null);
            UserComplianceConfigResponse response = await HttpClient.Get<UserComplianceConfigResponse>(path, headers: headers);
            #if UNITY_EDITOR
            TapLogger.Debug($"检查用户状态: ageLimit: {response.Result.userState.ageLimit} ageCheck: {response.Result.ageCheckResult.allow}  IsAdult: {response.Result.userState.isAdult} ");
            #endif
            return response.Result;
        }
        /// <summary>
        /// 检测是否可玩
        /// </summary>
        /// <returns></returns>
        internal static async Task<PlayableResult> CheckPlayable() 
        {
            string path = "";
            if (!enableTestMode) {
                path = $"anti-addiction/v1/heartbeat?client_id={gameId}";
            }
            else {
                path = $"anti-addiction/v1/heartbeat?client_id={gameId}&test_mode=1";
            }
            Dictionary<string,object> data = new Dictionary<string,object>{
                ["session_id"] = TapTapComplianceManager.CurrentSession,
                ["user_identifier"] = TapTapComplianceManager.UserId
            };
            Dictionary<string, object> headers = GetAuthHeaders(path,"POST",0, data);
            PlayableResponse response = await HttpClient.Post<PlayableResponse>(path, headers: headers, data:data);
            #if UNITY_EDITOR
            TapLogger.Debug($"检查是否可玩结果: remainTime: {response.Result.RemainTime}  Content: {response.Result.Content}");
            #endif
            return response.Result;
        }

        /// <summary>
        /// 检测是否可充值
        /// </summary>
        /// <param name="amount"></param>
        /// <returns></returns>
        internal static async Task<PayableResult> CheckPayable(long amount) 
        {
            string path = "";
            if (!enableTestMode) {
                path = $"anti-addiction/v1/payable?client_id={gameId}&amount={amount}&user_identifier={WebUtility.UrlEncode(TapTapComplianceManager.UserId)}";
            }
            else {
                path = $"anti-addiction/v1/payable?client_id={gameId}&amount={amount}&test_mode=1&user_identifier={WebUtility.UrlEncode(TapTapComplianceManager.UserId)}";
            }
            Dictionary<string, object> headers = GetAuthHeaders(path, "GET", 0, null);
            PayableResponse response = await HttpClient.Get<PayableResponse>(path, headers: headers);
            return response.Result;
        }

        /// <summary>
        /// 上传充值操作
        /// </summary>
        /// <param name="amount"></param>
        /// <returns></returns>
        internal static async Task SubmitPayment(long amount) 
        {
            string path = "";
            if (!enableTestMode) {
                path = $"anti-addiction/v1/payment-submit?client_id={gameId}";
            }
            else {
                path = $"anti-addiction/v1/payment-submit?client_id={gameId}&test_mode=1";
            }
             Dictionary<string, object> data = new Dictionary<string, object> 
            {
                { "amount", amount },
                {"user_identifier", TapTapComplianceManager.UserId}
            };
            Dictionary<string, object> headers = GetAuthHeaders(path, "POST", 0, data);
           
            await HttpClient.Post<SubmitPaymentResponse>(path, headers:headers, data: data);
        }
        // internal static Dictionary<string, object> GetAuthHeaders(){
        //     return new Dictionary<string,object>{};
        // }

        internal static Dictionary<string, object> GetAuthHeaders(string path, string httpMethod,int timestamp, Dictionary<string,object> body) 
        {
            var httpClientType = typeof(ComplianceHttpClient);
            var hostFieldInfo = httpClientType.GetField("serverUrl", BindingFlags.NonPublic | BindingFlags.Instance);
            string host = hostFieldInfo?.GetValue(HttpClient) as string;
            var uri =  "/" +  path;
            int ts = timestamp;
            if (ts == 0) {
                var dt = DateTime.UtcNow - new DateTime(1970, 1, 1);
                ts = (int)dt.TotalSeconds;
            }
            var nonce = new System.Random().Next().ToString();
            var headers = new Dictionary<string, object> 
            {
                { "X-Tap-PN", "TapSDK" },
                { "X-Tap-Lang", Tracker.getServerLanguage() },
                { "X-Tap-Device-Id", SystemInfo.deviceUniqueIdentifier},
                { "X-Tap-Platform", "PC"},
                { "X-Tap-SDK-Module","TapCompliance"},
                { "X-Tap-SDK-Module-Version", TapTapSDK.Version},
                { "X-Tap-SDK-Artifact", "Unity"},
                { "User-Agent","TapSDK-Unity/" + TapTapSDK.Version},
                { "X-Tap-Nonce", nonce},
                { "X-Tap-Ts",ts}
            };
            string token = Verification.GetCurrentToken();
            if (!string.IsNullOrEmpty(token)) 
            {
                headers.Add("X-Tap-Anti-Addiction-Token", token);
            }
            
            string currentDBUserId = TapCoreStandalone.GetCurrentUserId();
            if(currentDBUserId != null && currentDBUserId.Length > 0) {
                headers.Add("X-Tap-SDK-Game-User-Id", currentDBUserId);
            }
            
             headers = headers.OrderBy(x => x.Key).ToDictionary(x => x.Key, y=>y.Value);
             List<string> headerList = new List<string>();
             foreach(KeyValuePair<string,object> kv in headers){
                if(kv.Key.ToLower().StartsWith("x-tap-")){
                    headerList.Add(kv.Key.ToLower()+":"+kv.Value);
                }
             }
             string headerString = string.Join("\n",headerList);
             var normalizedString = $"{httpMethod}\n{uri}\n{headerString}\n";
             if(body != null){
                normalizedString += $"{JsonConvert.SerializeObject(body)}\n";
             }else{
                normalizedString += "\n";
             }

            HashAlgorithm hashGenerator= new HMACSHA256(Encoding.UTF8.GetBytes(clientToken));

            var hash = Convert.ToBase64String(hashGenerator.ComputeHash(Encoding.UTF8.GetBytes(normalizedString)));
            headers.Add("X-Tap-Sign",hash);
            return headers;
        }

        internal static void SetTestEnvironment(bool enable) {
            enableTestMode = enable;
        }
    }
}
