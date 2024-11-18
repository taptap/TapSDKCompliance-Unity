using System;
using System.Threading.Tasks;
using TapSDK.Login;
using TapSDK.Compliance.Internal;
using TapSDK.Compliance.Model;
using System.Collections.Generic;
using TapSDK.Compliance.Standalone.Internal;
using TapSDK.Core;

namespace TapSDK.Compliance
{
    public sealed class ComplianceNewJob : IComplianceJob
    {
        internal bool UseAgeRange = true;

         // 是否正在处理用户信息，当调用 startup 接口后设置为 true， 当通知游戏回调时设置为 false
        internal volatile bool isCheckingUser = false;
        private List<Action<int, string>> _externalCallbackList;

        public List<Action<int, string>> ExternalCallbackList
        {
            get => _externalCallbackList;
        }
        
        public Task<int> GetAgeRange()
        {
            var tcs = new TaskCompletionSource<int>();
            if (!Verification.IsVerified || !UseAgeRange){
                tcs.TrySetResult(-1);
            } 
            if(Verification.AgeLimit < Verification.UNKNOWN_AGE){
                tcs.TrySetResult(Verification.AgeLimit);
            }else{
                tcs.TrySetResult(-1);
            }
            return tcs.Task;
            
        }

        /// <summary>
        /// 剩余时间(单位:秒)
        /// </summary>
        public Task<int> GetRemainingTime()
        {
            int time = 0;
            if (TapTapComplianceManager.CurrentRemainSeconds == null){
                time = 0;
            }else{
                if (Verification.IsAdult){
                    time = 9999;
                }else{
                    time =  TapTapComplianceManager.CurrentRemainSeconds.Value;
                }
            }
            var tcs = new TaskCompletionSource<int>();
            tcs.TrySetResult(time);
            return tcs.Task;
            
        }


        
        public Task<string> GetCurrentToken()
        {
            var tcs = new TaskCompletionSource<string>();
            if (!Verification.IsVerified){
                tcs.TrySetResult("");
            } else{
                tcs.TrySetResult(Verification.GetCurrentToken());
            }
            return tcs.Task;
        }
        
        public void Init(string clientId, string clientToken, TapTapComplianceOption config) {
            UseAgeRange = config.useAgeRange;
            TapTapComplianceManager.Init(clientId, clientToken, config);
            TapComplianceTracker.Instance.TrackInit();
        }

        public void RegisterComplianceCallback(Action<int, string> callback){
            if(_externalCallbackList == null){
                _externalCallbackList = new List<Action<int, string>>();
            }
            if(!_externalCallbackList.Contains(callback)){
                _externalCallbackList.Add(callback);
            }
        }
        
        public async void Startup(string userId)
        {
            // 如果正在处理中，直接返回
            if (isCheckingUser) {
                TapLogger.Debug(" current user is checking so return");
                return;
            }
            isCheckingUser = true;
            string sessionId = Guid.NewGuid().ToString();
            TapComplianceTracker.Instance.TrackStart("startup", sessionId);
            if(TapTapComplianceManager.UserId != null){
                TapTapComplianceManager.ClearUserCache();
            }
            var code = await TapTapComplianceManager.StartUp(userId);
            switch(code){ 
                case StartUpResult.LOGIN_SUCCESS:
                case StartUpResult.PERIOD_RESTRICT:
                case StartUpResult.DURATION_LIMIT:
                case StartUpResult.AGE_LIMIT:
                    TapComplianceTracker.Instance.TrackSuccess("startup", sessionId);
                    break;
                case StartUpResult.INVALID_CLIENT_OR_NETWORK_ERROR:
                    TapComplianceTracker.Instance.TrackFailure("startup", sessionId, code, "invalid client or network error");
                    break;
                case StartUpResult.EXITED:
                case StartUpResult.SWITCH_ACCOUNT:
                    break;
                case StartUpResult.REAL_NAME_STOP:
                    TapComplianceTracker.Instance.TrackCancel("startup", sessionId);
                    break;
            }
            OnInvokeExternalCallback(code,null);
        }
        

        // ReSharper disable Unity.PerformanceAnalysis

        public void Exit()
        {
            TapTapComplianceManager.Logout();
            if(_externalCallbackList != null){
                foreach(Action<int, string> callback in _externalCallbackList){
                      callback?.Invoke(StartUpResult.EXITED, null);  
                }
            }
        }


        public async void CheckPaymentLimit(long amount, Action<CheckPayResult> handleCheckPayLimit, Action<string> handleCheckPayLimitException)
        {
            try
            {
                var payResult = await TapTapComplianceManager.CheckPayLimit(amount);
                handleCheckPayLimit?.Invoke(new CheckPayResult()
                {
                    // status 为 1 时可以支付
                    status = payResult.Status ? 1 : 0,
                    title = payResult.Title,
                    description = payResult.Content
                });
            }
            catch (Exception e)
            {
                handleCheckPayLimitException?.Invoke(e.Message);
                if(e is ComplianceException aee && aee.IsTokenExpired()){
                    Exit();
                }
            }
        }

        public async void SubmitPayment(long amount, Action handleSubmitPayResult, Action<string> handleSubmitPayResultException)
        {
            try
            {
                await TapTapComplianceManager.SubmitPayResult(amount);
                handleSubmitPayResult?.Invoke();
            }
            catch (Exception e)
            {
                handleSubmitPayResultException?.Invoke(e.Message);
                if(e is ComplianceException aee && aee.IsTokenExpired()){
                    Exit();
                }
            }
        }

        
        public void SetTestEnvironment(bool enable) {
            TapTapComplianceManager.SetTestEnvironment(enable);
        }

        public void OnInvokeExternalCallback(int code, string msg){
            switch(code){
                case StartUpResult.LOGIN_SUCCESS:
                    TapTapComplianceManager.CanPlay = true;
                    break;
                case StartUpResult.AGE_LIMIT:
                case StartUpResult.PERIOD_RESTRICT:
                case StartUpResult.DURATION_LIMIT:
                case StartUpResult.EXITED:
                case StartUpResult.INVALID_CLIENT_OR_NETWORK_ERROR:
                case StartUpResult.SWITCH_ACCOUNT:
                    TapTapComplianceManager.CanPlay = false;
                    break;
            }
            if (code == StartUpResult.LOGIN_SUCCESS // 通过校验
                || code == StartUpResult.REAL_NAME_STOP // 取消校验
                || code == StartUpResult.EXITED // 登出用户
                || code == StartUpResult.AGE_LIMIT // 年龄限制
                || code == StartUpResult.SWITCH_ACCOUNT // 切换账号
                || code == StartUpResult.INVALID_CLIENT_OR_NETWORK_ERROR) { // 网络异常
                    // 用户结束校验流程
                    isCheckingUser = false;
                }
            if (StartUpResult.Contains(code)){
                if(_externalCallbackList != null){
                    foreach(Action<int, string> callback in _externalCallbackList){
                        callback?.Invoke(code, msg);
                    }
                }
            }
        }

    }
}
